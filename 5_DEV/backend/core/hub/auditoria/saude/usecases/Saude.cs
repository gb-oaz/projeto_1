using System.Globalization;
using System.Runtime.CompilerServices;
using Boaz.Core.Hub.Auditoria.Card.Domain;
using Boaz.Core.Hub.Auditoria.Card.Dtos;
using Boaz.Core.Hub.Auditoria.Card.UseCases;
using Boaz.Core.Hub.Auditoria.Metricas.Domain;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Ports.Out;
using Boaz.Core.Hub.Auditoria.Ocorrencias.UseCases;
using Boaz.Core.Hub.Auditoria.Registros.Ports.In;
using Boaz.Core.Hub.Auditoria.Saude.Domain;
using Boaz.Core.Hub.Auditoria.Saude.Dtos;
using Boaz.Core.Hub.Auditoria.Saude.Ports.In;
using Boaz.Core.Hub.Auditoria.Saude.Ports.Out;

namespace Boaz.Core.Hub.Auditoria.Saude.UseCases;

/// <summary>
/// Quem tem a aba Saúde aberta ao vivo nesta instância. Em homologação com o relógio de teste, o AgendamentoDaAuditoria só
/// verifica a cada 30 s enquanto alguém acompanha (os ganchos de teste verificam na hora, e uma verificação a mais no
/// meio de um cenário o desfaria).
/// </summary>
public sealed class ObservadoresDaSaude
{
    int quantos;

    /// <summary>Quantas conexões ao vivo da Saúde estão abertas.</summary>
    public int Quantos => Volatile.Read(ref quantos);

    /// <summary>Uma conexão abriu; descartar o que volta a fecha.</summary>
    public IAsyncDisposable Entrar()
    {
        Interlocked.Increment(ref quantos);
        return new Marca(this);
    }

    sealed class Marca(ObservadoresDaSaude dono) : IAsyncDisposable
    {
        int fechada;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref fechada, 1) == 0) Interlocked.Decrement(ref dono.quantos);
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>A saúde na última verificação desta instância, com as instâncias no ar (RF-194): sem o banco (RNF-252).</summary>
public sealed class LeitorDaSaude(IUltimaVerificacao ultima, IVerificarServicos verificar, IInstancias instancias, IdentidadeDaInstancia eu)
{
    /// <summary>
    /// A última verificação; a primeira vez, que ainda não houve, verifica na hora. Esta instância sempre consta, e o
    /// cache fora do ar deixa só ela na lista.
    /// </summary>
    public async Task<SaudeDoSistema> Ler(CancellationToken ct = default)
    {
        if (ultima.VerificadoEm is not { } em) return await verificar.Executar(false, ct);
        var lidas = await instancias.Listar(ct).ContinuaSeFalhar(eu, em);
        return new SaudeDoSistema(eu.Ambiente, em, ultima.Servicos, ultima.Modulos, lidas);
    }
}

/// <summary>GET /auditoria/saude (RF-194, RF-196, RNF-252): lida da última verificação, sem tocar no banco; a consulta só fica gravada se ele responde.</summary>
public sealed class VerSaude(LeitorDaSaude leitor, IRegistrarConsulta registrar) : IVerSaude
{
    /// <inheritdoc />
    public async Task<SaudeDto> Executar(ConsultaPedida consulta, CancellationToken ct = default)
    {
        var saude = await leitor.Ler(ct);
        await registrar.Executar(consulta, ct);
        return SaudeDto.De(saude);
    }
}

/// <summary>
/// GET /auditoria/saude/{servico}/historico (RF-195): as quedas, a disponibilidade e a série de 24 h (por hora), 7 ou 30 dias
/// (por dia de Brasília). Sem o banco, 503.
/// </summary>
public sealed class VerHistoricoDoServico(IHistoricoDaSaude historico, IUltimaVerificacao ultima, IRegistrarConsulta registrar, TimeProvider relogio)
    : IVerHistoricoDoServico
{
    /// <inheritdoc />
    public async Task<HistoricoDto> Executar(string servico, string? periodo, ConsultaPedida consulta, CancellationToken ct = default)
    {
        var s = Servico.DeId(servico);
        var texto = periodo ?? "24h";
        var (duracao, pontos) = texto switch
        {
            "24h" => (TimeSpan.FromHours(24), 24),
            "7d" => (TimeSpan.FromDays(7), 7),
            "30d" => (TimeSpan.FromDays(30), 30),
            _ => throw FalhaDaAuditoria.DadosInvalidos("periodo"),
        };
        if (ultima.BancoForaDoAr) throw FalhaDaAuditoria.BancoDeDadosFora();

        var agora = relogio.GetUtcNow();
        var (de, ate) = (agora - duracao, agora);
        var quedas = await historico.Quedas(s, de, ate, ct);
        var (inicio, passo) = texto == "24h"
            ? (Janelas.Hora(agora).AddHours(-(pontos - 1)), TimeSpan.FromHours(1))
            : (Janelas.InicioDoDia(Commons.Usuario.Usuarios.Domain.CalendarioDoCard.Dia(agora).AddDays(-(pontos - 1))), TimeSpan.FromDays(1));
        var serie = await historico.Serie(s, inicio, passo, pontos, ct);

        await registrar.Executar(consulta, ct);
        return new HistoricoDto(s.Id, s.Nome, texto, de, ate, Queda.Disponibilidade(quedas, de, ate),
            [.. quedas.Select(q => new QuedaDto(q.Inicio, q.Fim, q.DuracaoMin(agora), q.Falha.Texto()))],
            [.. serie.Select(p => new PontoDaSerieDto(p.Inicio, p.TempoMedioMs, p.Falhas))]);
    }
}

/// <summary>
/// GET /auditoria/saude/ao-vivo (RNF-246, RNF-249): a saúde ao abrir e a cada verificação nova (a cada 30 s), mais cada ocorrência
/// em até 5 s, na mesma conexão.
/// </summary>
public sealed class AcompanharSaudeAoVivo(
    LeitorDaSaude leitor, IUltimaVerificacao ultima, IAvisosDeOcorrencias avisos, IRegistrarConsulta registrar, ObservadoresDaSaude observadores,
    AoVivoDaAuditoria aoVivo, TimeProvider relogio) : IAcompanharSaudeAoVivo
{
    static readonly TimeSpan Espera = TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    public async Task<AoVivoAberto> Abrir(ConsultaPedida consulta, CancellationToken ct = default)
    {
        var observador = observadores.Entrar();
        IAssinatura<Ocorrencias.Domain.Ocorrencia>? assinatura = null;
        try
        {
            assinatura = await avisos.Assinar(ct);
            var saude = await leitor.Ler(ct);
            await registrar.Executar(consulta, ct);
            var primeiro = new EventoAoVivo("saude", Id(saude.VerificadoEm), SaudeDto.De(saude));
            var ocorrencias = assinatura;
            return await aoVivo.Abrir(consulta.Quem, [primeiro],
                [c => Verificacoes(saude.VerificadoEm, c), c => FonteDeOcorrencias.Eventos(ocorrencias, c)], [assinatura, observador], ct);
        }
        catch
        {
            if (assinatura is not null) await assinatura.DisposeAsync();
            await observador.DisposeAsync();
            throw;
        }
    }

    // a verificação nova: confere a memória a cada 1 s, para a tela mostrar em até 5 s
    async IAsyncEnumerable<EventoAoVivo> Verificacoes(DateTimeOffset desde, [EnumeratorCancellation] CancellationToken ct)
    {
        var visto = desde;
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(Espera, relogio, ct);
            // diferente, e não maior: o relógio de teste também anda para trás
            if (ultima.VerificadoEm is not { } em || em == visto) continue;
            visto = em;
            var saude = await leitor.Ler(ct);
            yield return new EventoAoVivo("saude", Id(saude.VerificadoEm), SaudeDto.De(saude));
        }
    }

    static string Id(DateTimeOffset em) => em.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
}
