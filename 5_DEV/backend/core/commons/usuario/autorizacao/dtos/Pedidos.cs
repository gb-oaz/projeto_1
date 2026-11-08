using Boaz.Core.Commons.Usuario.Autorizacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autorizacao.Dtos;

/// <summary>
/// GET /usuarios/{id}/permissoes e …/historico: quem pede (o sub do token já
/// conferido pela Autenticação) e de quem são as permissões.
/// </summary>
/// <param name="QuemPede">Id do usuário do token.</param>
/// <param name="UsuarioId">O {id} do endereço.</param>
/// <param name="Ate">No histórico, só as mudanças antes desta hora (página seguinte).</param>
public sealed record PedidoDePermissoesDto(string QuemPede, string UsuarioId, DateTimeOffset? Ate = null);

/// <summary>PUT /usuarios/{id}/permissoes (RF-161, RN-254, RNF-222).</summary>
/// <param name="QuemPede">Id do usuário do token.</param>
/// <param name="UsuarioId">O {id} do endereço.</param>
/// <param name="IfMatch">O cabeçalho If-Match, com a versão lida ("7" ou 7).</param>
/// <param name="Mods">O conjunto inteiro: módulo → "C", "U" ou "CU".</param>
public sealed record SalvamentoDePermissoesDto(string QuemPede, string UsuarioId, string? IfMatch, IReadOnlyDictionary<string, string?>? Mods)
{
    /// <summary>A versão lida; sem If-Match é VERSAO_AUSENTE (428).</summary>
    /// <exception cref="FalhaDeAutorizacao">VERSAO_AUSENTE ou DADOS_INVALIDOS em If-Match.</exception>
    public int VersaoLida()
    {
        if (string.IsNullOrWhiteSpace(IfMatch)) throw FalhaDeAutorizacao.VersaoAusente();
        return int.TryParse(IfMatch.Trim().Trim('"'), out var versao) && versao > 0
            ? versao
            : throw FalhaDeAutorizacao.DadosInvalidos("If-Match");
    }

    /// <summary>O mapa com as abas lidas; null, sem mods ou com abas fora de "C", "U" e "CU" é DADOS_INVALIDOS.</summary>
    /// <exception cref="FalhaDeAutorizacao">DADOS_INVALIDOS em mods ou mods.MOD-NN.</exception>
    public IReadOnlyDictionary<string, Abas> ModsLidos() =>
        (Mods ?? throw FalhaDeAutorizacao.DadosInvalidos("mods"))
            .ToDictionary(m => m.Key, m => Abas.De(m.Value) ?? throw FalhaDeAutorizacao.DadosInvalidos($"mods.{m.Key}"));
}

/// <summary>O papel e os módulos do token de um pedido, para conferir a exigência da rota (RNF-208).</summary>
/// <param name="Papel">A claim papel.</param>
/// <param name="Mods">A claim mods.</param>
public sealed record PedidoAutorizavelDto(string Papel, IReadOnlyDictionary<string, string> Mods);

/// <summary>
/// A execução de um endereço que muda estado, para auditar o 403 da rota por
/// faltar o módulo (RN-270): o traceId do X-Trace-Id, a ação do endereço, quem
/// pede e a conta dele, pelo token, e o alvo do endereço, se ele tem.
/// </summary>
/// <param name="TraceId">A execução, devolvida no cabeçalho X-Trace-Id.</param>
/// <param name="Acao">A ação do endereço, como DESATIVAR_USUARIO.</param>
/// <param name="ContaId">A conta de quem pede.</param>
/// <param name="Quem">Quem pede, o sub do token.</param>
/// <param name="Alvo">O alvo do endereço; null no que cria (POST /usuarios).</param>
public sealed record ExecucaoDaRotaDto(
    Boaz.Core.Commons.Usuario.Usuarios.Domain.TraceId TraceId, Boaz.Core.Commons.Usuario.Usuarios.Domain.AcaoAuditada Acao, string? ContaId,
    string Quem, string? Alvo);
