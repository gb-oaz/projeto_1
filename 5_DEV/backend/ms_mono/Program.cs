using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.UseCases;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autorizacao.UseCases;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using Boaz.Core.Commons.Usuario.Usuarios.UseCases;
using Boaz.Core.Hub.Sac.Anexos.Ports.In;
using Boaz.Core.Hub.Sac.Anexos.UseCases;
using Boaz.Core.Hub.Sac.Card.Ports.In;
using Boaz.Core.Hub.Sac.Card.UseCases;
using Boaz.Core.Hub.Sac.Chamados.Ports.In;
using Boaz.Core.Hub.Sac.Chamados.UseCases;
using Boaz.Core.Hub.Sac.Configuracao.Ports.In;
using Boaz.Core.Hub.Sac.Configuracao.UseCases;
using Boaz.MsMono.Primario.In.Commons;
using Boaz.MsMono.Primario.In.Hub.Sac;
using Boaz.MsMono.Primario.In.Commons.Usuario.Usuarios;
using Boaz.MsMono.Secundario.In.Commons.Usuario.Usuarios;
using Boaz.MsMono.Secundario.In.Hub.Sac;
using Boaz.MsMono.Secundario.Out.Commons;
using Boaz.MsMono.Secundario.Out.Commons.Auditoria;
using Boaz.MsMono.Secundario.Out.Commons.Usuario;
using Boaz.MsMono.Secundario.Out.Commons.Usuario.Autenticacao;
using Boaz.MsMono.Secundario.Out.Commons.Usuario.Autorizacao;
using Boaz.MsMono.Secundario.Out.Commons.Usuario.Usuarios;
using Boaz.MsMono.Secundario.Out.Hub.Sac;
using Boaz.MsMono.Sistema;
using Microsoft.Extensions.Options;
using Autenticacao = Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;
using Autorizacao = Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;
using SacAnexos = Boaz.Core.Hub.Sac.Anexos.Ports.Out;
using SacCard = Boaz.Core.Hub.Sac.Card.Ports.Out;
using SacChamados = Boaz.Core.Hub.Sac.Chamados.Ports.Out;
using SacConfiguracao = Boaz.Core.Hub.Sac.Configuracao.Ports.Out;

// Ponto de composição do backend (RN-149): aqui, e só aqui, as portas do
// core/ são ligadas aos adaptadores primários (primario/in e primario/out) e
// secundários (secundario/in e secundario/out).
var builder = WebApplication.CreateBuilder(args);
var servicos = builder.Services;

servicos.AddConfiguracoes();
servicos.AddDocumentacaoApi();
servicos.AddJsonDasApis();
servicos.AddControladoresDasApis();
// o relógio de toda a aplicação; só o relógio de teste (sistema/TesteController) o desloca
servicos.AddSingleton<RelogioDaAplicacao>();
servicos.AddSingleton<TimeProvider>(s => s.GetRequiredService<RelogioDaAplicacao>());
// AMBIENTE (hml ou prd): só homologação tem as contas qa (17 RN-179, RN-266)
servicos.AddSingleton(new Boaz.Core.Commons.Usuario.Usuarios.Domain.AmbienteDaAplicacao(
    builder.Configuration[Boaz.Core.Commons.Usuario.Usuarios.Domain.ContasIniciais.VariavelDoAmbiente]));

// ── secundario/out: banco, cache, índices e o registro de módulos ────────────
servicos.AddSingleton<Armazenamento>();
servicos.AddSingleton<Indices>();
servicos.AddSingleton<RegistroDeModulos>();

// commons/auditoria: auditoria e auditoriaErros, de todos os módulos
servicos.AddSingleton<AuditoriaNoBanco>();
servicos.AddSingleton<IAuditoria>(s => s.GetRequiredService<AuditoriaNoBanco>());
servicos.AddSingleton<IAuditoriaDeErros>(s => s.GetRequiredService<AuditoriaNoBanco>());

// commons/usuario/usuarios
servicos.AddSingleton<IUsuarios, UsuariosNoBanco>();
servicos.AddSingleton<IContas, ContasNoBanco>();
servicos.AddSingleton<IGeracoesDeSenha, GeracoesNoBanco>();
servicos.AddSingleton<RegistrosNoBanco>();
// os pedidos de ajuda sem login viram chamados do SAC (18 RF-179)
servicos.AddSingleton<IPedidosDeAjuda, PedidosDeAjudaNoSac>();
servicos.AddSingleton<IPagamentos>(s => s.GetRequiredService<RegistrosNoBanco>());
servicos.AddSingleton<IRepositorioCardUsuarios, CardsNoBanco>();
servicos.AddSingleton<IContagemDoCard, ContagemNoBanco>();
servicos.AddSingleton<IPreferencias, PreferenciasNoBanco>();
servicos.AddSingleton<EncerramentosPeloRedis>();
servicos.AddSingleton<IConexoesAoVivo, ConexoesNoRedis>();
servicos.AddSingleton<ITravaDoFechamento, TravaNoRedis>();
servicos.AddSingleton<IAcessoAoVivo, AcessoDoAoVivo>();
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
servicos.AddSingleton<ContratacoesDeTeste>();
servicos.AddSingleton<Autorizacao.ISessoesDoUsuario, AvisoDePermissoes>();

// hub/sac (sac-classes v04, sac-hexagonal v03): chamados, anexos, prazos e card
servicos.AddSingleton<ChamadosNoBanco>();
servicos.AddSingleton<SacChamados.IChamados>(s => s.GetRequiredService<ChamadosNoBanco>());
servicos.AddSingleton<SacChamados.INumeracao>(s => s.GetRequiredService<ChamadosNoBanco>());
servicos.AddSingleton<AvisosDoChamadoPeloRedis>();
servicos.AddSingleton<SacChamados.ICanalDoChamado, CanalDoChamadoNoRedis>();
servicos.AddSingleton<SacChamados.IPessoasDoSac, PessoasDoSacNoBanco>();
// o AMBIENTE lido na resolução, como o relógio: o expiraEm do anexo usado é por ambiente (18 RNF-234)
servicos.AddSingleton(s => new AnexosNoBanco(s.GetRequiredService<Armazenamento>(), s.GetRequiredService<SacAnexos.IArquivos>(),
    new Boaz.Core.Commons.Usuario.Usuarios.Domain.AmbienteDaAplicacao(
        s.GetRequiredService<IConfiguration>()[Boaz.Core.Commons.Usuario.Usuarios.Domain.ContasIniciais.VariavelDoAmbiente])));
servicos.AddSingleton<SacAnexos.IAnexos>(s => s.GetRequiredService<AnexosNoBanco>());
servicos.AddSingleton<SacChamados.IAnexosDoChamado>(s => s.GetRequiredService<AnexosNoBanco>());
// os arquivos dos anexos: gravados no destino (Sac:Arquivos:Destino, BLOB ou
// GRIDFS) e lidos de onde cada um foi guardado (18 RNF-233); o Blob só existe
// com a conta configurada (ConnectionStrings:Blob ou Sac:Arquivos:Blob)
servicos.AddSingleton<ArquivosNoGridFs>();
servicos.AddSingleton(s => new ArquivosDosAnexos(s.GetRequiredService<ArquivosNoGridFs>(),
    ArquivosNoBlob.Da(builder.Configuration, s.GetRequiredService<TimeProvider>()), ArquivosDosAnexos.DestinoDa(builder.Configuration)));
servicos.AddSingleton<SacAnexos.IArquivos>(s => s.GetRequiredService<ArquivosDosAnexos>());
// a verificação dos anexos: o clamd em Sac:ClamAv; sem ele, o dublê em
// homologação e falha fechada fora dela; o interruptor é do relógio de teste
servicos.AddSingleton(s => new InterruptorDaVerificacao(InterruptorDaVerificacao.Escolher(builder.Configuration,
    s.GetRequiredService<Boaz.Core.Commons.Usuario.Usuarios.Domain.AmbienteDaAplicacao>().EmHomologacao)));
servicos.AddSingleton<SacAnexos.IVerificacaoDeArquivos>(s => s.GetRequiredService<InterruptorDaVerificacao>());
servicos.AddSingleton<PrazosNoBanco>();
servicos.AddSingleton<SacConfiguracao.IConfiguracaoDoSac>(s => s.GetRequiredService<PrazosNoBanco>());
servicos.AddSingleton<SacChamados.IPrazosDoSac>(s => s.GetRequiredService<PrazosNoBanco>());
servicos.AddSingleton<CardDoSacNoBanco>();
servicos.AddSingleton<SacCard.IContagemDoSac>(s => s.GetRequiredService<CardDoSacNoBanco>());
servicos.AddSingleton<SacCard.ISeriesDoSac>(s => s.GetRequiredService<CardDoSacNoBanco>());
servicos.AddSingleton<TravaDoAgendamentoNoRedis>();

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
servicos.AddSingleton<IObterCardUsuarios, ObterCardUsuarios>();
servicos.AddSingleton<IAtualizarCardUsuarios, AtualizarCardUsuarios>();
servicos.AddSingleton<IFecharCardsDoDia, FecharCardsDoDia>();
servicos.AddSingleton<IAcompanharCardAoVivo, AcompanharCardAoVivo>();
servicos.AddSingleton<IObterPreferencias, ObterPreferencias>();
servicos.AddSingleton<ISalvarPreferencias, SalvarPreferencias>();
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

// hub/sac
servicos.AddSingleton<IAbrirChamado, AbrirChamado>();
servicos.AddSingleton<IListarChamados, ListarChamados>();
servicos.AddSingleton<ILerChamado, LerChamado>();
servicos.AddSingleton<IEnviarMensagem, EnviarMensagem>();
servicos.AddSingleton<IAssumirChamado, AssumirChamado>();
servicos.AddSingleton<IMudarStatus, MudarStatus>();
servicos.AddSingleton<IReabrirChamado, ReabrirChamado>();
servicos.AddSingleton<IEncerrarChamado, EncerrarChamado>();
servicos.AddSingleton<IAvaliarChamado, AvaliarChamado>();
servicos.AddSingleton<IAcompanharChamado, AcompanharChamado>();
servicos.AddSingleton<IRegistrarPedidoDeSenha, RegistrarPedidoDeSenha>();
servicos.AddSingleton<IAplicarPrazos, AplicarPrazos>();
servicos.AddSingleton<IEnviarAnexo, EnviarAnexo>();
servicos.AddSingleton<ILerAnexo, LerAnexo>();
servicos.AddSingleton<IDescartarAnexosVencidos, DescartarAnexosVencidos>();
servicos.AddSingleton<ILerPrazos, LerPrazos>();
servicos.AddSingleton<ISalvarPrazos, SalvarPrazos>();
servicos.AddSingleton<ILerCardDoSac, LerCardDoSac>();
servicos.AddSingleton<IAcompanharCardDoSac, AcompanharCardDoSac>();
servicos.AddSingleton<IFecharCardDoSacDoDia, FecharCardDoSacDoDia>();

// ── primario: a partida, antes do primeiro pedido ────────────────────────────
servicos.AddSingleton<Partida>();
// o fechamento da 00:00 de Brasília, em segundo plano (15 RN-278)
servicos.AddHostedService<FechamentoDosCards>();
// os prazos, os anexos e o card do SAC, a cada 1 min (18 RN-290, RN-292, RN-295)
servicos.AddHostedService<AgendamentoDoSac>();

var app = builder.Build();

app.UseRespostasDeErro();

// Documentação da API em /doc/api, só com Swagger:Habilitado (homologação e
// máquina; nunca em produção, 05_cd-implantacao RNF-73)
if (DocumentacaoApi.Habilitada(app.Configuration))
{
    app.UseDocumentacaoApi();
}

// ── primario/in: os controllers (sistema/ e primario/in/<contexto>/<feature>/) ─
app.MapControllers();

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
