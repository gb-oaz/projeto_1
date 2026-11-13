using Boaz.Core.Hub.Sac.Anexos.Domain;
using Boaz.Core.Hub.Sac.Anexos.Dtos;
using Boaz.Core.Hub.Sac.Anexos.Ports.In;
using Boaz.Core.Hub.Sac.Anexos.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;

namespace Boaz.Core.Hub.Sac.Anexos.UseCases;

/// <summary>
/// POST /sac/anexos (RN-292), para a conta e o time: até 10 MB (413), PNG,
/// JPG ou PDF pelo conteúdo (415), sem ameaça na verificação (422), e a
/// verificação fora do ar recusa (503, falha fechada). Guarda o arquivo e o
/// anexo, que vence em 24 h se não for usado.
/// </summary>
public sealed class EnviarAnexo(IAnexos anexos, IArquivos arquivos, IVerificacaoDeArquivos verificacao, TimeProvider relogio) : IEnviarAnexo
{
    /// <inheritdoc />
    public async Task<AnexoDto> Executar(Stream conteudo, string? nome, QuemPede quem, CancellationToken ct = default)
    {
        _ = quem.Visao;
        var bytes = await Ler(conteudo, ct);
        var tipo = TipoDeAnexo.Do(bytes) ?? throw FalhaDoSac.TipoNaoAceito();
        switch (await verificacao.Verificar(bytes, ct))
        {
            case ResultadoDaVerificacao.Ameaca: throw FalhaDoSac.AnexoRecusado();
            case ResultadoDaVerificacao.Indisponivel: throw FalhaDoSac.VerificacaoIndisponivel();
        }
        var anexo = Anexo.Novo(Anexo.NomeDe(nome), tipo, bytes, quem.UsuarioId, quem.ContaId, relogio.GetUtcNow());
        await arquivos.Guardar(anexo.Objeto, bytes, ct);
        await anexos.Incluir(anexo, ct);
        return AnexoDto.De(anexo);
    }

    // o conteúdo até 10 MB; um byte a mais já é 413
    static async Task<byte[]> Ler(Stream conteudo, CancellationToken ct)
    {
        using var memoria = new MemoryStream();
        var bloco = new byte[81920];
        int lidos;
        while ((lidos = await conteudo.ReadAsync(bloco, ct)) > 0)
        {
            if (memoria.Length + lidos > LimitesDoAnexo.Tamanho) throw FalhaDoSac.AnexoGrande();
            memoria.Write(bloco, 0, lidos);
        }
        return memoria.ToArray();
    }
}
