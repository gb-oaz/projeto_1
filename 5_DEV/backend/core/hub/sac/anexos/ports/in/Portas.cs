using Boaz.Core.Hub.Sac.Anexos.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Dtos;

namespace Boaz.Core.Hub.Sac.Anexos.Ports.In;

/// <summary>POST /sac/anexos (RN-292).</summary>
public interface IEnviarAnexo
{
    /// <summary>Confere o tamanho, o tipo pelo conteúdo e a segurança, e guarda.</summary>
    Task<AnexoDto> Executar(Stream conteudo, string? nome, QuemPede quem, CancellationToken ct = default);
}

/// <summary>GET /sac/anexos/{id} (RN-288, RN-292).</summary>
public interface ILerAnexo
{
    /// <summary>O arquivo, para quem vê o chamado dele.</summary>
    Task<ArquivoDoAnexo> Executar(string id, QuemPede quem, CancellationToken ct = default);
}

/// <summary>O descarte dos anexos de expiraEm vencido (RN-292, RNF-234), pelo AgendamentoDoSac.</summary>
public interface IDescartarAnexosVencidos
{
    /// <summary>Tira o arquivo de onde foi guardado e o registro do não usado; devolve quantos.</summary>
    Task<int> Executar(CancellationToken ct = default);
}
