using System.Globalization;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using static Boaz.MsMono.Infra.In.Commons.Http;
using AcaoAuditada = Boaz.Core.Commons.Usuario.Usuarios.Domain.AcaoAuditada;

namespace Boaz.MsMono.Infra.In.Commons.Usuario.Autorizacao;

// O corpo do PUT: o conjunto inteiro de módulos e abas.
public sealed record PermissoesCorpo(IReadOnlyDictionary<string, string?>? Mods);

// Endereços da Autorização (17_modulo-usuario-b-autorizacao): as permissões de
// uma pessoa, a gravação com If-Match e o registro de mudanças.
public static class AutorizacaoApi
{
    public const string Tag = "Autorização";

    // A API deste módulo no seletor do Swagger (/doc/api/autorizacao.json).
    public const string Documento = "autorizacao";
    public const string Descricao =
        "Permissões por módulo e aba (17_modulo-usuario-b-autorizacao): ler, salvar o conjunto inteiro com a versão lida " +
        "no If-Match e o registro de mudanças. Exige MOD-03 · U e ser superior da pessoa";

    public static IEndpointRouteBuilder MapAutorizacao(this IEndpointRouteBuilder rotas)
    {
        var api = rotas.MapGroup("/usuarios/{id}/permissoes").WithTags(Tag).WithGroupName(Documento);
        var uso = ExigeUsuarios(Abas.U);

        api.MapGet("", (string id, HttpContext http, ILerPermissoes ler, CancellationToken ct) =>
                ler.Executar(new PedidoDePermissoesDto(http.QuemPede(), id), ct))
            .ComAcesso(uso)
            .WithSummary("Abrir as permissões da pessoa e o que ela pode receber")
            .WithDescription("Exige MOD-03 · U e ser superior (RF-160, RN-252). Administrador para membro do time; conta master ou " +
                "staff com Administrar usuários para o staff abaixo. Id de outra conta responde 404 (RN-235).")
            .Produces<PermissoesLidasDto>()
            .Produces<Erro>(StatusCodes.Status404NotFound);

        api.MapPut("", (string id, PermissoesCorpo? corpo, HttpContext http, ISalvarPermissoes salvar, CancellationToken ct) =>
                salvar.Executar(new SalvamentoDePermissoesDto(http.QuemPede(), id, http.Request.Headers.IfMatch.ToString() is { Length: > 0 } v ? v : null,
                    corpo?.Mods), http.NovoTraceId(), ct))
            .ComAcesso(uso, AcaoAuditada.EditarPermissoes, IdDaRota)
            .WithSummary("Salvar o conjunto inteiro de permissões da pessoa")
            .WithDescription("Exige MOD-03 · U, ser superior e o If-Match com a versão lida (RF-161, RF-162, RN-241, RN-254, RN-255). " +
                "Módulo fora do mapa é retirado; o pv da pessoa sobe e as sessões dela renovam.")
            .Produces<PermissoesSalvasDto>()
            .Produces<Erro>(StatusCodes.Status404NotFound)
            .Produces<Erro>(StatusCodes.Status412PreconditionFailed)
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity)
            .Produces<Erro>(StatusCodes.Status428PreconditionRequired);

        api.MapGet("/historico", (string id, string? ate, HttpContext http, ILerHistoricoDePermissoes ler, CancellationToken ct) =>
                ler.Executar(new PedidoDePermissoesDto(http.QuemPede(), id, Ate(ate)), ct))
            .ComAcesso(uso)
            .WithSummary("Mostrar o registro de mudanças das permissões")
            .WithDescription("Exige MOD-03 · U e ser superior (RN-256): 50 por página, da mais nova para a mais antiga; ?ate= pega as anteriores.")
            .Produces<IReadOnlyList<MudancaDto>>()
            .Produces<Erro>(StatusCodes.Status404NotFound)
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity);

        return rotas;
    }

    static DateTimeOffset? Ate(string? ate) =>
        ate is null ? null
        : DateTimeOffset.TryParse(ate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var data) ? data
        : throw FalhaDeAutorizacao.DadosInvalidos("ate");
}
