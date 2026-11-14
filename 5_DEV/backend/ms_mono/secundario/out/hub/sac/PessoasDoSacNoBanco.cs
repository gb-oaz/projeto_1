using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;
using Boaz.MsMono.Secundario.Out.Commons;
using MongoDB.Driver;

namespace Boaz.MsMono.Secundario.Out.Hub.Sac;

// Os nomes que o SAC copia do 17 (coleções usuarios e contas): o autor da
// conversa e a conta dona do chamado. Só lê.
public sealed class PessoasDoSacNoBanco(Armazenamento armazenamento) : IPessoasDoSac
{
    public async Task<string> Nome(string usuarioId, CancellationToken ct = default)
    {
        var usuario = await armazenamento.Colecao(Colecoes.Usuarios).Find(Bson.Filtro.Eq("_id", usuarioId)).FirstOrDefaultAsync(ct);
        return usuario?.Texto("nome") ?? usuarioId;
    }

    public async Task<ContaDoChamado?> Conta(string contaId, CancellationToken ct = default)
    {
        var conta = await armazenamento.Colecao(Colecoes.Contas).Find(Bson.Filtro.Eq("_id", contaId)).FirstOrDefaultAsync(ct);
        return conta is null ? null : new ContaDoChamado(contaId, conta.Texto("sigla") ?? "", conta.Texto("nome") ?? "", conta.Texto("tipo") ?? "");
    }
}
