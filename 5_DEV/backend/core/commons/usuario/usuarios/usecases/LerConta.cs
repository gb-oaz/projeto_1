using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>GET /contas/{id} (RN-187): só o cadastro, para o time deboaz.</summary>
public sealed class LerConta(IUsuarios usuarios, IContas contas, IPagamentos pagamentos, TimeProvider relogio)
    : ILerConta
{
    readonly Apoio apoio = new(usuarios, contas, relogio);

    /// <inheritdoc />
    public async Task<ContaLidaDto> Executar(PedidoDeContaDto pedido, CancellationToken ct = default)
    {
        var conta = await ContaPedida(pedido, ct);
        var master = await usuarios.MasterDaConta(conta.Id, ct);
        var pagamento = await pagamentos.UltimoPagamento(conta.Id, ct);
        return Montar(conta, master, pagamento);
    }

    /// <summary>A conta assinante, se quem pede é do time deboaz.</summary>
    /// <exception cref="FalhaDeUsuarios">ACESSO_NEGADO ou CONTA_INEXISTENTE.</exception>
    async Task<ContaAssinante> ContaPedida(PedidoDeContaDto pedido, CancellationToken ct)
    {
        await apoio.DoTime(pedido.QuemPede, ct);
        return await apoio.Assinante(pedido.ContaId, ct);
    }

    /// <summary>O cadastro da conta, com a entrada da master e o dia do último pagamento.</summary>
    static ContaLidaDto Montar(ContaAssinante conta, UsuarioCadastrado? master, DateOnly? pagamento) =>
        new(conta.Id, conta.Tipo.Texto(), conta.Nome, conta.Documento, conta.Sigla, master?.Login ?? Logins.DaMaster(conta.Dominio),
            conta.Responsavel, conta.Telefone, conta.Estado.Texto(), conta.CriadaEm,
            pagamento?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), conta.UltimoAcessoMaster);
}
