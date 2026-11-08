using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// POST /contas/{id}/codigo-master (RN-169, RN-172): o SAC confere o documento e
/// mais 2 de 3 dados; batendo, a conta master recebe uma nova geração de senha.
/// Auditado com o traceId recebido: "INICIADO" com quem e a conta pedida, sem
/// alvo; "CONCLUIDO" com o alvo, a conta master; na recusa (como
/// CONFERENCIA_INCOMPLETA) ou na falha, o registro de erro antes de a exceção
/// sair (RN-178, RN-270, RN-271).
/// </summary>
public sealed class GerarCodigoDaMaster(
    IUsuarios usuarios, IContas contas, IGeracoesDeSenha geracoes, IPagamentos pagamentos, ISessoesDosUsuarios sessoes,
    IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : IGerarCodigoDaMaster
{
    readonly Apoio apoio = new(usuarios, contas, relogio);
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task<CodigoDaMasterDto> Executar(ConferenciaDaMasterDto pedido, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = new Execucao(traceId, AcaoAuditada.CodigoMaster, pedido.ContaId, pedido.QuemPede, null);
        return auditada.Auditar(execucao, () => Gerar(pedido, execucao, ct), ct);
    }

    /// <summary>A regra do código da master, da conferência ao "CONCLUIDO".</summary>
    async Task<CodigoDaMasterDto> Gerar(ConferenciaDaMasterDto pedido, Execucao execucao, CancellationToken ct)
    {
        var conta = await ContaConferida(pedido, ct);
        var master = await MasterDa(conta, ct);
        var (geracao, codigo) = GeracaoDeSenhaPedida.Pedir(master, pedido.QuemPede, apoio.Agora);
        await Gravar(master, geracao, execucao, ct);
        await RegistrarConclusao(execucao, master, ct);
        return new(codigo, conta.Telefone, geracao.VenceEm);
    }

    /// <summary>A conta master da conta conferida.</summary>
    /// <exception cref="FalhaDeUsuarios">CONTA_INEXISTENTE, se a conta não tem master.</exception>
    async Task<UsuarioCadastrado> MasterDa(ContaAssinante conta, CancellationToken ct) =>
        await usuarios.MasterDaConta(conta.Id, ct) ?? throw FalhaDeUsuarios.ContaInexistente();

    /// <summary>Esquece a senha da master, grava a geração nova e encerra as sessões dela (RN-169).</summary>
    async Task Gravar(UsuarioCadastrado master, GeracaoDeSenhaPedida geracao, Execucao execucao, CancellationToken ct)
    {
        master.EsquecerSenha();
        await usuarios.Atualizar(master, ct);
        await geracoes.Inserir(geracao, ct);
        await sessoes.EncerrarDoUsuario(master.Id, MotivoFim.NovaSenha, execucao.Chamada, ct);
    }

    /// <summary>O "CONCLUIDO" antes de responder, com a conta master como alvo; o "INICIADO" foi sem alvo (RN-178).</summary>
    Task RegistrarConclusao(Execucao execucao, UsuarioCadastrado master, CancellationToken ct) =>
        auditada.Concluir(execucao with { Alvo = master.Id }, ct);

    /// <summary>A conta, se o cadastro confere; a conferência fica gravada, batendo ou não (RN-172).</summary>
    /// <exception cref="FalhaDeUsuarios">ACESSO_NEGADO, DADOS_INVALIDOS, CONTA_INEXISTENTE ou CONFERENCIA_INCOMPLETA.</exception>
    async Task<ContaAssinante> ContaConferida(ConferenciaDaMasterDto pedido, CancellationToken ct)
    {
        var quem = await apoio.DoTime(pedido.QuemPede, ct);
        var conferencia = pedido.Validar();
        var conta = await apoio.Assinante(pedido.ContaId, ct);
        var confere = conferencia.Confere(conta, await pagamentos.UltimoPagamento(conta.Id, ct));
        await contas.RegistrarConferencia(conta.Id, quem.Id, confere, apoio.Agora, ct);
        return confere ? conta : throw FalhaDeUsuarios.ConferenciaIncompleta();
    }
}
