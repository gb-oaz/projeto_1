namespace Boaz.Core.Commons.Usuario.Autenticacao.Domain;

/// <summary>Onde a sessão foi aberta. Hub e quest têm sessões separadas (RN-227).</summary>
public enum Frente
{
    /// <summary>O hub, pela entrada e a senha.</summary>
    Hub,
    /// <summary>O quest, pelo código de acesso do participante.</summary>
    Quest,
    /// <summary>Uma sala ao vivo, sem código de acesso (RN-231).</summary>
    Sala,
}

/// <summary>De onde a pessoa entrou: decide o tempo sem uso e onde fica a renovação (RNF-214).</summary>
public enum Aparelho
{
    /// <summary>Navegador: a renovação vai num cookie HttpOnly.</summary>
    Navegador,
    /// <summary>App: a renovação vai no corpo e fica no Keychain ou no Keystore.</summary>
    App,
}

/// <summary>O que está sendo tentado, para contar os erros seguidos (RNF-204).</summary>
public enum AlvoTentativa
{
    /// <summary>A entrada do hub com a senha.</summary>
    Entrada,
    /// <summary>O código de acesso do quest.</summary>
    CodigoAcesso,
    /// <summary>O código de liberação da geração de senha.</summary>
    CodigoLiberacao,
}

/// <summary>Por que uma sessão terminou.</summary>
public enum MotivoFim
{
    /// <summary>Passou o tempo sem uso da conta (RN-226).</summary>
    TempoSemUso,
    /// <summary>A pessoa saiu deste aparelho (RF-138) ou de um aparelho da lista (RF-157).</summary>
    Sair,
    /// <summary>A pessoa saiu de todos os aparelhos (RF-157).</summary>
    SairDeTodos,
    /// <summary>Uma sexta sessão do hub encerrou a mais antiga (RN-247).</summary>
    LimiteDoHub,
    /// <summary>O usuário foi desativado (RF-141).</summary>
    UsuarioDesativado,
    /// <summary>Uma nova geração de senha foi pedida (RNF-203).</summary>
    NovaSenha,
    /// <summary>A conta saiu de ATIVA (RN-175).</summary>
    ContaIndisponivel,
    /// <summary>Uma renovação já trocada voltou: sinal de cópia (RNF-213).</summary>
    CopiaDaRenovacao,
    /// <summary>O participante recebeu um código de acesso novo (RN-245).</summary>
    NovoCodigo,
    /// <summary>O staff mudou de nome curto e de entrada (RF-153).</summary>
    NovoNomeCurto,
    /// <summary>A sala do acesso anônimo fechou (RN-231).</summary>
    SalaFechada,
}

/// <summary>Tipo do usuário, como no cadastro de Usuários.</summary>
public enum TipoUsuario
{
    /// <summary>Conta master da instituição ou o educador autônomo.</summary>
    Master,
    /// <summary>Staff da instituição.</summary>
    Staff,
    /// <summary>Participante, que entra pelo quest.</summary>
    Participante,
    /// <summary>Membro do time deboaz.</summary>
    MembroTime,
    /// <summary>O administrador do deboaz, com a senha no Key Vault (RN-190).</summary>
    Administrador,
}

/// <summary>Estado do usuário no cadastro.</summary>
public enum EstadoUsuario
{
    /// <summary>Criado, ainda sem a senha confirmada.</summary>
    Pendente,
    /// <summary>Em uso.</summary>
    Ativo,
    /// <summary>Sem acesso, sem ter sido apagado (RF-112).</summary>
    Desativado,
}

/// <summary>Estado da conta: fora de ATIVA ninguém dela entra (RN-175).</summary>
public enum EstadoConta
{
    /// <summary>Uso normal.</summary>
    Ativa,
    /// <summary>Decisão temporária do time deboaz.</summary>
    Suspensa,
    /// <summary>Falta de pagamento.</summary>
    Desativada,
    /// <summary>Pedido do cliente; não volta.</summary>
    Encerrada,
}

/// <summary>Quem entrou no quest com o código do participante (RN-228).</summary>
public enum TipoQuemEntrou
{
    /// <summary>O próprio participante.</summary>
    Participante,
    /// <summary>Um responsável, que age como o participante.</summary>
    Responsavel,
}
