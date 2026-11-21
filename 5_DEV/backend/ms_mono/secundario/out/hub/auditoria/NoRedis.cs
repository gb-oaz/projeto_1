using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using Boaz.Core.Hub.Auditoria.Card.Ports.Out;
using Boaz.Core.Hub.Auditoria.Registros.Ports.Out;
using Boaz.Core.Hub.Auditoria.Saude.Domain;
using Boaz.Core.Hub.Auditoria.Saude.Ports.Out;
using Boaz.MsMono.Secundario.Out.Commons;
using StackExchange.Redis;
using ISessoes = Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out.ISessoes;

namespace Boaz.MsMono.Secundario.Out.Hub.Auditoria;

// A última verificação desta instância, só na memória (auditoria-classes v03): a saúde responde sem o banco (RNF-252) e a
// verificação seguinte parte do estado dela (falhas seguidas, "desde").
public sealed class UltimaVerificacaoEmMemoria : IUltimaVerificacao
{
    sealed record Estado(DateTimeOffset Em, IReadOnlyList<Verificacao> Servicos, IReadOnlyList<SaudeDoModulo> Modulos);

    volatile Estado? atual;

    public DateTimeOffset? VerificadoEm => atual?.Em;

    public IReadOnlyList<Verificacao> Servicos => atual?.Servicos ?? [];

    public IReadOnlyList<SaudeDoModulo> Modulos => atual?.Modulos ?? [];

    public bool BancoForaDoAr => De(Servico.Banco)?.Estado == EstadoDoServico.ForaDoAr;

    public Verificacao? De(Servico servico) => atual?.Servicos.FirstOrDefault(v => v.Servico == servico);

    public SaudeDoModulo? DoModulo(string modulo) => atual?.Modulos.FirstOrDefault(m => m.Modulo == modulo);

    public void Guardar(DateTimeOffset em, IReadOnlyList<Verificacao> servicos, IReadOnlyList<SaudeDoModulo> modulos) =>
        atual = new Estado(em, servicos, modulos);
}

// As instâncias no ar: o hash auditoria:instancia:<id> (versao, inicio, ultimoSinal) com TTL de 90 s, renovado a cada
// verificação (auditoria-documentos v02).
public sealed class InstanciasNoRedis(Armazenamento armazenamento) : IInstancias
{
    const string Prefixo = "auditoria:instancia:";
    static readonly TimeSpan Validade = TimeSpan.FromSeconds(90);

    public async Task Anunciar(Instancia instancia, CancellationToken ct = default)
    {
        RedisKey chave = Prefixo + instancia.Id;
        await armazenamento.Cache.HashSetAsync(chave,
        [
            new HashEntry("versao", instancia.Versao),
            new HashEntry("inicio", instancia.Inicio.ToUnixTimeMilliseconds()),
            new HashEntry("ultimoSinal", instancia.UltimoSinal.ToUnixTimeMilliseconds()),
        ]);
        await armazenamento.Cache.KeyExpireAsync(chave, Validade);
    }

    public async Task<IReadOnlyList<Instancia>> Listar(CancellationToken ct = default)
    {
        var lidas = new List<Instancia>();
        await foreach (var chave in armazenamento.Chaves(Prefixo + "*"))
        {
            var campos = (await armazenamento.Cache.HashGetAllAsync(chave)).ToDictionary(e => e.Name.ToString(), e => e.Value);
            if (!campos.TryGetValue("inicio", out var inicio) || !campos.TryGetValue("ultimoSinal", out var sinal)) continue;
            lidas.Add(new Instancia(chave.ToString()[Prefixo.Length..], campos.GetValueOrDefault("versao").ToString(),
                DateTimeOffset.FromUnixTimeMilliseconds((long)inicio), DateTimeOffset.FromUnixTimeMilliseconds((long)sinal)));
        }
        return lidas;
    }
}

// A trava entre as instâncias (auditoria:agendamento, SET NX PX): a do minuto (card e descarte) é a do diagrama, 55 s; a
// verificação de 30 s tem a dela, auditoria:agendamento:verificacao, porque a de 55 s não deixaria verificar a cada 30 s.
public sealed class TravaDaAuditoriaNoRedis(Armazenamento armazenamento) : ITravaDaAuditoria
{
    public const string DoMinuto = "minuto";

    public Task<bool> Tentar(string tarefa, TimeSpan prazo, CancellationToken ct = default) =>
        armazenamento.Cache.StringSetAsync(tarefa == DoMinuto ? "auditoria:agendamento" : $"auditoria:agendamento:{tarefa}", Environment.MachineName, prazo,
            When.NotExists);
}

// A consulta que acabou de ser registrada (auditoria:consulta:<usuarioId>:<hash do alvo>): guarda a hora do relógio da
// aplicação, para a janela de 5 min valer também com o relógio de teste, que anda de outro jeito; o TTL do Redis só limpa.
public sealed class ConsultasRecentesNoRedis(Armazenamento armazenamento) : IConsultasRecentes
{
    static readonly TimeSpan Limpeza = TimeSpan.FromMinutes(5);

    public async Task<bool> Primeira(string quem, string alvo, DateTimeOffset agora, TimeSpan janela, CancellationToken ct = default)
    {
        RedisKey chave = $"auditoria:consulta:{quem}:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(alvo)))[..16]}";
        // em segundos inteiros, como a hora do pedido: 13:45:00 depois de 13:40:00 já é uma consulta nova
        var segundo = agora.ToUnixTimeSeconds();
        var guardada = await armazenamento.Cache.StringGetAsync(chave);
        if (guardada.HasValue && (long)guardada is var antes && segundo >= antes && segundo - antes < (long)janela.TotalSeconds) return false;
        await armazenamento.Cache.StringSetAsync(chave, segundo.ToString(CultureInfo.InvariantCulture), Limpeza);
        return true;
    }
}

// A última rodada dos agendamentos (auditoria:ultimaExecucao:<agendamento>, TTL de 1 dia), gravada pelo SAC e pelo Pagamento
// a cada rodada e lida pela sonda da Auditoria (RN-315).
public sealed class UltimaExecucaoNoRedis(Armazenamento armazenamento) : IUltimaExecucaoDosAgendamentos
{
    static readonly TimeSpan Validade = TimeSpan.FromDays(1);

    static RedisKey Chave(string agendamento) => $"auditoria:ultimaExecucao:{agendamento}";

    public Task Gravar(string agendamento, DateTimeOffset quando, CancellationToken ct = default) =>
        armazenamento.Cache.StringSetAsync(Chave(agendamento), quando.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture), Validade);

    public async Task<DateTimeOffset?> Ler(string agendamento, CancellationToken ct = default)
    {
        var guardada = await armazenamento.Cache.StringGetAsync(Chave(agendamento));
        return guardada.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds((long)guardada) : null;
    }
}

// O que o ao vivo da Auditoria confere a cada 5 s: a sessão do token (sessao:<sid>, da Autenticação) e o MOD-17 · U, ou o
// papel ADMINISTRADOR, pelas permissões de agora (da Autorização), como o [ComAcesso] da rota na abertura.
public sealed class AcessoDoAoVivoDaAuditoria(ISessoes sessoes, ICalcularPermissoes calcular, IAutorizarPedido autorizar, TimeProvider relogio)
    : IAcessoDoAoVivo
{
    static readonly ExigenciaDaRota Exigencia = new("MOD-17", Abas.U);

    public async Task<bool> SessaoAberta(string sid, CancellationToken ct = default) =>
        await sessoes.Buscar(sid, ct) is { } sessao && sessao.EstaAtiva(relogio.GetUtcNow());

    public async Task<bool> PodeUsar(string usuarioId, CancellationToken ct = default)
    {
        try
        {
            var permissoes = await calcular.Executar(usuarioId, ct);
            autorizar.Executar(Exigencia, new PedidoAutorizavelDto(permissoes.Papel, permissoes.Mods));
            return true;
        }
        catch (FalhaDeAutorizacao)
        {
            return false;
        }
    }
}
