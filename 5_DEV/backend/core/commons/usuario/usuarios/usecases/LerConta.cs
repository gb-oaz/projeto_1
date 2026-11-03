using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>GET /contas/{id} (RN-187): só o cadastro, para o time deboaz.</summary>
public sealed class LerConta(IUsuarios usuarios, IContas contas, IPagamentos pagamentos, IAuditoria auditoria, TimeProvider relogio)
    : ILerConta
{
    readonly Apoio apoio = new(usuarios, contas, auditoria, relogio);

    /// <inheritdoc />
    public async Task<ContaLidaDto> Executar(PedidoDeContaDto pedido, CancellationToken ct = default)
    {
        await apoio.DoTime(pedido.QuemPede, ct);
        var conta = await apoio.Assinante(pedido.ContaId, ct);
        var master = await usuarios.MasterDaConta(conta.Id, ct);
        var pagamento = await pagamentos.UltimoPagamento(conta.Id, ct);
        return new(conta.Id, conta.Tipo.Texto(), conta.Nome, conta.Documento, conta.Sigla, master?.Login ?? Logins.DaMaster(conta.Dominio),
            conta.Responsavel, conta.Telefone, conta.Estado.Texto(), conta.CriadaEm,
            pagamento?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), conta.UltimoAcessoMaster);
    }
}
