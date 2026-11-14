using Boaz.Core.Hub.Sac.Anexos.Domain;
using Boaz.Core.Hub.Sac.Anexos.Ports.Out;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;
using Boaz.MsMono.Secundario.Out.Commons;
using Boaz.MsMono.Secundario.Out.Hub.Sac;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Tests.Integracao;

// Os adaptadores de saída da feature anexos com o banco de verdade
// (sac-hexagonal v01, sac-documentos v02, RN-292): AnexosNoBanco (IAnexos e
// IAnexosDoChamado) e ArquivosNoGridFs (balde "sacAnexos"). No
// padrão de ContasNoBancoTests. Os vencimentos ficam no futuro do relógio
// real, para o AgendamentoDoSac do próprio backend não descartar os anexos do teste.
public class AnexosNoBancoTests(Backend backend) : IClassFixture<Backend>
{
    static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    IAnexos Anexos => backend.Services.GetRequiredService<IAnexos>();

    IAnexosDoChamado DoChamado => backend.Services.GetRequiredService<IAnexosDoChamado>();

    // o adaptador do GridFS em si: o IArquivos da aplicação (ArquivosDosAnexos,
    // sac-classes v04) escolhe o destino pela configuração
    ArquivosNoGridFs Arquivos => new(backend.Services.GetRequiredService<Armazenamento>());

    async Task<Anexo> Enviado(string usuario, DateTimeOffset quando)
    {
        _ = backend.Server;
        var anexo = Anexo.Novo("erro.png", TipoDeAnexo.Png, Png, usuario, "conta-anexos", quando);
        await Anexos.Incluir(anexo);
        return anexo;
    }

    static string Usuario() => $"usr-anexos-{Guid.NewGuid():N}";

    // o registro guarda só o anexo limpo, com o caminho do arquivo e o vencimento em 24 h
    [Fact]
    public async Task RN_292_registro_do_anexo_verificado()
    {
        var agora = DateTimeOffset.UtcNow;
        var anexo = await Enviado(Usuario(), agora);

        var documento = await backend.Banco.GetCollection<BsonDocument>("anexosSac")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", anexo.Id)).SingleAsync();

        Assert.Matches("^anx-[0-9a-f]+$", anexo.Id);
        Assert.Equal(($"sac/anexos/{anexo.Id}", "LIMPO", "conta-anexos"),
            (documento["objeto"].AsString, documento["verificacao"]["resultado"].AsString, documento["contaId"].AsString));
        Assert.True(documento["chamadoId"].IsBsonNull);
        Assert.Equal(documento["enviadoEm"].ToUniversalTime().AddHours(24), documento["expiraEm"].ToUniversalTime());
        var lido = (await Anexos.Obter(anexo.Id))!;
        Assert.Equal((anexo.Nome, anexo.Tipo, anexo.Tamanho, anexo.Sha256, anexo.EnviadoPor, (string?)null),
            (lido.Nome, lido.Tipo, lido.Tamanho, lido.Sha256, lido.EnviadoPor, lido.ChamadoId));
    }

    // não usado em 24 h: vence e entra no descarte (VencidosAte, RNF-234); usado num chamado: não vence em 24 h
    [Fact]
    public async Task RN_292_nao_usado_em_24_h_e_descartado_e_usado_nao_vence()
    {
        var agora = DateTimeOffset.UtcNow;
        var parado = await Enviado(Usuario(), agora);
        var usado = await Enviado(Usuario(), agora);

        await DoChamado.Vincular([usado.Id], new NumeroDoChamado(42), "msg-000318", true);

        var antes = (await Anexos.VencidosAte(agora.AddHours(23))).Select(a => a.Id).ToList();
        var depois = (await Anexos.VencidosAte(agora.AddHours(24).AddMinutes(1))).Select(a => a.Id).ToList();
        Assert.DoesNotContain(parado.Id, antes);
        Assert.Contains(parado.Id, depois);
        Assert.DoesNotContain(usado.Id, depois);
        var vinculado = (await Anexos.Obter(usado.Id))!;
        Assert.Equal(("SAC-000042", "msg-000318", true, (DateTimeOffset?)null),
            (vinculado.ChamadoId, vinculado.MensagemId, vinculado.Interna, vinculado.ExpiraEm));

        await Anexos.Apagar(parado.Id);
        Assert.Null(await Anexos.Obter(parado.Id));
    }

    // RF-171, RF-173: só quem enviou usa o anexo, uma vez, antes de vencer
    [Fact]
    public async Task RN_292_disponivel_so_para_quem_enviou_uma_vez_e_antes_de_vencer()
    {
        var agora = DateTimeOffset.UtcNow;
        var dono = Usuario();
        var anexo = await Enviado(dono, agora);

        var doDono = await DoChamado.Disponiveis([anexo.Id], dono, agora.AddHours(1));
        var deOutro = await DoChamado.Disponiveis([anexo.Id], Usuario(), agora.AddHours(1));
        var vencido = await DoChamado.Disponiveis([anexo.Id], dono, agora.AddHours(25));
        var comInexistente = await DoChamado.Disponiveis([anexo.Id, "anx-inexistente"], dono, agora.AddHours(1));
        await DoChamado.Vincular([anexo.Id], new NumeroDoChamado(43), null, false);
        var jaUsado = await DoChamado.Disponiveis([anexo.Id], dono, agora.AddHours(1));

        Assert.Equal((true, false, false, false, false), (doDono, deOutro, vencido, comInexistente, jaUsado));
    }

    // ArquivosNoGridFs: guarda pelo caminho, devolve o mesmo conteúdo e apaga o arquivo e os pedaços
    [Fact]
    public async Task RN_292_arquivo_no_gridfs_pelo_caminho()
    {
        _ = backend.Server;
        var objeto = $"sac/anexos/anx-teste-{Guid.NewGuid():N}";
        var arquivos = backend.Banco.GetCollection<BsonDocument>("sacAnexos.files");
        var pedacos = backend.Banco.GetCollection<BsonDocument>("sacAnexos.chunks");

        await Arquivos.Guardar(objeto, Png);
        var guardado = await arquivos.Find(Builders<BsonDocument>.Filter.Eq("filename", objeto)).SingleAsync();
        await using (var lido = await Arquivos.Abrir(objeto))
        {
            using var copia = new MemoryStream();
            await lido!.CopyToAsync(copia);
            Assert.Equal(Png, copia.ToArray());
        }
        await Arquivos.Apagar(objeto);

        Assert.Null(await Arquivos.Abrir(objeto));
        Assert.Equal(0, await arquivos.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("filename", objeto)));
        Assert.Equal(0, await pedacos.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("files_id", guardado["_id"])));
    }
}
