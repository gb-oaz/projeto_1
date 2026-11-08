using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.UseCases;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autorizacao.UseCases;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using Boaz.Core.Commons.Usuario.Usuarios.UseCases;
using Boaz.MsMono.Infra.In.Commons;
using Boaz.MsMono.Infra.In.Commons.Usuario.Autenticacao;
using Boaz.MsMono.Infra.In.Commons.Usuario.Autorizacao;
using Boaz.MsMono.Infra.In.Commons.Usuario.Usuarios;
using Boaz.MsMono.Infra.Out.Commons;
using Boaz.MsMono.Infra.Out.Commons.Usuario;
using Boaz.MsMono.Infra.Out.Commons.Usuario.Autenticacao;
using Boaz.MsMono.Infra.Out.Commons.Usuario.Autorizacao;
using Boaz.MsMono.Infra.Out.Commons.Usuario.Usuarios;
using Boaz.MsMono.Sistema;
using Microsoft.Extensions.Options;
using Autenticacao = Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;
using Autorizacao = Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;

// Ponto de composição do backend (RN-149): aqui, e só aqui, as portas do
// core/ são ligadas aos adaptadores de infra/in/ e infra/out/.
var builder = WebApplication.CreateBuilder(args);
var servicos = builder.Services;

servicos.AddConfiguracoes();
servicos.AddDocumentacaoApi();
servicos.AddJsonDasApis();
servicos.AddSingleton(TimeProvider.System);
// AMBIENTE (hml ou prd): só homologação tem as contas qa (17 RN-179, RN-266)
servicos.AddSingleton(new Boaz.Core.Commons.Usuario.Usuarios.Domain.AmbienteDaAplicacao(
    builder.Configuration[Boaz.Core.Commons.Usuario.Usuarios.Domain.ContasIniciais.VariavelDoAmbiente]));

// ── infra/out: banco, cache, índices e o registro de módulos ─────────────────
servicos.AddSingleton<Armazenamento>();
servicos.AddSingleton<Indices>();
servicos.AddSingleton<RegistroDeModulos>();

// commons/usuario/usuarios
servicos.AddSingleton<IUsuarios, UsuariosNoBanco>();
servicos.AddSingleton<IContas, ContasNoBanco>();
servicos.AddSingleton<IGeracoesDeSenha, GeracoesNoBanco>();
servicos.AddSingleton<RegistrosNoBanco>();
servicos.AddSingleton<IAuditoria>(s => s.GetRequiredService<RegistrosNoBanco>());
servicos.AddSingleton<IAuditoriaDeErros>(s => s.GetRequiredService<RegistrosNoBanco>());
servicos.AddSingleton<IPedidosDeAjuda>(s => s.GetRequiredService<RegistrosNoBanco>());
servicos.AddSingleton<IPagamentos>(s => s.GetRequiredService<RegistrosNoBanco>());
servicos.AddSingleton<IIndicadores, IndicadoresNoBanco>();
servicos.AddSingleton<ISessoesDosUsuarios, SessoesDosUsuarios>();
servicos.AddSingleton<TentativasNoRedis>();
servicos.AddSingleton<ITentativasDeLiberacao>(s => s.GetRequiredService<TentativasNoRedis>());

// commons/usuario/autenticacao
servicos.AddSingleton<CadastroDaEntrada>();
servicos.AddSingleton<Autenticacao.IUsuarios>(s => s.GetRequiredService<CadastroDaEntrada>());
servicos.AddSingleton<Autenticacao.IContas>(s => s.GetRequiredService<CadastroDaEntrada>());
servicos.AddSingleton<Autenticacao.IGeracoesDeSenha>(s => s.GetRequiredService<CadastroDaEntrada>());
servicos.AddSingleton<Autenticacao.ISessoes, SessoesNoRedis>();
servicos.AddSingleton<Autenticacao.ITentativas>(s => s.GetRequiredService<TentativasNoRedis>());
servicos.AddSingleton<Autenticacao.IAssinador, AssinadorRsa>();
servicos.AddSingleton<Autenticacao.IPermissoes, PermissoesDoToken>();
servicos.AddSingleton<Autenticacao.ISalas, SalasAindaSemModulo>();

// commons/usuario/autorizacao
servicos.AddSingleton<PermissoesNoBanco>();
servicos.AddSingleton<Autorizacao.IUsuarios>(s => s.GetRequiredService<PermissoesNoBanco>());
servicos.AddSingleton<Autorizacao.IPermissoesGuardadas>(s => s.GetRequiredService<PermissoesNoBanco>());
servicos.AddSingleton<Autorizacao.IModulos>(s => s.GetRequiredService<RegistroDeModulos>());
servicos.AddSingleton<Autorizacao.IContratacoes, ContratacoesNoBanco>();
servicos.AddSingleton<Autorizacao.ISessoesDoUsuario, AvisoDePermissoes>();

// ── core: cada porta de entrada ligada ao caso de uso dela ───────────────────
// commons/usuario/autenticacao
servicos.AddSingleton<IEntrarNoHub, EntrarNoHub>();
servicos.AddSingleton<IEntrarNoQuest, EntrarNoQuest>();
servicos.AddSingleton<IEntrarNaSala, EntrarNaSala>();
servicos.AddSingleton<IConfirmarSenha, ConfirmarSenha>();
servicos.AddSingleton<IRenovarSessao, RenovarSessao>();
servicos.AddSingleton<IConferirAcesso, ConferirAcesso>();
servicos.AddSingleton<ISairDoAparelho, SairDoAparelho>();
servicos.AddSingleton<IListarSessoes, ListarSessoes>();
servicos.AddSingleton<IEncerrarSessao, EncerrarSessao>();
servicos.AddSingleton<IEncerrarTodasAsSessoes, EncerrarTodasAsSessoes>();
servicos.AddSingleton<IEncerrarSessoesDoUsuario, EncerrarSessoesDoUsuario>();
servicos.AddSingleton<IAvisarPermissoesNovas, AvisarPermissoesNovas>();
servicos.AddSingleton<ILerChavesPublicas, LerChavesPublicas>();

// commons/usuario/usuarios
servicos.AddSingleton<ICriarConta, CriarConta>();
servicos.AddSingleton<ISugerirSigla, SugerirSigla>();
servicos.AddSingleton<IGerarSenha, GerarSenha>();
servicos.AddSingleton<IPedirAjudaDaMaster, PedirAjudaDaMaster>();
servicos.AddSingleton<IListarUsuarios, ListarUsuarios>();
servicos.AddSingleton<ILerUsuario, LerUsuario>();
servicos.AddSingleton<ICriarUsuario, CriarUsuario>();
servicos.AddSingleton<IEditarUsuario, EditarUsuario>();
servicos.AddSingleton<IPedirNovaGeracaoDeSenha, PedirNovaGeracaoDeSenha>();
servicos.AddSingleton<IGerarNovoCodigoDeAcesso, GerarNovoCodigoDeAcesso>();
servicos.AddSingleton<IDesativarUsuario, DesativarUsuario>();
servicos.AddSingleton<IReativarUsuario, ReativarUsuario>();
servicos.AddSingleton<ILerCardDeUsuarios, LerCardDeUsuarios>();
servicos.AddSingleton<ILerTempoDaConta, LerTempoDaConta>();
servicos.AddSingleton<ISalvarTempoDaConta, SalvarTempoDaConta>();
servicos.AddSingleton<IListarContas, ListarContas>();
servicos.AddSingleton<ILerConta, LerConta>();
servicos.AddSingleton<IMudarEstadoDaConta, MudarEstadoDaConta>();
servicos.AddSingleton<IGerarCodigoDaMaster, GerarCodigoDaMaster>();
servicos.AddSingleton<ILerTempoDoTime, LerTempoDoTime>();
servicos.AddSingleton<ISalvarTempoDoTime, SalvarTempoDoTime>();
servicos.AddSingleton<IInicializarContas, InicializarContas>();

// commons/usuario/autorizacao
servicos.AddSingleton<ICalcularPermissoes, CalcularPermissoes>();
servicos.AddSingleton<ICalcularPermissoesDoAnonimo, CalcularPermissoesDoAnonimo>();
servicos.AddSingleton<IAutorizarPedido, AutorizarPedido>();
servicos.AddSingleton<IAutorizarPedidoAuditado, AutorizarPedidoAuditado>();
servicos.AddSingleton<ILerPermissoes, LerPermissoes>();
servicos.AddSingleton<ISalvarPermissoes, SalvarPermissoes>();
servicos.AddSingleton<ILerHistoricoDePermissoes, LerHistoricoDePermissoes>();
servicos.AddSingleton<IAvisarContratacaoMudou, AvisarContratacaoMudou>();

// ── infra/in: a partida, antes do primeiro pedido ────────────────────────────
servicos.AddSingleton<Partida>();

var app = builder.Build();

app.UseRespostasDeErro();

// Documentação da API em /doc/api, só com Swagger:Habilitado (homologação e
// máquina; nunca em produção, 05_cd-implantacao RNF-73)
if (DocumentacaoApi.Habilitada(app.Configuration))
{
    app.UseDocumentacaoApi();
}

// ── infra/in ─────────────────────────────────────────────────────────────────
app.MapSaude();
app.MapAutenticacao();
app.MapUsuarios();
app.MapAutorizacao();

// A partida, antes de aceitar pedidos (usuario-inicializacao-sequencia): a
// configuração conferida, o registro de módulos, os índices e as contas
// iniciais do Key Vault (RN-190, RN-265 a RN-267). Segredo ausente ou senha
// fraca (RNF-226), índice único que não pôde ser criado (RNF-227) ou banco
// fora do ar: termina com código 1, sem /health.
app.Services.GetService<IStartupValidator>()?.Validate();
bool pronta;
try
{
    pronta = await app.Services.GetRequiredService<Partida>().Executar();
}
catch (Exception erro) when (erro is MongoDB.Driver.MongoException or TimeoutException)
{
    app.Logger.LogCritical("A aplicação não inicia: o banco não respondeu na partida ({Erro})", erro.GetType().Name);
    pronta = false;
}
if (!pronta)
{
    // o log do console é assíncrono: descartar a aplicação o esvazia
    await app.DisposeAsync();
    return 1;
}

app.Run();
return 0;

// Para os testes de integração (WebApplicationFactory<Program>)
public partial class Program;
