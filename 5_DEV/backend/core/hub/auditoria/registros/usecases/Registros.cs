using System.Runtime.CompilerServices;
using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Hub.Auditoria.Card.Domain;
using Boaz.Core.Hub.Auditoria.Card.Dtos;
using Boaz.Core.Hub.Auditoria.Card.UseCases;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Ports.Out;
using Boaz.Core.Hub.Auditoria.Ocorrencias.UseCases;
using Boaz.Core.Hub.Auditoria.Registros.Domain;
using Boaz.Core.Hub.Auditoria.Registros.Dtos;
using Boaz.Core.Hub.Auditoria.Registros.Ports.In;
using Boaz.Core.Hub.Auditoria.Registros.Ports.Out;
using Boaz.Core.Hub.Auditoria.Saude.Ports.Out;

namespace Boaz.Core.Hub.Auditoria.Registros.UseCases;

/// <summary>
/// O registro de toda consulta atendida (RN-325): CONSULTAR_AUDITORIA em INICIADO e CONCLUIDO, com o alvo, quem e o traceId do
/// X-Trace-Id. A mesma consulta da mesma pessoa em menos de 5 min não grava de novo (a leitura de uma aba ao vivo não
/// enche a auditoria); a consulta recusada nunca chega aqui. Com o banco fora, a consulta responde sem registro.
/// </summary>
public sealed class RegistrarConsulta(IAuditoria auditoria, IConsultasRecentes recentes, IUltimaVerificacao ultima, TimeProvider relogio) : IRegistrarConsulta
{
    /// <inheritdoc />
    public async Task<ResultadoDaConsulta> Executar(ConsultaPedida consulta, CancellationToken ct = default)
    {
        if (ultima.BancoForaDoAr) return ResultadoDaConsulta.SemRegistro;
        try
        {
            var inicio = relogio.GetUtcNow();
            if (!await recentes.Primeira(consulta.Quem.UsuarioId, consulta.Alvo, inicio, ConsultaDaAuditoria.Janela, ct)) return ResultadoDaConsulta.Repetida;
            await auditoria.Registrar(Registro(consulta, EtapaDaExecucao.Iniciado, inicio), ct);
            // o CONCLUIDO sempre depois do INICIADO, para a lista os mostrar na ordem
            var fim = relogio.GetUtcNow();
            await auditoria.Registrar(Registro(consulta, EtapaDaExecucao.Concluido, fim > inicio ? fim : inicio.AddMilliseconds(1)), ct);
            return ResultadoDaConsulta.Gravada;
        }
        catch (Exception falha) when (falha is not OperationCanceledException)
        {
            return ResultadoDaConsulta.SemRegistro;
        }
    }

    static RegistroDeAuditoria Registro(ConsultaPedida consulta, EtapaDaExecucao etapa, DateTimeOffset quando) =>
        new(consulta.TraceId, etapa, null, consulta.Quem.UsuarioId, AcaoAuditada.ConsultarAuditoria, consulta.Alvo, quando);
}

/// <summary>Põe nomes nos registros: o nome de quem fez e a sigla da conta, lidos de usuarios e contas em lote (RF-199).</summary>
public sealed class LeitorDeRegistros(INomesDaAuditoria nomes)
{
    /// <summary>Os registros lidos, na mesma ordem.</summary>
    public async Task<IReadOnlyList<RegistroLido>> Ler(IReadOnlyList<RegistroBruto> brutos, CancellationToken ct = default)
    {
        if (brutos.Count == 0) return [];
        var usuarios = await nomes.Usuarios(brutos.Select(b => b.QuemId).OfType<string>().Distinct(), ct);
        var contas = await nomes.Contas(brutos.Select(b => b.ContaId).OfType<string>().Distinct(), ct);
        return [.. brutos.Select(b => new RegistroLido(
            b.Id, b.Tipo, b.TraceId, b.Etapa, b.Nivel, b.Codigo, b.Acao, CatalogoDeAcoes.Nome(b.Acao), CatalogoDeAcoes.Modulo(b.Acao),
            b.QuemId is { } q ? new PessoaLida(q, usuarios.GetValueOrDefault(q, q)) : null,
            b.ContaId is { } c ? new ContaLida(c, contas.GetValueOrDefault(c, c)) : null,
            b.Alvo, b.Quando))];
    }
}

/// <summary>
/// GET /auditoria/registros (RF-199, RF-200, RN-321): auditoria e auditoriaErros juntos, o mais novo primeiro, 50 por
/// página. O traceId traz todos os registros da execução, sem período. Sem o banco, 503 BANCO_DE_DADOS_FORA (RNF-252).
/// </summary>
public sealed class ConsultarRegistros(
    ILeituraDaAuditoria leitura, INomesDaAuditoria nomes, LeitorDeRegistros leitor, IUltimaVerificacao ultima, IRegistrarConsulta registrar,
    TimeProvider relogio) : IConsultarRegistros
{
    /// <inheritdoc />
    public async Task<RegistrosDto> Executar(PedidoDeRegistros pedido, ConsultaPedida consulta, CancellationToken ct = default)
    {
        var filtro = FiltroDosRegistros.Ler(pedido.Tipo, pedido.Nivel, pedido.Modulo, pedido.Acao, pedido.Conta, pedido.Quem, pedido.Codigo, pedido.TraceId);
        var de = Periodo.Ler(pedido.De, "de");
        var ate = Periodo.Ler(pedido.Ate, "ate");
        var depois = Cursor.Ler(pedido.Depois);
        var limite = Cursor.LerLimite(pedido.Limite);
        Periodo? periodo = filtro.TraceId is null ? Periodo.Criar(de, ate, Periodo.MaximoDeDias, relogio.GetUtcNow()) : null;
        if (ultima.BancoForaDoAr) throw FalhaDaAuditoria.BancoDeDadosFora();

        var acoes = filtro.Acoes();
        var contaId = filtro.Conta is null ? null : await nomes.IdDaConta(filtro.Conta, ct);
        var nenhum = acoes is { Count: 0 } || (filtro.Conta is not null && contaId is null);
        IReadOnlyList<RegistroBruto> brutos = nenhum
            ? []
            : await leitura.Registros(new ConsultaDeRegistros(periodo, filtro, acoes, contaId, depois, limite), ct);
        var pagina = brutos.Take(limite).ToList();
        var proximo = brutos.Count > limite ? new Cursor(pagina[^1].Quando, pagina[^1].Id).Texto : null;
        var itens = await leitor.Ler(pagina, ct);

        await registrar.Executar(consulta, ct);
        return new RegistrosDto(periodo?.De, periodo?.Ate, [.. itens.Select(RegistroDto.De)], proximo);
    }
}

/// <summary>GET /auditoria/registros/ao-vivo (RF-201, RNF-249): cada registro novo, pelos mesmos filtros, e as ocorrências.</summary>
public sealed class AcompanharRegistrosAoVivo(
    IAvisosDaAuditoria avisos, IAvisosDeOcorrencias avisosDeOcorrencias, LeitorDeRegistros leitor, IRegistrarConsulta registrar, AoVivoDaAuditoria aoVivo)
    : IAcompanharRegistrosAoVivo
{
    /// <inheritdoc />
    public async Task<AoVivoAberto> Abrir(PedidoDeRegistros pedido, ConsultaPedida consulta, CancellationToken ct = default)
    {
        // o ao vivo não tem período nem página: o que não existe nele é inválido
        var (campo, valor) = new[] { ("de", pedido.De), ("ate", pedido.Ate), ("traceId", pedido.TraceId), ("depois", pedido.Depois), ("limite", pedido.Limite) }
            .FirstOrDefault(p => p.Item2 is not null);
        if (valor is not null) throw FalhaDaAuditoria.DadosInvalidos(campo);
        var filtro = FiltroDosRegistros.Ler(pedido.Tipo, pedido.Nivel, pedido.Modulo, pedido.Acao, pedido.Conta, pedido.Quem, pedido.Codigo, null);

        var registros = await avisos.Assinar(ct);
        IAssinatura<Ocorrencias.Domain.Ocorrencia> ocorrencias;
        try
        {
            ocorrencias = await avisosDeOcorrencias.Assinar(ct);
        }
        catch
        {
            await registros.DisposeAsync();
            throw;
        }
        await registrar.Executar(consulta, ct);
        return await aoVivo.Abrir(consulta.Quem, [],
            [c => Novos(registros, filtro, c), c => FonteDeOcorrencias.Eventos(ocorrencias, c)], [registros, ocorrencias], ct);
    }

    async IAsyncEnumerable<EventoAoVivo> Novos(IAssinatura<RegistroBruto> assinatura, FiltroDosRegistros filtro, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var bruto in assinatura.Avisos(ct))
        {
            var lido = (await leitor.Ler([bruto], ct))[0];
            if (filtro.Aceita(lido)) yield return new EventoAoVivo("registro", lido.Id, RegistroDto.De(lido));
        }
    }
}

/// <summary>GET /auditoria/acoes (RN-319): o catálogo, para os filtros e os nomes.</summary>
public sealed class ListarAcoes : IListarAcoes
{
    /// <inheritdoc />
    public IReadOnlyList<AcaoDto> Executar() => [.. CatalogoDeAcoes.Todas.Select(AcaoDto.De)];
}
