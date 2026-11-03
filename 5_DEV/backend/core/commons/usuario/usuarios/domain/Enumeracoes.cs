using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Domain;

// Tipos, estados e motivos de fim de sessão são os mesmos da autenticação
// (TipoUsuario, EstadoUsuario, EstadoConta, MotivoFim): Usuários grava, a
// autenticação lê. Aqui ficam só os que são de Usuários.

/// <summary>Tipo da conta assinante (RN-242).</summary>
public enum TipoConta
{
    /// <summary>Escola ou empresa, com CNPJ e staff.</summary>
    Instituicao,
    /// <summary>Educador autônomo, com CPF e só participantes (RN-165).</summary>
    Autonomo,
}

/// <summary>Ação registrada na auditoria (RN-178).</summary>
public enum AcaoAuditada
{
    /// <summary>POST /contas.</summary>
    CriarConta,
    /// <summary>POST /usuarios.</summary>
    CriarUsuario,
    /// <summary>PATCH /usuarios/{id}.</summary>
    EditarUsuario,
    /// <summary>POST /usuarios/{id}/nova-geracao-senha.</summary>
    NovaGeracaoSenha,
    /// <summary>POST /usuarios/gerar-senha: a senha foi mostrada.</summary>
    GerarSenha,
    /// <summary>POST /usuarios/{id}/novo-codigo-acesso.</summary>
    NovoCodigoAcesso,
    /// <summary>POST /usuarios/{id}/desativar.</summary>
    DesativarUsuario,
    /// <summary>POST /usuarios/{id}/reativar.</summary>
    ReativarUsuario,
    /// <summary>POST /contas/{id}/estado.</summary>
    MudarEstadoConta,
    /// <summary>POST /contas/{id}/codigo-master.</summary>
    CodigoMaster,
    /// <summary>PUT /contas/{id}/tempo-sessao ou PUT /time/tempo-sessao.</summary>
    SalvarTempoSessao,
}

/// <summary>Os textos do contrato de API para os tipos e estados.</summary>
public static class Textos
{
    /// <summary>O tipo pelo texto (o de <see cref="Nomes.Texto(TipoUsuario)"/>); null se não for um tipo de usuário.</summary>
    public static TipoUsuario? TipoDe(string? texto) =>
        Enum.GetValues<TipoUsuario>().Cast<TipoUsuario?>().FirstOrDefault(t => t!.Value.Texto() == texto);

    /// <summary>PENDENTE, ATIVO ou DESATIVADO.</summary>
    public static string Texto(this EstadoUsuario estado) => estado switch
    {
        EstadoUsuario.Pendente => "PENDENTE",
        EstadoUsuario.Ativo => "ATIVO",
        _ => "DESATIVADO",
    };

    /// <summary>O estado pelo texto; null se não for um estado de usuário.</summary>
    public static EstadoUsuario? EstadoDe(string? texto) =>
        Enum.GetValues<EstadoUsuario>().Cast<EstadoUsuario?>().FirstOrDefault(e => e!.Value.Texto() == texto);

    /// <summary>ATIVA, SUSPENSA, DESATIVADA ou ENCERRADA.</summary>
    public static string Texto(this EstadoConta estado) => estado switch
    {
        EstadoConta.Ativa => "ATIVA",
        EstadoConta.Suspensa => "SUSPENSA",
        EstadoConta.Desativada => "DESATIVADA",
        _ => "ENCERRADA",
    };

    /// <summary>O estado da conta pelo texto; null se não for um estado de conta.</summary>
    public static EstadoConta? EstadoDaContaDe(string? texto) =>
        Enum.GetValues<EstadoConta>().Cast<EstadoConta?>().FirstOrDefault(e => e!.Value.Texto() == texto);

    /// <summary>INSTITUICAO ou AUTONOMO.</summary>
    public static string Texto(this TipoConta tipo) => tipo == TipoConta.Instituicao ? "INSTITUICAO" : "AUTONOMO";

    /// <summary>O tipo da conta pelo texto; null se não for INSTITUICAO nem AUTONOMO.</summary>
    public static TipoConta? TipoDaContaDe(string? texto) => texto switch
    {
        "INSTITUICAO" => TipoConta.Instituicao,
        "AUTONOMO" => TipoConta.Autonomo,
        _ => null,
    };

    /// <summary>A ação em MAIÚSCULAS com sublinhado, como CRIAR_USUARIO.</summary>
    public static string Texto(this AcaoAuditada acao) => acao switch
    {
        AcaoAuditada.CriarConta => "CRIAR_CONTA",
        AcaoAuditada.CriarUsuario => "CRIAR_USUARIO",
        AcaoAuditada.EditarUsuario => "EDITAR_USUARIO",
        AcaoAuditada.NovaGeracaoSenha => "NOVA_GERACAO_SENHA",
        AcaoAuditada.GerarSenha => "GERAR_SENHA",
        AcaoAuditada.NovoCodigoAcesso => "NOVO_CODIGO_ACESSO",
        AcaoAuditada.DesativarUsuario => "DESATIVAR_USUARIO",
        AcaoAuditada.ReativarUsuario => "REATIVAR_USUARIO",
        AcaoAuditada.MudarEstadoConta => "MUDAR_ESTADO_CONTA",
        AcaoAuditada.CodigoMaster => "CODIGO_MASTER",
        _ => "SALVAR_TEMPO_SESSAO",
    };

    /// <summary>Uma duração no formato ISO 8601 do contrato (PT8H, P7D).</summary>
    public static string Iso(TimeSpan tempo) => System.Xml.XmlConvert.ToString(tempo);

    /// <summary>
    /// Uma duração ISO 8601 sem dias (PT24H, não P1D): os tempos do time e o
    /// staff no navegador são contados em horas no contrato.
    /// </summary>
    public static string IsoEmHoras(TimeSpan tempo)
    {
        var horas = (long)tempo.TotalHours;
        var resto = tempo - TimeSpan.FromHours(horas);
        return resto == TimeSpan.Zero ? $"PT{horas}H" : horas == 0 ? Iso(tempo) : $"PT{horas}H{Iso(resto)[2..]}";
    }

    /// <summary>Lê uma duração ISO 8601; null se o texto não for uma.</summary>
    public static TimeSpan? IsoDe(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        try
        {
            return System.Xml.XmlConvert.ToTimeSpan(texto.Trim());
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
