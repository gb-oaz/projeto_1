using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Ports.In;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Secundario.Out.Commons.Usuario.Usuarios;

// Os pedidos de ajuda da conta master sem login (17 RF-111, RNF-205) no SAC
// (18 RF-179, sac-contrato-api v02, POST /contas/esqueci-senha-master): o pedido vira, na criação, o
// chamado SENHA_CONTA_MASTER (IRegistrarPedidoDeSenha), e pedidosAjuda guarda
// só o limite do aparelho, com o mesmo número ("ajuda-000087" = "SAC-000087")
// e o chamado. O id do pedido é o alvo do "CONCLUIDO" do ESQUECI_SENHA_MASTER (RN-274).
public sealed class PedidosDeAjudaNoSac(Armazenamento armazenamento, IRegistrarPedidoDeSenha registrar) : IPedidosDeAjuda
{
    public async Task<IReadOnlyList<DateTimeOffset>> EnviosDoAparelho(string aparelho, DateTimeOffset desde, CancellationToken ct = default)
    {
        var lidos = await armazenamento.Colecao(Colecoes.PedidosAjuda)
            .Find(Bson.Filtro.Eq("aparelho", aparelho) & Bson.Filtro.Gt("quando", Bson.Valor(desde))).ToListAsync(ct);
        return lidos.Select(d => d.Data("quando")).OfType<DateTimeOffset>().ToList();
    }

    public async Task<string> Enviar(string aparelho, string instituicao, string contato, DateTimeOffset quando, CancellationToken ct = default)
    {
        var chamado = await registrar.Executar(aparelho, instituicao, contato, quando, ct);
        var id = chamado.Pedido();
        await armazenamento.Colecao(Colecoes.PedidosAjuda).InsertOneAsync(new BsonDocument
        {
            ["_id"] = id,
            ["aparelho"] = aparelho,
            ["instituicao"] = instituicao,
            ["contato"] = contato,
            ["quando"] = Bson.Valor(quando),
            ["chamado"] = chamado.Valor,
        }, cancellationToken: ct);
        return id;
    }
}
