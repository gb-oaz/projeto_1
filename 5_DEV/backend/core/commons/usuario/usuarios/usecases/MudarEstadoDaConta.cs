using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// POST /contas/{id}/estado (RF-113, RN-173 a RN-175): o time deboaz muda o
/// estado de qualquer conta; a conta master e o autônomo só desativam a própria
/// conta (o resto, 403). Fora de ATIVA, as sessões da conta terminam; a fatura
/// em aberto segue devida no Pagamento, e a conta com fatura em aberto não se
/// encerra (409 FATURA_EM_ABERTO, 19 RN-313); ENCERRADA apaga os dados e não
/// volta. Auditado com o
/// traceId recebido: "INICIADO" com quem e a conta pedida (contaId e alvo),
/// "CONCLUIDO" antes de responder e, na recusa ou na falha, o registro de erro
/// antes de a exceção sair (RN-178, RN-270, RN-271).
/// </summary>
public sealed class MudarEstadoDaConta(
    IUsuarios usuarios, IContas contas, ISessoesDosUsuarios sessoes, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio,
    IAtualizarCardUsuarios cardsDoModulo, IContaTemFaturaEmAberto faturasEmAberto)
    : IMudarEstadoDaConta
{
    readonly Apoio apoio = new(usuarios, contas, relogio);
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task<ContaComEstadoDto> Executar(EstadoDaContaDto pedido, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = new Execucao(traceId, AcaoAuditada.MudarEstadoConta, pedido.ContaId, pedido.QuemPede, pedido.ContaId);
        return auditada.Auditar(execucao, () => Mudar(pedido, execucao, ct), ct);
    }

    /// <summary>A regra do estado da conta, de quem pede ao "CONCLUIDO".</summary>
    async Task<ContaComEstadoDto> Mudar(EstadoDaContaDto pedido, Execucao execucao, CancellationToken ct)
    {
        var (conta, novo) = await ContaEEstadoPedidos(pedido, ct);
        if (novo == EstadoConta.Encerrada && conta.Estado != EstadoConta.Encerrada && await faturasEmAberto.Executar(conta.Id, ct))
        {
            throw FalhaDeUsuarios.FaturaEmAberto();
        }
        if (conta.MudarEstado(novo))
        {
            await Gravar(conta, novo, execucao, ct);
            await cardsDoModulo.Executar(new MudancaNoCadastroDto(conta.Id), ct);
        }
        await auditada.Concluir(execucao, ct);
        return new(conta.Id, conta.Estado.Texto());
    }

    /// <summary>
    /// A conta assinante e o estado pedido (RN-173): o time deboaz pede qualquer
    /// estado; a conta master e o autônomo só "DESATIVADA" da própria conta.
    /// </summary>
    /// <exception cref="FalhaDeUsuarios">ACESSO_NEGADO, DADOS_INVALIDOS ou CONTA_INEXISTENTE.</exception>
    async Task<(ContaAssinante Conta, EstadoConta Novo)> ContaEEstadoPedidos(EstadoDaContaDto pedido, CancellationToken ct)
    {
        var quem = await apoio.Quem(pedido.QuemPede, ct);
        var daPropria = quem.Tipo == TipoUsuario.Master && quem.ContaId == pedido.ContaId;
        if (!quem.Tipo.DoTime() && !daPropria) throw FalhaDeUsuarios.AcessoNegado();
        var novo = pedido.Validar();
        if (!quem.Tipo.DoTime() && novo != EstadoConta.Desativada) throw FalhaDeUsuarios.AcessoNegado();
        return (await apoio.Assinante(pedido.ContaId, ct), novo);
    }

    /// <summary>Grava o estado novo; fora de ATIVA as sessões terminam e ENCERRADA apaga os dados (RN-173 a RN-175).</summary>
    async Task Gravar(ContaAssinante conta, EstadoConta novo, Execucao execucao, CancellationToken ct)
    {
        await contas.Atualizar(conta, ct);
        if (novo != EstadoConta.Ativa) await sessoes.EncerrarDaConta(conta.Id, MotivoFim.ContaIndisponivel, execucao.Chamada, ct);
        if (novo == EstadoConta.Encerrada) await contas.Encerrar(conta.Id, apoio.Agora, ct);
    }
}
