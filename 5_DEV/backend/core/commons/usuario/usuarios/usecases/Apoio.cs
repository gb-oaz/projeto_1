using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// O que os casos de uso de Usuários repetem: achar quem pede, o alvo na mesma
/// conta, a cadeia de quem criou, ids e códigos únicos e o registro de auditoria.
/// </summary>
sealed class Apoio(IUsuarios usuarios, IContas contas, IAuditoria auditoria, TimeProvider relogio)
{
    /// <summary>Até onde a cadeia de quem criou é seguida.</summary>
    const int MaiorCadeia = 32;

    /// <summary>Tentativas de sortear um código ou matrícula livre.</summary>
    const int Sorteios = 20;

    /// <summary>A hora de agora, em UTC.</summary>
    public DateTimeOffset Agora => relogio.GetUtcNow();

    /// <summary>Quem pede, pelo sub do token; sem cadastro, não pode nada.</summary>
    public async Task<UsuarioCadastrado> Quem(string id, CancellationToken ct) =>
        await usuarios.Buscar(id, ct) ?? throw FalhaDeUsuarios.AcessoNegado();

    /// <summary>Quem pede, desde que seja do time deboaz.</summary>
    public async Task<UsuarioCadastrado> DoTime(string id, CancellationToken ct)
    {
        var quem = await Quem(id, ct);
        return quem.Tipo.DoTime() ? quem : throw FalhaDeUsuarios.AcessoNegado();
    }

    /// <summary>A conta de quem pede.</summary>
    public async Task<ContaAssinante> ContaDe(UsuarioCadastrado quem, CancellationToken ct) =>
        await contas.Buscar(quem.ContaId, ct) ?? throw FalhaDeUsuarios.ContaInexistente();

    /// <summary>Uma conta assinante; a do time deboaz não aparece para ninguém.</summary>
    public async Task<ContaAssinante> Assinante(string contaId, CancellationToken ct) =>
        await contas.Buscar(contaId, ct) is { } conta && conta.Sigla != Sigla.DoTime ? conta : throw FalhaDeUsuarios.ContaInexistente();

    /// <summary>O alvo do pedido; de outra conta é como se não existisse.</summary>
    public async Task<UsuarioCadastrado> Alvo(UsuarioCadastrado quem, string alvoId, CancellationToken ct) =>
        await usuarios.Buscar(alvoId, ct) is { } alvo && alvo.ContaId == quem.ContaId ? alvo : throw FalhaDeUsuarios.UsuarioInexistente();

    /// <summary>O alvo, se quem pede está acima dele (RN-164, RN-169).</summary>
    public async Task<UsuarioCadastrado> Administravel(UsuarioCadastrado quem, string alvoId, CancellationToken ct)
    {
        var alvo = await Alvo(quem, alvoId, ct);
        return Hierarquia.PodeAdministrar(quem, alvo, await Acima(alvo, ct)) ? alvo : throw FalhaDeUsuarios.AcessoNegado();
    }

    /// <summary>Quem criou o alvo, quem criou esse, e assim por diante.</summary>
    public async Task<IReadOnlyList<string>> Acima(UsuarioCadastrado alvo, CancellationToken ct)
    {
        var acima = new List<string>();
        for (var id = alvo.CriadoPor; id is not null && acima.Count < MaiorCadeia && !acima.Contains(id);)
        {
            acima.Add(id);
            id = (await usuarios.Buscar(id, ct))?.CriadoPor;
        }
        return acima;
    }

    /// <summary>usr-&lt;sigla&gt;-&lt;nome curto&gt;, com um número no fim se já existir.</summary>
    public async Task<string> NovoId(string sigla, string nomeCurto, CancellationToken ct)
    {
        var baseDoId = $"usr-{sigla.ToLowerInvariant()}-{nomeCurto}";
        var id = baseDoId;
        for (var n = 2; await usuarios.Buscar(id, ct) is not null; n++) id = $"{baseDoId}-{n}";
        return id;
    }

    /// <summary>Um código de acesso que ninguém usa (RN-184).</summary>
    public async Task<string> NovoCodigo(string sigla, string nomeCurto, CancellationToken ct)
    {
        for (var i = 0; i < Sorteios; i++)
        {
            var codigo = Logins.CodigoDeAcesso(sigla, nomeCurto);
            if (!await usuarios.LoginEmUso(codigo, ct)) return codigo;
        }
        throw new InvalidOperationException($"Nenhum código de acesso livre para {sigla}-{nomeCurto}.");
    }

    /// <summary>Uma matrícula de 8 números que a conta não usa (RN-188).</summary>
    public async Task<string> NovaMatricula(string contaId, CancellationToken ct)
    {
        for (var i = 0; i < Sorteios; i++)
        {
            var matricula = Matricula.Gerar();
            if (!await usuarios.MatriculaEmUso(contaId, matricula, null, ct)) return matricula;
        }
        throw new InvalidOperationException($"Nenhuma matrícula livre na conta {contaId}.");
    }

    /// <summary>
    /// Guarda a ação na auditoria (RN-178), por ora num só registro "CONCLUIDO"
    /// com um traceId próprio: o "INICIADO", o registro de erro e o traceId do
    /// endereço (RN-270, RN-271) chegam caso a caso, como no CriarConta.
    /// </summary>
    public Task Auditar(string contaId, string quem, AcaoAuditada acao, string alvo, CancellationToken ct) =>
        auditoria.Registrar(new RegistroDeAuditoria(TraceId.Novo(), EtapaDaExecucao.Concluido, contaId, quem, acao, alvo, Agora), ct);
}
