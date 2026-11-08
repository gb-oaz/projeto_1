using Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using Boaz.Core.Commons.Usuario.Usuarios.UseCases;

namespace Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks;

/// <summary>
/// Mock de todas as portas de saída de Usuários, em memória, já ligado aos casos
/// de uso. Guarda o que os testes conferem: sessões encerradas, auditoria,
/// conferências do SAC, pedidos de ajuda e contas apagadas.
/// </summary>
public sealed class UsuariosEmMemoria : IUsuarios, IContas, IGeracoesDeSenha, ITentativasDeLiberacao, ISessoesDosUsuarios,
    IAuditoria, IAuditoriaDeErros, IPedidosDeAjuda, IPagamentos, IIndicadores
{
    readonly Dictionary<string, UsuarioCadastrado> usuarios = [];
    readonly Dictionary<string, ContaAssinante> contas = [];
    readonly List<GeracaoDeSenhaPedida> geracoes = [];
    readonly Dictionary<string, TentativasDeEntrada> tentativas = [];
    readonly Dictionary<string, TempoDaConta> temposDasContas = [];
    readonly Dictionary<string, DateOnly> pagamentos = [];
    TempoDoTime? tempoDoTime;

    /// <summary>Cria o mock e liga os casos de uso.</summary>
    /// <param name="ambiente">O AMBIENTE da aplicação (hml ou prd).</param>
    public UsuariosEmMemoria(string ambiente = "prd")
    {
        ListarUsuarios = new ListarUsuarios(this, this, this, Relogio);
        LerUsuario = new LerUsuario(this, this, this, Relogio);
        CriarUsuario = new CriarUsuario(this, this, this, this, this, Relogio);
        EditarUsuario = new EditarUsuario(this, this, this, this, Relogio);
        GerarSenha = new GerarSenha(this, this, this, this, this, Relogio);
        PedirNovaGeracaoDeSenha = new PedirNovaGeracaoDeSenha(this, this, this, this, this, this, Relogio);
        GerarNovoCodigoDeAcesso = new GerarNovoCodigoDeAcesso(this, this, this, this, Relogio);
        DesativarUsuario = new DesativarUsuario(this, this, this, this, this, Relogio);
        ReativarUsuario = new ReativarUsuario(this, this, this, Relogio);
        LerCardDeUsuarios = new LerCardDeUsuarios(this, this, this, this, Relogio);
        CriarConta = new CriarConta(this, this, this, Relogio, new AmbienteDaAplicacao(ambiente));
        SugerirSigla = new SugerirSigla(this);
        PedirAjudaDaMaster = new PedirAjudaDaMaster(this, Relogio);
        ListarContas = new ListarContas(this, this, this, Relogio);
        LerConta = new LerConta(this, this, this, this, Relogio);
        MudarEstadoDaConta = new MudarEstadoDaConta(this, this, this, this, Relogio);
        GerarCodigoDaMaster = new GerarCodigoDaMaster(this, this, this, this, this, this, Relogio);
        LerTempoDaConta = new LerTempoDaConta(this, this, this, Relogio);
        SalvarTempoDaConta = new SalvarTempoDaConta(this, this, this, Relogio);
        LerTempoDoTime = new LerTempoDoTime(this, this, this, Relogio);
        SalvarTempoDoTime = new SalvarTempoDoTime(this, this, this, Relogio);
        InicializarContas = new InicializarContas(this, this, Relogio);
    }

    /// <summary>O relógio dos casos de uso.</summary>
    public RelogioDeTeste Relogio { get; } = new();

    /// <summary>GET /usuarios.</summary>
    public IListarUsuarios ListarUsuarios { get; }
    /// <summary>GET /usuarios/{id}.</summary>
    public ILerUsuario LerUsuario { get; }
    /// <summary>POST /usuarios.</summary>
    public ICriarUsuario CriarUsuario { get; }
    /// <summary>PATCH /usuarios/{id}.</summary>
    public IEditarUsuario EditarUsuario { get; }
    /// <summary>POST /usuarios/gerar-senha.</summary>
    public IGerarSenha GerarSenha { get; }
    /// <summary>POST /usuarios/{id}/nova-geracao-senha.</summary>
    public IPedirNovaGeracaoDeSenha PedirNovaGeracaoDeSenha { get; }
    /// <summary>POST /usuarios/{id}/novo-codigo-acesso.</summary>
    public IGerarNovoCodigoDeAcesso GerarNovoCodigoDeAcesso { get; }
    /// <summary>POST /usuarios/{id}/desativar.</summary>
    public IDesativarUsuario DesativarUsuario { get; }
    /// <summary>POST /usuarios/{id}/reativar.</summary>
    public IReativarUsuario ReativarUsuario { get; }
    /// <summary>GET /usuarios/card.</summary>
    public ILerCardDeUsuarios LerCardDeUsuarios { get; }
    /// <summary>POST /contas.</summary>
    public ICriarConta CriarConta { get; }
    /// <summary>GET /contas/sigla-sugerida.</summary>
    public ISugerirSigla SugerirSigla { get; }
    /// <summary>POST /contas/esqueci-senha-master.</summary>
    public IPedirAjudaDaMaster PedirAjudaDaMaster { get; }
    /// <summary>GET /contas.</summary>
    public IListarContas ListarContas { get; }
    /// <summary>GET /contas/{id}.</summary>
    public ILerConta LerConta { get; }
    /// <summary>POST /contas/{id}/estado.</summary>
    public IMudarEstadoDaConta MudarEstadoDaConta { get; }
    /// <summary>POST /contas/{id}/codigo-master.</summary>
    public IGerarCodigoDaMaster GerarCodigoDaMaster { get; }
    /// <summary>GET /contas/{id}/tempo-sessao.</summary>
    public ILerTempoDaConta LerTempoDaConta { get; }
    /// <summary>PUT /contas/{id}/tempo-sessao.</summary>
    public ISalvarTempoDaConta SalvarTempoDaConta { get; }
    /// <summary>GET /time/tempo-sessao.</summary>
    public ILerTempoDoTime LerTempoDoTime { get; }
    /// <summary>PUT /time/tempo-sessao.</summary>
    public ISalvarTempoDoTime SalvarTempoDoTime { get; }
    /// <summary>A partida da aplicação.</summary>
    public IInicializarContas InicializarContas { get; }

    // ── preparo do cenário ─────────────────────────────────────────────────────

    /// <summary>Grava uma conta.</summary>
    public ContaAssinante Guardar(ContaAssinante conta) => contas[conta.Id] = conta;

    /// <summary>Grava um usuário.</summary>
    public UsuarioCadastrado Guardar(UsuarioCadastrado usuario) => usuarios[usuario.Id] = usuario;

    /// <summary>Grava uma geração de senha.</summary>
    public void Guardar(GeracaoDeSenhaPedida geracao) => geracoes.Add(geracao);

    /// <summary>O último pagamento de uma conta.</summary>
    public void Pagou(string contaId, DateOnly quando) => pagamentos[contaId] = quando;

    /// <summary>Ativos no fim de cada mês, por conta (ou "time" para as contas ativas).</summary>
    public Dictionary<string, IReadOnlyList<int>> Meses { get; } = [];

    /// <summary>Ativos hoje, por conta e tipo.</summary>
    public Dictionary<(string ContaId, TipoUsuario Tipo), int> Ativos { get; } = [];

    /// <summary>Códigos que vencem hoje, por conta.</summary>
    public Dictionary<string, int> Selos { get; } = [];

    // ── o que os testes conferem ───────────────────────────────────────────────

    /// <summary>Um usuário como está guardado.</summary>
    public UsuarioCadastrado? Usuario(string id) => usuarios.GetValueOrDefault(id);

    /// <summary>Os módulos e abas que a partida repôs em cada usuário qa (RN-268).</summary>
    public Dictionary<string, Dictionary<string, string>> ModulosRepostos { get; } = [];

    /// <summary>Os módulos em vigor que a partida deixou em cada conta qa (RN-268).</summary>
    public Dictionary<string, List<string>> Contratacoes { get; } = [];

    /// <summary>Uma conta como está guardada.</summary>
    public ContaAssinante? Conta(string id) => contas.GetValueOrDefault(id);

    /// <summary>As gerações de senha de um usuário, na ordem.</summary>
    public IReadOnlyList<GeracaoDeSenhaPedida> Geracoes(string usuarioId) => [.. geracoes.Where(g => g.UsuarioId == usuarioId)];

    /// <summary>As tentativas erradas guardadas de uma entrada.</summary>
    public TentativasDeEntrada? Tentativas(string entrada) => tentativas.GetValueOrDefault(entrada);

    /// <summary>O tempo guardado de uma conta.</summary>
    public TempoDaConta? Tempo(string contaId) => temposDasContas.GetValueOrDefault(contaId);

    /// <summary>Sessões encerradas por usuário, com o motivo.</summary>
    public List<(string UsuarioId, MotivoFim Motivo)> SessoesEncerradas { get; } = [];

    /// <summary>Sessões encerradas por conta, com o motivo.</summary>
    public List<(string ContaId, MotivoFim Motivo)> ContasSemSessao { get; } = [];

    /// <summary>A auditoria inteira.</summary>
    public List<RegistroDeAuditoria> Auditoria { get; } = [];

    /// <summary>Os registros de erro (auditoriaErros).</summary>
    public List<RegistroDeErro> Erros { get; } = [];

    /// <summary>
    /// Auditoria e erros na ordem em que foram gravados, como "INICIADO",
    /// "CONCLUIDO" ou "ALERTA SIGLA_EM_USO"; a recusa só sai do caso de uso depois.
    /// </summary>
    public List<string> Gravacoes { get; } = [];

    /// <summary>Se não for null, IContas.Criar lança esta falha (o banco que cai no meio da gravação).</summary>
    public Exception? FalhaAoCriarConta { get; set; }

    /// <summary>Se não for null, IUsuarios.Inserir e IUsuarios.Atualizar lançam esta falha (o banco que cai no meio da gravação).</summary>
    public Exception? FalhaAoGravarUsuario { get; set; }

    /// <summary>As conferências do SAC.</summary>
    public List<(string ContaId, string Quem, bool Conferiu)> Conferencias { get; } = [];

    /// <summary>Os pedidos que chegaram ao SAC.</summary>
    public List<(string Aparelho, string Instituicao, string Contato, DateTimeOffset Quando)> PedidosDeAjuda { get; } = [];

    /// <summary>As contas encerradas, com os dados apagados.</summary>
    public List<string> ContasApagadas { get; } = [];

    // ── IUsuarios ──────────────────────────────────────────────────────────────

    Task<UsuarioCadastrado?> IUsuarios.Buscar(string id, CancellationToken ct) => Task.FromResult(usuarios.GetValueOrDefault(id));

    Task<UsuarioCadastrado?> IUsuarios.BuscarPorEntrada(string entrada, CancellationToken ct) =>
        Task.FromResult(usuarios.Values.FirstOrDefault(u => string.Equals(u.Login, entrada, StringComparison.OrdinalIgnoreCase)));

    Task<UsuarioCadastrado?> IUsuarios.MasterDaConta(string contaId, CancellationToken ct) =>
        Task.FromResult(usuarios.Values.FirstOrDefault(u => u.ContaId == contaId && u.Tipo == TipoUsuario.Master));

    Task<bool> IUsuarios.NomeCurtoEmUso(string contaId, string nomeCurto, string? exceto, CancellationToken ct) =>
        Task.FromResult(usuarios.Values.Any(u => u.ContaId == contaId && u.NomeCurto == nomeCurto && u.Id != exceto));

    Task<bool> IUsuarios.MatriculaEmUso(string contaId, string matricula, string? exceto, CancellationToken ct) =>
        Task.FromResult(usuarios.Values.Any(u => u.ContaId == contaId && u.Matricula == matricula && u.Id != exceto));

    Task<bool> IUsuarios.LoginEmUso(string login, CancellationToken ct) =>
        Task.FromResult(usuarios.Values.Any(u => string.Equals(u.Login, login, StringComparison.OrdinalIgnoreCase)));

    Task IUsuarios.Inserir(UsuarioCadastrado usuario, CancellationToken ct)
    {
        if (FalhaAoGravarUsuario is { } falha) return Task.FromException(falha);
        usuarios.Add(usuario.Id, usuario);
        return Task.CompletedTask;
    }

    Task IUsuarios.Atualizar(UsuarioCadastrado usuario, CancellationToken ct)
    {
        if (FalhaAoGravarUsuario is { } falha) return Task.FromException(falha);
        usuarios[usuario.Id] = usuario;
        return Task.CompletedTask;
    }

    // upsert pelo id: o cadastro fica; a entrada, o resumo e o estado vêm de novo
    Task<bool> IUsuarios.Garantir(UsuarioCadastrado usuario, CancellationToken ct)
    {
        if (usuarios.GetValueOrDefault(usuario.Id) is not { } guardado)
        {
            usuarios.Add(usuario.Id, usuario);
            return Task.FromResult(true);
        }
        usuarios[usuario.Id] = new UsuarioCadastrado(guardado.Id, guardado.ContaId, guardado.Sigla, guardado.Tipo, guardado.Nome,
            guardado.NomeCurto, usuario.Login, guardado.Matricula, usuario.Estado, usuario.ResumoSenha, guardado.Responsaveis,
            guardado.CriadoPor, guardado.CriadoEm, guardado.CodigoGeradoEm, guardado.UltimoAcesso, guardado.UltimoAcessoQuem);
        return Task.FromResult(false);
    }

    Task IUsuarios.ReporModulos(string usuarioId, IReadOnlyDictionary<string, string> mods, CancellationToken ct)
    {
        ModulosRepostos[usuarioId] = new Dictionary<string, string>(mods);
        return Task.CompletedTask;
    }

    Task<PaginaDe<UsuarioCadastrado>> IUsuarios.Listar(FiltroDeUsuarios filtro, CancellationToken ct)
    {
        var achados = usuarios.Values
            .Where(u => u.ContaId == filtro.ContaId && filtro.Tipos.Contains(u.Tipo) && (filtro.Estado is null || u.Estado == filtro.Estado))
            .Where(u => filtro.Busca is not { } busca || Contem(u.Nome, busca) || Contem(u.Login, busca) || u.Matricula == busca)
            .OrderBy(u => u.Nome, StringComparer.Ordinal).ToList();
        return Task.FromResult(Pagina(achados, filtro.Pagina));
    }

    // ── IContas ────────────────────────────────────────────────────────────────

    Task<ContaAssinante?> IContas.Buscar(string id, CancellationToken ct) => Task.FromResult(contas.GetValueOrDefault(id));

    Task<bool> IContas.SiglaEmUso(string sigla, CancellationToken ct) =>
        Task.FromResult(sigla == Sigla.DoTime || contas.Values.Any(c => c.Sigla == sigla));

    Task<bool> IContas.DominioEmUso(string dominio, CancellationToken ct) => Task.FromResult(contas.Values.Any(c => c.Dominio == dominio));

    Task IContas.Criar(ContaAssinante conta, UsuarioCadastrado master, GeracaoDeSenhaPedida geracao, TempoDaConta tempo, CancellationToken ct)
    {
        if (FalhaAoCriarConta is { } falha) return Task.FromException(falha);
        contas.Add(conta.Id, conta);
        usuarios.Add(master.Id, master);
        geracoes.Add(geracao);
        temposDasContas[conta.Id] = tempo;
        return Task.CompletedTask;
    }

    Task<bool> IContas.Garantir(ContaAssinante conta, TempoDaConta? tempo, CancellationToken ct)
    {
        if (!contas.TryAdd(conta.Id, conta)) return Task.FromResult(false);
        if (tempo is not null) temposDasContas[conta.Id] = tempo;
        return Task.FromResult(true);
    }

    Task IContas.ReporContratacoes(ContaAssinante conta, IReadOnlySet<string> modulos, DateTimeOffset agora, CancellationToken ct)
    {
        Contratacoes[conta.Id] = [.. modulos.Order(StringComparer.Ordinal)];
        return Task.CompletedTask;
    }

    Task IContas.Atualizar(ContaAssinante conta, CancellationToken ct)
    {
        contas[conta.Id] = conta;
        return Task.CompletedTask;
    }

    Task IContas.Encerrar(string contaId, DateTimeOffset quando, CancellationToken ct)
    {
        ContasApagadas.Add(contaId);
        foreach (var id in usuarios.Values.Where(u => u.ContaId == contaId).Select(u => u.Id).ToList()) usuarios.Remove(id);
        return Task.CompletedTask;
    }

    Task<PaginaDe<ContaAssinante>> IContas.Listar(FiltroDeContas filtro, CancellationToken ct)
    {
        var achadas = contas.Values
            .Where(c => c.Sigla != Sigla.DoTime && (filtro.Estado is null || c.Estado == filtro.Estado))
            .Where(c => filtro.Busca is not { } busca || Contem(c.Nome, busca) || Contem(c.Sigla, busca)
                || (Documento.Numeros(busca) is { Length: > 0 } numeros && Documento.Numeros(c.Documento) == numeros))
            .OrderBy(c => c.Nome, StringComparer.Ordinal).ToList();
        return Task.FromResult(Pagina(achadas, filtro.Pagina));
    }

    Task<TempoDaConta?> IContas.TempoDaConta(string contaId, CancellationToken ct) => Task.FromResult(temposDasContas.GetValueOrDefault(contaId));

    Task IContas.SalvarTempoDaConta(string contaId, TempoDaConta tempo, CancellationToken ct)
    {
        temposDasContas[contaId] = tempo;
        return Task.CompletedTask;
    }

    Task<TempoDoTime?> IContas.TempoDoTime(CancellationToken ct) => Task.FromResult(tempoDoTime);

    Task IContas.SalvarTempoDoTime(TempoDoTime tempo, CancellationToken ct)
    {
        tempoDoTime = tempo;
        return Task.CompletedTask;
    }

    Task IContas.RegistrarConferencia(string contaId, string quem, bool conferiu, DateTimeOffset quando, CancellationToken ct)
    {
        Conferencias.Add((contaId, quem, conferiu));
        return Task.CompletedTask;
    }

    // ── IGeracoesDeSenha ───────────────────────────────────────────────────────

    Task IGeracoesDeSenha.Inserir(GeracaoDeSenhaPedida geracao, CancellationToken ct)
    {
        geracoes.Add(geracao);
        return Task.CompletedTask;
    }

    Task<GeracaoDeSenhaPedida?> IGeracoesDeSenha.UltimaDoUsuario(string usuarioId, CancellationToken ct) =>
        Task.FromResult(geracoes.LastOrDefault(g => g.UsuarioId == usuarioId));

    Task IGeracoesDeSenha.Atualizar(GeracaoDeSenhaPedida geracao, CancellationToken ct) => Task.CompletedTask;

    // ── ITentativasDeLiberacao ─────────────────────────────────────────────────

    Task<TentativasDeEntrada?> ITentativasDeLiberacao.Buscar(string entrada, CancellationToken ct) =>
        Task.FromResult(tentativas.GetValueOrDefault(entrada));

    Task ITentativasDeLiberacao.Gravar(TentativasDeEntrada registro, CancellationToken ct)
    {
        tentativas[registro.Chave] = registro;
        return Task.CompletedTask;
    }

    Task ITentativasDeLiberacao.Apagar(string entrada, CancellationToken ct)
    {
        tentativas.Remove(entrada);
        return Task.CompletedTask;
    }

    // ── ISessoesDosUsuarios e IAuditoria ───────────────────────────────────────

    Task ISessoesDosUsuarios.EncerrarDoUsuario(string usuarioId, MotivoFim motivo, CancellationToken ct)
    {
        SessoesEncerradas.Add((usuarioId, motivo));
        return Task.CompletedTask;
    }

    Task ISessoesDosUsuarios.EncerrarDaConta(string contaId, MotivoFim motivo, CancellationToken ct)
    {
        ContasSemSessao.Add((contaId, motivo));
        return Task.CompletedTask;
    }

    Task IAuditoria.Registrar(RegistroDeAuditoria registro, CancellationToken ct)
    {
        Auditoria.Add(registro);
        Gravacoes.Add(registro.Etapa.Texto());
        return Task.CompletedTask;
    }

    Task IAuditoriaDeErros.Registrar(RegistroDeErro registro, CancellationToken ct)
    {
        Erros.Add(registro);
        Gravacoes.Add($"{registro.Nivel.Texto()} {registro.Codigo}");
        return Task.CompletedTask;
    }

    // ── IPedidosDeAjuda e IPagamentos ──────────────────────────────────────────

    Task<IReadOnlyList<DateTimeOffset>> IPedidosDeAjuda.EnviosDoAparelho(string aparelho, DateTimeOffset desde, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DateTimeOffset>>([.. PedidosDeAjuda.Where(p => p.Aparelho == aparelho && p.Quando > desde).Select(p => p.Quando)]);

    Task IPedidosDeAjuda.Enviar(string aparelho, string instituicao, string contato, DateTimeOffset quando, CancellationToken ct)
    {
        PedidosDeAjuda.Add((aparelho, instituicao, contato, quando));
        return Task.CompletedTask;
    }

    Task<DateOnly?> IPagamentos.UltimoPagamento(string contaId, CancellationToken ct) =>
        Task.FromResult(pagamentos.TryGetValue(contaId, out var data) ? data : (DateOnly?)null);

    // ── IIndicadores ───────────────────────────────────────────────────────────

    Task<IReadOnlyList<int>> IIndicadores.UsuariosAtivosPorMes(string contaId, IReadOnlySet<TipoUsuario> tipos, DateTimeOffset agora, int meses,
        CancellationToken ct) => Task.FromResult(Meses.GetValueOrDefault(contaId) ?? []);

    Task<int> IIndicadores.UsuariosAtivos(string contaId, TipoUsuario tipo, CancellationToken ct) =>
        Task.FromResult(Ativos.GetValueOrDefault((contaId, tipo)));

    Task<IReadOnlyList<int>> IIndicadores.ContasAtivasPorMes(DateTimeOffset agora, int meses, CancellationToken ct) =>
        Task.FromResult(Meses.GetValueOrDefault("time") ?? []);

    Task<int> IIndicadores.CodigosQueVencem(string contaId, DateTimeOffset de, DateTimeOffset ate, CancellationToken ct) =>
        Task.FromResult(Selos.GetValueOrDefault(contaId));

    static bool Contem(string texto, string busca) => texto.Contains(busca, StringComparison.OrdinalIgnoreCase);

    static PaginaDe<T> Pagina<T>(List<T> todos, int pagina) =>
        new([.. todos.Skip((pagina - 1) * PaginaDe<T>.Tamanho).Take(PaginaDe<T>.Tamanho)], pagina, todos.Count);
}
