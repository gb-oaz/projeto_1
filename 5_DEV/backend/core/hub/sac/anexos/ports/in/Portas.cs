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

/// <summary>O descarte dos anexos não usados em 24 h (RN-292), pelo AgendamentoDoSac.</summary>
public interface IDescartarAnexosNaoUsados
{
    /// <summary>Tira do banco e do armazenamento; devolve quantos.</summary>
    Task<int> Executar(CancellationToken ct = default);
}
