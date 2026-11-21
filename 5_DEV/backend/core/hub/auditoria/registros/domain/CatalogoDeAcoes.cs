using Boaz.Core.Commons.Auditoria.Domain;

namespace Boaz.Core.Hub.Auditoria.Registros.Domain;

/// <summary>O que "quem" quer dizer quando o registro vai sem quem (RN-319).</summary>
public enum SemQuem
{
    /// <summary>A ação sem login (a pessoa ainda não entrou).</summary>
    SemLogin,
    /// <summary>O sistema: um agendamento ou um caso de uso interno.</summary>
    Sistema,
    /// <summary>O banco, em seu aviso.</summary>
    Banco,
}

/// <summary>Uma ação do catálogo: o código, o nome mostrado, o módulo e o que quer dizer a falta de quem (RN-319).</summary>
/// <param name="Acao">O código, como CRIAR_USUARIO.</param>
/// <param name="Nome">O nome mostrado, como "Criar usuário".</param>
/// <param name="Modulo">O módulo da ação, como MOD-03.</param>
/// <param name="SemQuem">O que "sem quem" quer dizer nesta ação.</param>
public sealed record AcaoDoCatalogo(string Acao, string Nome, string? Modulo, SemQuem SemQuem)
{
    /// <summary>SEM_LOGIN, SISTEMA ou BANCO.</summary>
    public string SemQuemTexto => SemQuem switch
    {
        SemQuem.SemLogin => "SEM_LOGIN",
        SemQuem.Sistema => "SISTEMA",
        _ => "BANCO",
    };
}

/// <summary>
/// O nome e o módulo de cada ação auditada (RN-319): as 41 de <see cref="AcaoAuditada"/>. Uma ação fora do
/// catálogo (de um módulo novo) é mostrada pelo próprio código, sem módulo.
/// </summary>
public static class CatalogoDeAcoes
{
    const string Usuarios = "MOD-03", Sac = "MOD-02", Pagamento = "MOD-01", Auditoria = "MOD-17";

    static readonly Dictionary<string, AcaoDoCatalogo> PorCodigo = Montar();

    /// <summary>As 41 ações, na ordem de <see cref="AcaoAuditada"/>.</summary>
    public static IReadOnlyList<AcaoDoCatalogo> Todas { get; } = [.. PorCodigo.Values];

    /// <summary>O nome da ação; o próprio código se ela não está no catálogo.</summary>
    public static string Nome(string acao) => PorCodigo.TryGetValue(acao, out var a) ? a.Nome : acao;

    /// <summary>O módulo da ação; null se ela não está no catálogo.</summary>
    public static string? Modulo(string acao) => PorCodigo.TryGetValue(acao, out var a) ? a.Modulo : null;

    /// <summary>A ação está no catálogo.</summary>
    public static bool Existe(string? acao) => acao is not null && PorCodigo.ContainsKey(acao);

    /// <summary>Os códigos das ações de um módulo.</summary>
    public static IReadOnlyList<string> DoModulo(string modulo) => [.. PorCodigo.Values.Where(a => a.Modulo == modulo).Select(a => a.Acao)];

    static Dictionary<string, AcaoDoCatalogo> Montar()
    {
        var nomes = new Dictionary<AcaoAuditada, (string Nome, string Modulo, SemQuem Sem)>
        {
            [AcaoAuditada.CriarConta] = ("Criar conta", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.CriarUsuario] = ("Criar usuário", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.EditarUsuario] = ("Editar usuário", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.NovaGeracaoSenha] = ("Nova geração de senha", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.GerarSenha] = ("Gerar senha", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.NovoCodigoAcesso] = ("Novo código de acesso", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.DesativarUsuario] = ("Desativar usuário", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.ReativarUsuario] = ("Reativar usuário", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.MudarEstadoConta] = ("Mudar estado da conta", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.CodigoMaster] = ("Código da conta master", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.SalvarTempoSessao] = ("Salvar tempo de sessão", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.EditarPermissoes] = ("Editar permissões", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.EntrarHub] = ("Entrar no hub", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.EntrarQuest] = ("Entrar no quest", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.EntrarSala] = ("Entrar na sala", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.ConfirmarSenha] = ("Confirmar senha", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.RenovarSessao] = ("Renovar sessão", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.Sair] = ("Sair", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.EncerrarSessao] = ("Encerrar sessão", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.EncerrarSessoes] = ("Encerrar sessões", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.EsqueciSenhaMaster] = ("Esqueci a senha da master", Usuarios, SemQuem.SemLogin),
            [AcaoAuditada.EncerrarSessoesDoUsuario] = ("Encerrar sessões do usuário", Usuarios, SemQuem.Sistema),
            [AcaoAuditada.AvisarPermissoesNovas] = ("Avisar permissões novas", Usuarios, SemQuem.Sistema),
            [AcaoAuditada.AvisarContratacaoMudou] = ("Avisar mudança na contratação", Usuarios, SemQuem.Sistema),
            [AcaoAuditada.AbrirChamado] = ("Abrir chamado", Sac, SemQuem.SemLogin),
            [AcaoAuditada.EnviarMensagem] = ("Enviar mensagem", Sac, SemQuem.SemLogin),
            [AcaoAuditada.AssumirChamado] = ("Assumir chamado", Sac, SemQuem.SemLogin),
            [AcaoAuditada.MudarStatus] = ("Mudar status do chamado", Sac, SemQuem.SemLogin),
            [AcaoAuditada.ReabrirChamado] = ("Reabrir chamado", Sac, SemQuem.SemLogin),
            [AcaoAuditada.EncerrarChamado] = ("Encerrar chamado", Sac, SemQuem.SemLogin),
            [AcaoAuditada.AvaliarChamado] = ("Avaliar chamado", Sac, SemQuem.SemLogin),
            [AcaoAuditada.SalvarPrazosSac] = ("Salvar prazos do SAC", Sac, SemQuem.SemLogin),
            [AcaoAuditada.FecharPorPrazo] = ("Fechar por prazo", Sac, SemQuem.Sistema),
            [AcaoAuditada.PagamentoCriado] = ("Criar pagamento", Pagamento, SemQuem.SemLogin),
            [AcaoAuditada.PagamentoCancelado] = ("Cancelar pagamento", Pagamento, SemQuem.SemLogin),
            [AcaoAuditada.FaturaCancelada] = ("Cancelar fatura", Pagamento, SemQuem.SemLogin),
            [AcaoAuditada.EstornoPedido] = ("Pedir estorno", Pagamento, SemQuem.SemLogin),
            [AcaoAuditada.ConfiguracaoSalva] = ("Salvar configuração do Pagamento", Pagamento, SemQuem.SemLogin),
            [AcaoAuditada.AvisoDoBanco] = ("Aviso do banco", Pagamento, SemQuem.Banco),
            [AcaoAuditada.FaturaVencida] = ("Fatura vencida", Pagamento, SemQuem.Sistema),
            [AcaoAuditada.ConsultarAuditoria] = ("Consultar a Auditoria", Auditoria, SemQuem.SemLogin),
        };
        return Enum.GetValues<AcaoAuditada>().ToDictionary(a => a.Texto(),
            a => new AcaoDoCatalogo(a.Texto(), nomes[a].Nome, nomes[a].Modulo, nomes[a].Sem));
    }
}
