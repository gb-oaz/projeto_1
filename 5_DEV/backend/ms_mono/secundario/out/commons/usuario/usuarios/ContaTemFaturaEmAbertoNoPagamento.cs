using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using PagFaturas = Boaz.Core.Hub.Pagamento.Faturas.Ports.In;

namespace Boaz.MsMono.Secundario.Out.Commons.Usuario.Usuarios;

// A conta com fatura em aberto (a pagar ou vencida) não é encerrada (17 RN-173,
// 19 RN-313): o 17 só conhece a porta dele; a pergunta vai ao Pagamento pela
// porta de entrada do hub, sem um contexto importar o outro.
public sealed class ContaTemFaturaEmAbertoNoPagamento(PagFaturas.IContaTemFaturaEmAberto pagamento) : IContaTemFaturaEmAberto
{
    public Task<bool> Executar(string contaId, CancellationToken ct = default) => pagamento.Executar(contaId, ct);
}
