using Boaz.Core.Hub.Sac.Anexos.Domain;

namespace Boaz.Core.Hub.Sac.Anexos.Ports.Out;

/// <summary>A coleção anexosSac (sac-documentos v03).</summary>
public interface IAnexos
{
    /// <summary>Grava o anexo novo.</summary>
    Task Incluir(Anexo anexo, CancellationToken ct = default);

    /// <summary>O anexo pelo id; null se não existe.</summary>
    Task<Anexo?> Obter(string id, CancellationToken ct = default);

    /// <summary>Os de expiraEm vencido até agora, usados ou não, que ainda têm o arquivo (RNF-234).</summary>
    Task<IReadOnlyList<Anexo>> VencidosAte(DateTimeOffset agora, CancellationToken ct = default);

    /// <summary>Tira o anexo descartado.</summary>
    Task Apagar(string id, CancellationToken ct = default);

    /// <summary>O arquivo do anexo usado saiu: o registro fica, com removidoEm (RNF-234).</summary>
    Task MarcarRemovido(string id, DateTimeOffset agora, CancellationToken ct = default);
}

/// <summary>
/// Os arquivos dos anexos (sac-classes v05): grava no destino da configuração
/// (Sac:Arquivos:Destino) e lê, apaga e marca no armazenamento de cada anexo,
/// o contêiner anexos-sac do armazenamento de objetos ou o GridFS. Só o backend lê e grava.
/// </summary>
public interface IArquivos
{
    /// <summary>Guarda o conteúdo no caminho, com o tipo, no destino; devolve onde guardou.</summary>
    Task<ArmazenamentoDoAnexo> Guardar(string objeto, byte[] conteudo, string tipo, CancellationToken ct = default);

    /// <summary>O conteúdo do caminho, de onde foi guardado; null se não existe.</summary>
    Task<Stream?> Abrir(ArmazenamentoDoAnexo armazenamento, string objeto, CancellationToken ct = default);

    /// <summary>Apaga o caminho de onde foi guardado; o que não existe fica como está.</summary>
    Task Apagar(ArmazenamentoDoAnexo armazenamento, string objeto, CancellationToken ct = default);

    /// <summary>A tag "uso" do arquivo (RNF-234); só o armazenamento de objetos a tem.</summary>
    Task MarcarUso(ArmazenamentoDoAnexo armazenamento, string objeto, UsoDoArquivo uso, CancellationToken ct = default);
}

/// <summary>A verificação de segurança do arquivo antes de gravar (RN-292): falha fechada.</summary>
public interface IVerificacaoDeArquivos
{
    /// <summary>Limpo, ameaça ou indisponível.</summary>
    Task<ResultadoDaVerificacao> Verificar(byte[] conteudo, CancellationToken ct = default);
}
