using System.Runtime.CompilerServices;
using Boaz.Core.Hub.Auditoria.Card.Domain;
using Boaz.Core.Hub.Auditoria.Card.Dtos;
using Boaz.Core.Hub.Auditoria.Card.UseCases;
using Boaz.Core.Hub.Auditoria.Metricas.Domain;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Domain;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Dtos;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Ports.In;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Ports.Out;
using Boaz.Core.Hub.Auditoria.Registros.Domain;
using Boaz.Core.Hub.Auditoria.Registros.Ports.In;
using Boaz.Core.Hub.Auditoria.Saude.Domain;
using Boaz.Core.Hub.Auditoria.Saude.Ports.Out;

namespace Boaz.Core.Hub.Auditoria.Ocorrencias.UseCases;

/// <summary>As ocorrências novas como eventos "ocorrencia", para todo ao vivo da Auditoria (RF-204): o mesmo corpo de um item da lista.</summary>
public static class FonteDeOcorrencias
{
    /// <summary>Um evento por ocorrência avisada, até a conexão terminar.</summary>
    public static async IAsyncEnumerable<EventoAoVivo> Eventos(IAssinatura<Ocorrencia> assinatura, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var ocorrencia in assinatura.Avisos(ct)) yield return new EventoAoVivo("ocorrencia", ocorrencia.Id, OcorrenciaDto.Da(ocorrencia));
    }
}

/// <summary>
/// GET /auditoria/ocorrencias/abertas (RF-204): a última mudança de cada alvo fora de No ar, FORA_DO_AR antes de LENTO e a
/// mais nova primeiro, e as voltas a No ar dos últimos 10 min. Lida do Redis, sem o banco (RNF-252); não é auditada.
/// </summary>
public sealed class VerOcorrenciasAbertas(IOcorrenciasAbertas abertas, TimeProvider relogio) : IVerOcorrenciasAbertas
{
    /// <inheritdoc />
    public async Task<AbertasDto> Executar(CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow();
        var emAberto = await abertas.Abertas(ct);
        var voltas = await abertas.Voltaram(agora, ct);
        return new AbertasDto(
            [.. emAberto.OrderBy(o => o.Gravidade).ThenByDescending(o => o.Quando).Select(OcorrenciaDto.Da)],
            [.. voltas.OrderByDescending(o => o.Quando).Select(OcorrenciaDto.Da)]);
    }
}

/// <summary>GET /auditoria/ocorrencias (RF-205, RN-321): as ocorrências do período, da mais nova para a mais antiga. Sem o banco, 503.</summary>
public sealed class ListarOcorrencias(IOcorrencias ocorrencias, IUltimaVerificacao ultima, IRegistrarConsulta registrar, TimeProvider relogio) : IListarOcorrencias
{
    /// <inheritdoc />
    public async Task<OcorrenciasDto> Executar(
        string? de, string? ate, string? tipo, string? alvo, string? para, string? depois, string? limite, ConsultaPedida consulta, CancellationToken ct = default)
    {
        TipoDoAlvo? tipoPedido = tipo is null ? null : TextosDaOcorrencia.TipoDe(tipo) ?? throw FalhaDaAuditoria.DadosInvalidos("tipo");
        if (alvo is not null && !Servico.Todos.Any(s => s.Id == alvo) && !ModuloDoEndereco.Valido(alvo)) throw FalhaDaAuditoria.DadosInvalidos("alvo");
        EstadoDeOcorrencia? paraPedido = para is null ? null : TextosDaOcorrencia.EstadoDe(para) ?? throw FalhaDaAuditoria.DadosInvalidos("para");
        var comeco = Periodo.Ler(de, "de");
        var fim = Periodo.Ler(ate, "ate");
        var cursor = Cursor.Ler(depois);
        var tamanho = Cursor.LerLimite(limite);
        var periodo = Periodo.Criar(comeco, fim, Periodo.MaximoDeDias, relogio.GetUtcNow());
        if (ultima.BancoForaDoAr) throw FalhaDaAuditoria.BancoDeDadosFora();

        var pagina = await ocorrencias.Listar(periodo, tipoPedido, alvo, paraPedido, cursor, tamanho, ct);
        await registrar.Executar(consulta, ct);
        return new OcorrenciasDto(periodo.De, periodo.Ate, [.. pagina.Itens.Select(OcorrenciaDto.Da)], pagina.Proximo?.Texto);
    }
}

/// <summary>GET /auditoria/ocorrencias/ao-vivo (RF-204, RNF-249): o evento "abertas" ao abrir e um "ocorrencia" a cada mudança, em até 5 s.</summary>
public sealed class AcompanharOcorrenciasAoVivo(
    IVerOcorrenciasAbertas abertas, IAvisosDeOcorrencias avisos, AoVivoDaAuditoria aoVivo) : IAcompanharOcorrenciasAoVivo
{
    /// <inheritdoc />
    public async Task<AoVivoAberto> Abrir(QuemPede quem, CancellationToken ct = default)
    {
        // assina antes de ler: a ocorrência que chega no meio não se perde
        var assinatura = await avisos.Assinar(ct);
        try
        {
            var primeiro = new EventoAoVivo("abertas", null, await abertas.Executar(ct));
            return await aoVivo.Abrir(quem, [primeiro], [c => FonteDeOcorrencias.Eventos(assinatura, c)], [assinatura], ct);
        }
        catch
        {
            await assinatura.DisposeAsync();
            throw;
        }
    }
}
