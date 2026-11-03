using System.Globalization;
using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using static Boaz.MsMono.Infra.In.Commons.Http;

namespace Boaz.MsMono.Infra.In.Commons.Usuario.Usuarios;

// Corpos dos pedidos, como chegam no JSON (usuarios-contrato-api v03). Quem
// pede não vem no corpo: sai do token.
public sealed record NovoUsuarioCorpo(string? Tipo, string? Nome, string? NomeCurto, string? Matricula, IReadOnlyList<ResponsavelDto?>? Responsaveis);

public sealed record EdicaoDeUsuarioCorpo(string? Nome, string? NomeCurto, string? Matricula, IReadOnlyList<ResponsavelDto?>? Responsaveis);

public sealed record AjudaDaMasterCorpo(string? Instituicao, string? Contato);

public sealed record EstadoDaContaCorpo(string? Estado);

public sealed record ConferenciaDaMasterCorpo(string? Documento, string? Responsavel, string? Telefone, string? UltimoPagamento);

public sealed record TempoDaContaCorpo(string? StaffNavegador, string? StaffApp, string? Participante);

public sealed record TempoDoTimeCorpo(string? MembrosNavegador, string? MembrosApp, string? MastersNavegador, string? MastersApp);

// Endereços do módulo Usuários (17_modulo-usuario): contas, staff,
// participantes e membros do time, o card e o tempo de sessão.
public static class UsuariosApi
{
    public const string Tag = "Usuários";

    // A API deste módulo no seletor do Swagger (/doc/api/usuarios.json).
    public const string Documento = "usuarios";
    public const string Descricao =
        "Contas assinantes, staff, participantes e membros do time (17_modulo-usuario): cadastro, senha, código de acesso, " +
        "estado, card e tempo de sessão. Com login, exige o módulo Usuários (MOD-03) na aba Utilização, e na Configuração para o tempo de sessão";

    public static IEndpointRouteBuilder MapUsuarios(this IEndpointRouteBuilder rotas)
    {
        var api = rotas.MapGroup("").WithTags(Tag).WithGroupName(Documento);
        var uso = ExigeUsuarios(Abas.U);
        var configuracao = ExigeUsuarios(Abas.C);

        // ── sem login ──────────────────────────────────────────────────────────

        api.MapPost("/contas", async (NovaContaDto? corpo, ICriarConta criar, CancellationToken ct) =>
                Results.Json(await criar.Executar(corpo ?? new(null, null, null, null, null, null, null), ct), statusCode: StatusCodes.Status201Created))
            .WithSummary("Criar a conta de uma instituição ou de um educador autônomo")
            .WithDescription("Endereço público, da tela Criar conta do hub (RF-104, RN-179, RN-180, RN-242). A senha da conta master é " +
                "gerada e mostrada uma vez; a pessoa anota e confirma em POST /auth/hub/confirmar-senha com o comprovante.")
            .Produces<ContaCriadaDto>(StatusCodes.Status201Created)
            .Produces<Erro>(StatusCodes.Status409Conflict)
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity);

        api.MapGet("/contas/sigla-sugerida", (string? nome, ISugerirSigla sugerir, CancellationToken ct) => sugerir.Executar(new NomeDaContaDto(nome), ct))
            .WithSummary("Sugerir a sigla a partir do nome")
            .WithDescription("Endereço público (RN-179): iniciais sem conectores, completadas com a última palavra. Sigla null e livre false " +
                "se o nome não der 5 letras.")
            .Produces<SiglaSugeridaDto>();

        api.MapPost("/usuarios/gerar-senha", (GeracaoDeSenhaDto? corpo, IGerarSenha gerar, CancellationToken ct) =>
                gerar.Executar(corpo ?? new(null, null), ct))
            .WithSummary("Mostrar a senha nova a quem tem o código de liberação")
            .WithDescription("Endereço público (RF-109, RF-110, RN-181, RN-182): a senha sai uma vez, com o comprovante para confirmar. " +
                "A 5ª tentativa errada seguida bloqueia (RNF-204); o código vale 72 h.")
            .Produces<SenhaGeradaDto>()
            .Produces<Erro>(StatusCodes.Status401Unauthorized)
            .Produces<Erro>(StatusCodes.Status410Gone)
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity)
            .Produces<Erro>(StatusCodes.Status429TooManyRequests);

        api.MapPost("/contas/esqueci-senha-master", async (AjudaDaMasterCorpo? corpo, HttpContext http, IPedirAjudaDaMaster pedir, CancellationToken ct) =>
            {
                await pedir.Executar(new AjudaDaMasterDto(corpo?.Instituicao, corpo?.Contato, Aparelho(http)), ct);
                return Results.Accepted();
            })
            .WithSummary("Pedir ajuda ao SAC quando a conta master esqueceu a senha")
            .WithDescription("Endereço público (RF-111, RNF-205): vira uma mensagem no SAC. Até 3 envios em 24 h do mesmo aparelho.")
            .Produces(StatusCodes.Status202Accepted)
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity)
            .Produces<Erro>(StatusCodes.Status429TooManyRequests);

        // ── staff, participantes e membros do time ─────────────────────────────

        api.MapGet("/usuarios", (string? tipo, string? busca, string? estado, string? pagina, HttpContext http, IListarUsuarios listar,
                CancellationToken ct) => listar.Executar(new ListaDeUsuariosDto(http.QuemPede(), tipo, busca, estado, Pagina(pagina)), ct))
            .ComAcesso(uso)
            .WithSummary("Listar e buscar o staff, os participantes ou os membros do time")
            .WithDescription("Exige MOD-03 · U (RF-105, RN-185, RN-186, RN-188). 50 por página, de A a Z; a busca acha nome, entrada, matrícula e código. Sem tipo, busca em todos os tipos que quem pede pode listar, e cada item diz o seu.")
            .Produces<PaginaDto<UsuarioNaListaDto>>()
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity);

        api.MapGet("/usuarios/card", (HttpContext http, ILerCardDeUsuarios ler, CancellationToken ct) => ler.Executar(new QuemPedeDto(http.QuemPede()), ct))
            .ComAcesso(uso)
            .WithSummary("Montar o card Usuários")
            .WithDescription("Exige MOD-03 · U (RN-189). Instituição: staff + participantes ativos, sem a conta master; autônomo: " +
                "participantes ativos; time: contas ativas. Selo: códigos de liberação que vencem hoje (UTC).")
            .Produces<CardDeUsuariosDto>();

        api.MapGet("/usuarios/{id}", (string id, HttpContext http, ILerUsuario ler, CancellationToken ct) =>
                ler.Executar(new PedidoDeUsuarioDto(http.QuemPede(), id), ct))
            .ComAcesso(uso)
            .WithSummary("Abrir o usuário selecionado")
            .WithDescription("Exige MOD-03 · U (RF-153, RN-186). Id de outra conta responde 404 (RN-235).")
            .Produces<UsuarioLidoDto>()
            .Produces<Erro>(StatusCodes.Status404NotFound);

        api.MapPost("/usuarios", async (NovoUsuarioCorpo? corpo, HttpContext http, ICriarUsuario criar, CancellationToken ct) =>
                Results.Json(await criar.Executar(new NovoUsuarioDto(http.QuemPede(), corpo?.Tipo, corpo?.Nome, corpo?.NomeCurto, corpo?.Matricula,
                    corpo?.Responsaveis), ct), statusCode: StatusCodes.Status201Created))
            .ComAcesso(uso)
            .WithSummary("Cadastrar um staff, um participante ou um membro do time")
            .WithDescription("Exige MOD-03 · U (RF-106, RF-108, RN-164, RN-165, RN-188, RN-246). O participante recebe o código de acesso " +
                "como login, sem senha; staff e membros recebem o código de liberação, mostrado uma vez, e começam sem módulos.")
            .Produces<UsuarioCriadoDto>(StatusCodes.Status201Created)
            .Produces<Erro>(StatusCodes.Status409Conflict)
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity);

        api.MapPatch("/usuarios/{id}", (string id, EdicaoDeUsuarioCorpo? corpo, HttpContext http, IEditarUsuario editar, CancellationToken ct) =>
                editar.Executar(new EdicaoDeUsuarioDto(http.QuemPede(), id, corpo?.Nome, corpo?.NomeCurto, corpo?.Matricula, corpo?.Responsaveis), ct))
            .ComAcesso(uso)
            .WithSummary("Editar nome, nome curto, matrícula e responsáveis")
            .WithDescription("Exige MOD-03 · U e ser superior (RF-153, RN-244). Nome curto novo: o staff mantém a senha e as sessões " +
                "terminam; o participante recebe código novo. \"responsaveis\" vai inteiro.")
            .Produces<UsuarioEditadoDto>()
            .Produces<Erro>(StatusCodes.Status404NotFound)
            .Produces<Erro>(StatusCodes.Status409Conflict)
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity);

        api.MapPost("/usuarios/{id}/nova-geracao-senha", async (string id, HttpContext http, IPedirNovaGeracaoDeSenha pedir, CancellationToken ct) =>
                Results.Json(await pedir.Executar(new PedidoDeUsuarioDto(http.QuemPede(), id), ct), statusCode: StatusCodes.Status201Created))
            .ComAcesso(uso)
            .WithSummary("Gerar o código de liberação para quem esqueceu a senha")
            .WithDescription("Exige MOD-03 · U e ser superior (RF-110, RN-169, RN-190, RNF-203). A senha antiga e as sessões terminam.")
            .Produces<CodigoDeLiberacaoDto>(StatusCodes.Status201Created)
            .Produces<Erro>(StatusCodes.Status404NotFound)
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity);

        api.MapPost("/usuarios/{id}/novo-codigo-acesso", (string id, HttpContext http, IGerarNovoCodigoDeAcesso gerar, CancellationToken ct) =>
                gerar.Executar(new PedidoDeUsuarioDto(http.QuemPede(), id), ct))
            .ComAcesso(uso)
            .WithSummary("Trocar o código de acesso perdido de um participante")
            .WithDescription("Exige MOD-03 · U (RF-154, RN-184, RN-245). O código anterior e as sessões dele terminam na hora.")
            .Produces<CodigoDeAcessoDto>()
            .Produces<Erro>(StatusCodes.Status404NotFound)
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity);

        api.MapPost("/usuarios/{id}/desativar", async (string id, HttpContext http, IDesativarUsuario desativar, CancellationToken ct) =>
            {
                await desativar.Executar(new PedidoDeUsuarioDto(http.QuemPede(), id), ct);
                return Results.NoContent();
            })
            .ComAcesso(uso)
            .WithSummary("Tirar o acesso de quem saiu, sem apagar")
            .WithDescription("Exige MOD-03 · U e ser superior (RF-112, RF-141, RN-169, RN-174, RN-175). Todas as sessões terminam.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<Erro>(StatusCodes.Status404NotFound)
            .Produces<Erro>(StatusCodes.Status409Conflict);

        api.MapPost("/usuarios/{id}/reativar", async (string id, HttpContext http, IReativarUsuario reativar, CancellationToken ct) =>
            {
                await reativar.Executar(new PedidoDeUsuarioDto(http.QuemPede(), id), ct);
                return Results.NoContent();
            })
            .ComAcesso(uso)
            .WithSummary("Devolver o acesso, com as mesmas permissões")
            .WithDescription("Exige MOD-03 · U e ser superior (RF-112). Volta PENDENTE se nunca gerou a senha nem entrou; senão ATIVO.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<Erro>(StatusCodes.Status404NotFound)
            .Produces<Erro>(StatusCodes.Status409Conflict);

        // ── configuração ───────────────────────────────────────────────────────

        api.MapGet("/contas/{id}/tempo-sessao", (string id, HttpContext http, ILerTempoDaConta ler, CancellationToken ct) =>
                ler.Executar(new PedidoDeContaDto(http.QuemPede(), id), ct))
            .ComAcesso(configuracao)
            .WithSummary("Ler o tempo de sessão do staff e dos participantes")
            .WithDescription("Exige MOD-03 · C (RF-114, RN-183). staffNavegador em horas; staffApp e participante em dias.")
            .Produces<TempoDaContaLidoDto>()
            .Produces<Erro>(StatusCodes.Status404NotFound);

        api.MapPut("/contas/{id}/tempo-sessao", (string id, TempoDaContaCorpo? corpo, HttpContext http, ISalvarTempoDaConta salvar,
                CancellationToken ct) => salvar.Executar(new TempoDaContaDto(http.QuemPede(), id, corpo?.StaffNavegador, corpo?.StaffApp,
                    corpo?.Participante), ct))
            .ComAcesso(configuracao)
            .WithSummary("Salvar o tempo de sessão do staff e dos participantes")
            .WithDescription("Exige MOD-03 · C (RF-114, RN-183). Fora dos limites: FORA_DOS_LIMITES com o campo, o mínimo e o máximo.")
            .Produces<TempoDaContaLidoDto>()
            .Produces<Erro>(StatusCodes.Status404NotFound)
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity);

        // ── time deboaz ────────────────────────────────────────────────────────

        api.MapGet("/contas", (string? busca, string? estado, string? pagina, HttpContext http, IListarContas listar, CancellationToken ct) =>
                listar.Executar(new ListaDeContasDto(http.QuemPede(), busca, estado, Pagina(pagina)), ct))
            .ComAcesso(uso)
            .WithSummary("Listar e buscar as contas assinantes")
            .WithDescription("Só o time deboaz, com MOD-03 · U (RF-113, RN-185, RN-186). A conta do time não aparece.")
            .Produces<PaginaDto<ContaNaListaDto>>()
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity);

        api.MapGet("/contas/{id}", (string id, HttpContext http, ILerConta ler, CancellationToken ct) =>
                ler.Executar(new PedidoDeContaDto(http.QuemPede(), id), ct))
            .ComAcesso(uso)
            .WithSummary("Ver o cadastro de uma conta")
            .WithDescription("Só o time deboaz, com MOD-03 · U (RN-187): o cadastro, sem usuários nem conteúdo.")
            .Produces<ContaLidaDto>()
            .Produces<Erro>(StatusCodes.Status404NotFound);

        api.MapPost("/contas/{id}/estado", (string id, EstadoDaContaCorpo? corpo, HttpContext http, IMudarEstadoDaConta mudar, CancellationToken ct) =>
                mudar.Executar(new EstadoDaContaDto(http.QuemPede(), id, corpo?.Estado), ct))
            .ComAcesso(uso)
            .WithSummary("Suspender, desativar, reativar ou encerrar uma conta")
            .WithDescription("Só o time deboaz, com MOD-03 · U (RF-113, RN-173, RN-174, RN-175, RNF-203). Fora de ATIVA ninguém da conta " +
                "entra e as sessões terminam; encerrada não volta.")
            .Produces<ContaComEstadoDto>()
            .Produces<Erro>(StatusCodes.Status404NotFound)
            .Produces<Erro>(StatusCodes.Status409Conflict)
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity);

        api.MapPost("/contas/{id}/codigo-master", async (string id, ConferenciaDaMasterCorpo? corpo, HttpContext http, IGerarCodigoDaMaster gerar,
                CancellationToken ct) => Results.Json(await gerar.Executar(new ConferenciaDaMasterDto(http.QuemPede(), id, corpo?.Documento,
                    corpo?.Responsavel, corpo?.Telefone, corpo?.UltimoPagamento), ct), statusCode: StatusCodes.Status201Created))
            .ComAcesso(uso)
            .WithSummary("Conferir o cadastro e gerar o código da conta master")
            .WithDescription("Só o time deboaz, com MOD-03 · U (RN-172, RN-190): o CNPJ e 2 de 3 entre quem cadastrou, telefone e " +
                "data do último pagamento.")
            .Produces<CodigoDaMasterDto>(StatusCodes.Status201Created)
            .Produces<Erro>(StatusCodes.Status404NotFound)
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity);

        api.MapGet("/time/tempo-sessao", (HttpContext http, ILerTempoDoTime ler, CancellationToken ct) => ler.Executar(new QuemPedeDto(http.QuemPede()), ct))
            .ComAcesso(configuracao)
            .WithSummary("Ler o tempo de sessão dos membros do time e das contas master")
            .WithDescription("Só o time deboaz, com MOD-03 · C (RF-152, RN-243). Todos em horas.")
            .Produces<TempoDoTimeLidoDto>();

        api.MapPut("/time/tempo-sessao", (TempoDoTimeCorpo? corpo, HttpContext http, ISalvarTempoDoTime salvar, CancellationToken ct) =>
                salvar.Executar(new TempoDoTimeDto(http.QuemPede(), corpo?.MembrosNavegador, corpo?.MembrosApp, corpo?.MastersNavegador,
                    corpo?.MastersApp), ct))
            .ComAcesso(configuracao)
            .WithSummary("Salvar o tempo de sessão dos membros do time e das contas master")
            .WithDescription("Só o time deboaz, com MOD-03 · C (RF-152, RN-243). Fora dos limites: FORA_DOS_LIMITES.")
            .Produces<TempoDoTimeLidoDto>()
            .Produces<Erro>(StatusCodes.Status422UnprocessableEntity);

        return rotas;
    }

    // A página da lista: ausente é a primeira; texto que não é número, a página 0,
    // que o caso de uso recusa com DADOS_INVALIDOS em "pagina".
    static int? Pagina(string? pagina) =>
        pagina is null ? null : int.TryParse(pagina, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    // O aparelho do pedido de ajuda (RNF-205): o endereço de quem envia.
    static string Aparelho(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
}
