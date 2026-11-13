using Boaz.Core.Hub.Sac.Anexos.Domain;

namespace Boaz.Core.Hub.Sac.Anexos.Ports.Out;

/// <summary>A coleção anexosSac (sac-documentos v01).</summary>
public interface IAnexos
{
    /// <summary>Grava o anexo novo.</summary>
    Task Incluir(Anexo anexo, CancellationToken ct = default);

    /// <summary>O anexo pelo id; null se não existe.</summary>
    Task<Anexo?> Obter(string id, CancellationToken ct = default);

    /// <summary>Os não usados vencidos até agora (RN-292).</summary>
    Task<IReadOnlyList<Anexo>> NaoUsadosAte(DateTimeOffset agora, CancellationToken ct = default);

    /// <summary>Tira o anexo descartado.</summary>
    Task Apagar(string id, CancellationToken ct = default);
}

/// <summary>Os arquivos no armazenamento de objetos (contêiner privado sac-anexos): só o backend lê e grava.</summary>
public interface IArquivos
{
    /// <summary>Guarda o conteúdo no caminho.</summary>
    Task Guardar(string objeto, byte[] conteudo, CancellationToken ct = default);

    /// <summary>O conteúdo do caminho; null se não existe.</summary>
    Task<Stream?> Abrir(string objeto, CancellationToken ct = default);

    /// <summary>Apaga o caminho; o que não existe fica como está.</summary>
    Task Apagar(string objeto, CancellationToken ct = default);
}

/// <summary>A verificação de segurança do arquivo antes de gravar (RN-292): falha fechada.</summary>
public interface IVerificacaoDeArquivos
{
    /// <summary>Limpo, ameaça ou indisponível.</summary>
    Task<ResultadoDaVerificacao> Verificar(byte[] conteudo, CancellationToken ct = default);
}
