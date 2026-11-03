namespace Boaz.Core.Commons.Usuario.Autorizacao.Dtos;

/// <summary>Papel, módulos e pv que vão no token (RNF-219).</summary>
/// <param name="Papel">ADMINISTRADOR, MEMBRO_TIME, MASTER, AUTONOMO, STAFF, PARTICIPANTE ou ANONIMO.</param>
/// <param name="Mods">Módulo → "C", "U" ou "CU".</param>
/// <param name="Pv">Versão das permissões.</param>
public sealed record PermissoesNoTokenDto(string Papel, IReadOnlyDictionary<string, string> Mods, int Pv);

/// <summary>Um módulo que quem pede pode conceder, em GET …/permissoes (RF-160).</summary>
/// <param name="Mod">MOD-NN.</param>
/// <param name="Nome">Nome do módulo.</param>
/// <param name="Abas">As abas que pode dar.</param>
public sealed record ConcedivelDto(string Mod, string Nome, string Abas);

/// <summary>Corpo de GET /usuarios/{id}/permissoes.</summary>
/// <param name="Usuario">De quem são.</param>
/// <param name="Versao">A versão para o If-Match.</param>
/// <param name="Mods">Os módulos que a pessoa tem e a conta tem agora.</param>
/// <param name="Concediveis">O que quem pede pode marcar.</param>
public sealed record PermissoesLidasDto(string Usuario, int Versao, IReadOnlyDictionary<string, string> Mods, IReadOnlyList<ConcedivelDto> Concediveis);

/// <summary>Corpo de 200 em PUT /usuarios/{id}/permissoes.</summary>
/// <param name="Versao">A versão nova, ou a mesma se nada mudou (RN-255).</param>
/// <param name="Mods">As permissões salvas.</param>
public sealed record PermissoesSalvasDto(int Versao, IReadOnlyDictionary<string, string> Mods);

/// <summary>Uma linha de GET …/permissoes/historico (RN-256).</summary>
/// <param name="Em">Quando.</param>
/// <param name="Por">Quem salvou.</param>
/// <param name="Versao">A versão criada.</param>
/// <param name="Antes">Abas de antes dos módulos que mudaram (null = não tinha).</param>
/// <param name="Depois">Abas de depois (null = saiu).</param>
public sealed record MudancaDto(DateTimeOffset Em, string Por, int Versao,
    IReadOnlyDictionary<string, string?> Antes, IReadOnlyDictionary<string, string?> Depois);
