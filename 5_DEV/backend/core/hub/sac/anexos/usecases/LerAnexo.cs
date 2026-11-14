using Boaz.Core.Hub.Sac.Anexos.Dtos;
using Boaz.Core.Hub.Sac.Anexos.Ports.In;
using Boaz.Core.Hub.Sac.Anexos.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;

namespace Boaz.Core.Hub.Sac.Anexos.UseCases;

/// <summary>
/// GET /sac/anexos/{id} (RN-288, RN-292, RNF-233): o time vê todos; a conta,
/// só os da própria conta e nunca o de nota interna. O de outra conta, o
/// descartado e o vencido pelo expiraEm, mesmo antes de o arquivo sair, são
/// como o que não existe (404). O arquivo é lido de onde foi guardado.
/// </summary>
public sealed class LerAnexo(IAnexos anexos, IArquivos arquivos, TimeProvider relogio) : ILerAnexo
{
    /// <inheritdoc />
    public async Task<ArquivoDoAnexo> Executar(string id, QuemPede quem, CancellationToken ct = default)
    {
        var visao = quem.Visao;
        var anexo = await anexos.Obter(id, ct);
        if (anexo is null || anexo.Vencido(relogio.GetUtcNow())
            || (visao == Visao.Conta && (anexo.ContaId != quem.ContaId || anexo.Interna)))
        {
            throw FalhaDoSac.AnexoInexistente();
        }
        var conteudo = await arquivos.Abrir(anexo.Armazenamento, anexo.Objeto, ct) ?? throw FalhaDoSac.AnexoInexistente();
        return new ArquivoDoAnexo(AnexoDto.De(anexo), conteudo);
    }
}
