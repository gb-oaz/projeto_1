using System.Text;
using Boaz.Core.Hub.Sac.Anexos.Domain;
using Boaz.Core.Hub.Tests.Sac.Mocks;
using static Boaz.Core.Hub.Tests.Sac.Mocks.Cenario;

namespace Boaz.Core.Hub.Tests.Sac.Anexos;

/// <summary>RN-292: tamanho, tipo pelo conteúdo, verificação de segurança e descarte em 24 h; RN-288 na leitura.</summary>
public sealed class AnexosTests
{
    readonly Cenario c = new();
    static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0];

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    // o PDF saiu do RN-292 (18 v06): só PNG e JPG são reconhecidos
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 }, null)]
    [InlineData(new byte[] { 0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70 }, null)]
    public void RN_292_tipo_pelo_conteudo(byte[] inicio, string? tipo) => Assert.Equal(tipo, TipoDeAnexo.Do(inicio));

    [Theory]
    [InlineData("erro.png", "erro.png")]
    [InlineData("C:\\pasta\\tela.png", "tela.png")]
    [InlineData("  ", "arquivo")]
    [InlineData(null, "arquivo")]
    public void Nome_sem_pastas(string? nome, string esperado) => Assert.Equal(esperado, Anexo.NomeDe(nome));

    [Fact]
    public void Nome_longo_corta_em_200() => Assert.Equal(200, Anexo.NomeDe(new string('n', 300)).Length);

    [Fact]
    public async Task RN_292_enviar_ler_e_descartar()
    {
        var anexo = await c.EnviarAnexo.Executar(new MemoryStream(Png), "erro.png", Master);
        Assert.Matches("^anx-[0-9a-f]{6}$", anexo.Id);
        Assert.Equal(("image/png", 10L), (anexo.Tipo, anexo.Tamanho));
        var lido = await c.LerAnexo.Executar(anexo.Id, Master);
        Assert.Equal(Png, ((MemoryStream)lido.Conteudo).ToArray());
        await c.LerAnexo.Executar(anexo.Id, Caio);
        await Recusa("ANEXO_INEXISTENTE", () => c.LerAnexo.Executar(anexo.Id, Pedro));
        c.Relogio.Agora = c.Relogio.Agora.AddHours(24).AddMinutes(1);
        await Recusa("ANEXO_INEXISTENTE", () => c.LerAnexo.Executar(anexo.Id, Master));
        Assert.Equal(1, await c.Descartar.Executar());
        Assert.Empty(c.Mock.Arquivos);
        await Recusa("ANEXO_INEXISTENTE", () => c.LerAnexo.Executar(anexo.Id, Caio));
    }

    [Fact]
    public async Task RN_288_anexo_de_nota_interna_so_o_time_ve()
    {
        var id = await c.No("EM_ATENDIMENTO");
        var anexo = await c.EnviarAnexo.Executar(new MemoryStream(Png), "log.png", Caio);
        c.Mock.Anexos[anexo.Id] = c.Mock.Anexos[anexo.Id] with { ContaId = "conta-ebfga" };
        await c.Enviar.Executar(id, new("Log", [anexo.Id], true), Caio, Trace);
        await Recusa("ANEXO_INEXISTENTE", () => c.LerAnexo.Executar(anexo.Id, Master));
        c.Mock.Arquivos.Clear();
        await Recusa("ANEXO_INEXISTENTE", () => c.LerAnexo.Executar(anexo.Id, Caio));
    }

    [Fact]
    public async Task RN_292_recusas_na_ordem_tamanho_tipo_verificacao()
    {
        await Recusa("ANEXO_GRANDE", () => c.EnviarAnexo.Executar(new MemoryStream(new byte[(11 * 1024 * 1024)]), "foto.jpg", Master));
        await Recusa("TIPO_NAO_ACEITO", () => c.EnviarAnexo.Executar(new MemoryStream(Encoding.ASCII.GetBytes("ftypmp42")), "tela.mp4", Master));
        c.Mock.Verificacao = ResultadoDaVerificacao.Ameaca;
        await Recusa("ANEXO_RECUSADO", () => c.EnviarAnexo.Executar(new MemoryStream(Png), "nota.png", Master));
        c.Mock.Verificacao = ResultadoDaVerificacao.Indisponivel;
        await Recusa("VERIFICACAO_INDISPONIVEL", () => c.EnviarAnexo.Executar(new MemoryStream(Png), "nota.png", Master));
        await Recusa("ACESSO_NEGADO", () => c.EnviarAnexo.Executar(new MemoryStream(Png), "nota.png", Maria));
        Assert.Empty(c.Mock.Anexos);
    }
}
