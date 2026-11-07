using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

/// <summary>A coleção contas: o cadastro, o estado, o tempo de sessão e as conferências da conta master.</summary>
public interface IContas
{
    /// <summary>A conta pelo id; null se não existir.</summary>
    Task<ContaAssinante?> Buscar(string id, CancellationToken ct = default);

    /// <summary>A sigla já é de uma conta (comparada em maiúsculas).</summary>
    Task<bool> SiglaEmUso(string sigla, CancellationToken ct = default);

    /// <summary>O domínio já é de uma conta.</summary>
    Task<bool> DominioEmUso(string dominio, CancellationToken ct = default);

    /// <summary>
    /// Grava numa só operação a conta nova, a conta master, a geração da senha
    /// mostrada e o tempo de sessão padrão (RF-104).
    /// </summary>
    Task Criar(ContaAssinante conta, UsuarioCadastrado master, GeracaoDeSenhaPedida geracao, TempoDaConta tempo,
        CancellationToken ct = default);

    /// <summary>
    /// Garante uma conta inicial pelo id (RN-267): grava o cadastro e o tempo de
    /// sessão (se dado) só se ela não existir. Devolve true se ela nasceu agora.
    /// </summary>
    Task<bool> Garantir(ContaAssinante conta, TempoDaConta? tempo, CancellationToken ct = default);

    /// <summary>Grava o estado novo da conta.</summary>
    Task Atualizar(ContaAssinante conta, CancellationToken ct = default);

    /// <summary>
    /// Encerra a conta (RN-174): apaga em cascata os dados da escola e guarda só o
    /// registro mínimo para histórico, auditoria e cobrança, anonimizado em 5 anos.
    /// </summary>
    Task Encerrar(string contaId, DateTimeOffset quando, CancellationToken ct = default);

    /// <summary>Uma página das contas assinantes, de A a Z pelo nome, sem a do time (RN-185).</summary>
    Task<PaginaDe<ContaAssinante>> Listar(FiltroDeContas filtro, CancellationToken ct = default);

    /// <summary>O tempo de sessão da conta; null se nunca foi salvo (vale o padrão).</summary>
    Task<TempoDaConta?> TempoDaConta(string contaId, CancellationToken ct = default);

    /// <summary>Grava o tempo de sessão da conta.</summary>
    Task SalvarTempoDaConta(string contaId, TempoDaConta tempo, CancellationToken ct = default);

    /// <summary>O tempo de sessão dos membros e das contas master; null se nunca foi salvo.</summary>
    Task<TempoDoTime?> TempoDoTime(CancellationToken ct = default);

    /// <summary>Grava o tempo de sessão dos membros e das contas master.</summary>
    Task SalvarTempoDoTime(TempoDoTime tempo, CancellationToken ct = default);

    /// <summary>Guarda uma conferência da conta master em contas.conferencias (RN-172).</summary>
    Task RegistrarConferencia(string contaId, string quem, bool conferiu, DateTimeOffset quando, CancellationToken ct = default);
}
