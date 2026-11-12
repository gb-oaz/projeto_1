using Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
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
    IAuditoria, IAuditoriaDeErros, IPedidosDeAjuda, IPagamentos
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
        ListarUsuarios = new ListarUsuarios(this, this, Relogio);
        LerUsuario = new LerUsuario(this, this, Relogio);
        AtualizarCardUsuarios = new AtualizarCardUsuarios(this, this, Cards, Cards, Relogio);
        ObterCardUsuarios = new ObterCardUsuarios(this, this, Cards, Cards, Relogio);
        FecharCardsDoDia = new FecharCardsDoDia(Cards, Cards, Cards, Relogio);
        AcompanharCardAoVivo = new AcompanharCardAoVivo(this, this, Cards, Cards, Cards, Cards, Relogio);
        ObterPreferencias = new ObterPreferencias(Cards);
        SalvarPreferencias = new SalvarPreferencias(Cards);
        CriarUsuario = new CriarUsuario(this, this, this, this, this, Relogio, AtualizarCardUsuarios);
        EditarUsuario = new EditarUsuario(this, this, this, this, this, Relogio, AtualizarCardUsuarios);
        GerarSenha = new GerarSenha(this, this, this, this, this, this, Relogio, AtualizarCardUsuarios);
        PedirNovaGeracaoDeSenha = new PedirNovaGeracaoDeSenha(this, this, this, this, this, this, Relogio, AtualizarCardUsuarios);
        GerarNovoCodigoDeAcesso = new GerarNovoCodigoDeAcesso(this, this, this, this, this, Relogio);
        DesativarUsuario = new DesativarUsuario(this, this, this, this, this, Relogio, AtualizarCardUsuarios);
        ReativarUsuario = new ReativarUsuario(this, this, this, this, Relogio, AtualizarCardUsuarios);
        CriarConta = new CriarConta(this, this, this, Relogio, new AmbienteDaAplicacao(ambiente), AtualizarCardUsuarios);
        SugerirSigla = new SugerirSigla(this);
        PedirAjudaDaMaster = new PedirAjudaDaMaster(this, this, this, Relogio);
        ListarContas = new ListarContas(this, this, Relogio);
        LerConta = new LerConta(this, this, this, Relogio);
        MudarEstadoDaConta = new MudarEstadoDaConta(this, this, this, this, this, Relogio, AtualizarCardUsuarios);
        GerarCodigoDaMaster = new GerarCodigoDaMaster(this, this, this, this, this, this, this, Relogio);
        LerTempoDaConta = new LerTempoDaConta(this, this, Relogio);
        SalvarTempoDaConta = new SalvarTempoDaConta(this, this, this, this, Relogio);
        LerTempoDoTime = new LerTempoDoTime(this, this, Relogio);
        SalvarTempoDoTime = new SalvarTempoDoTime(this, this, this, this, Relogio);
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
    /// <summary>Os cards guardados, a contagem, o ao vivo e as preferências, em memória.</summary>
    public CardsEmMemoria Cards { get; } = new();
    /// <summary>A recontagem na mesma execução.</summary>
    public IAtualizarCardUsuarios AtualizarCardUsuarios { get; }
    /// <summary>GET /usuarios/card.</summary>
    public IObterCardUsuarios ObterCardUsuarios { get; }
    /// <summary>O fechamento da 00:00.</summary>
    public IFecharCardsDoDia FecharCardsDoDia { get; }
    /// <summary>GET /usuarios/card/ao-vivo.</summary>
    public IAcompanharCardAoVivo AcompanharCardAoVivo { get; }
    /// <summary>GET /usuarios/eu/preferencias.</summary>
    public IObterPreferencias ObterPreferencias { get; }
    /// <summary>PUT /usuarios/eu/preferencias.</summary>
    public ISalvarPreferencias SalvarPreferencias { get; }
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

    /// <summary>A execução de quem chama, em cada pedido de encerrar sessões (RN-275).</summary>
    public List<ExecucaoDeQuemChama> Chamadas { get; } = [];

    /// <summary>
    /// Se não for null, o encerramento por usuário chega a este caso de uso da
    /// Autenticação, como o adaptador real (RN-275).
    /// </summary>
    public IEncerrarSessoesDoUsuario? Autenticacao { get; set; }

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

    /// <summary>Os pedidos que chegaram ao SAC, com o id gravado em "pedidosAjuda".</summary>
    public List<(string Id, string Aparelho, string Instituicao, string Contato, DateTimeOffset Quando)> PedidosDeAjuda { get; } = [];

    /// <summary>Se não for null, IPedidosDeAjuda.Enviar lança esta falha (o banco que cai no meio da gravação).</summary>
    public Exception? FalhaAoPedirAjuda { get; set; }

    /// <summary>Se não for null, IContas.Atualizar, SalvarTempoDaConta e SalvarTempoDoTime lançam esta falha (o banco que cai).</summary>
    public Exception? FalhaAoGravarConta { get; set; }

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
        if (FalhaAoGravarConta is { } falha) return Task.FromException(falha);
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
        if (FalhaAoGravarConta is { } falha) return Task.FromException(falha);
        temposDasContas[contaId] = tempo;
        return Task.CompletedTask;
    }

    Task<TempoDoTime?> IContas.TempoDoTime(CancellationToken ct) => Task.FromResult(tempoDoTime);

    Task IContas.SalvarTempoDoTime(TempoDoTime tempo, CancellationToken ct)
    {
        if (FalhaAoGravarConta is { } falha) return Task.FromException(falha);
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

    Task ISessoesDosUsuarios.EncerrarDoUsuario(string usuarioId, MotivoFim motivo, ExecucaoDeQuemChama chamada, CancellationToken ct)
    {
        SessoesEncerradas.Add((usuarioId, motivo));
        Chamadas.Add(chamada);
        return Autenticacao?.Executar(usuarioId, motivo, chamada, ct) ?? Task.CompletedTask;
    }

    Task ISessoesDosUsuarios.EncerrarDaConta(string contaId, MotivoFim motivo, ExecucaoDeQuemChama chamada, CancellationToken ct)
    {
        ContasSemSessao.Add((contaId, motivo));
        Chamadas.Add(chamada);
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

    Task<string> IPedidosDeAjuda.Enviar(string aparelho, string instituicao, string contato, DateTimeOffset quando, CancellationToken ct)
    {
        if (FalhaAoPedirAjuda is { } falha) return Task.FromException<string>(falha);
        var id = $"ajuda-{PedidosDeAjuda.Count + 1:000000}";
        PedidosDeAjuda.Add((id, aparelho, instituicao, contato, quando));
        return Task.FromResult(id);
    }

    Task<DateOnly?> IPagamentos.UltimoPagamento(string contaId, CancellationToken ct) =>
        Task.FromResult(pagamentos.TryGetValue(contaId, out var data) ? data : (DateOnly?)null);

    static bool Contem(string texto, string busca) => texto.Contains(busca, StringComparison.OrdinalIgnoreCase);

    static PaginaDe<T> Pagina<T>(List<T> todos, int pagina) =>
        new([.. todos.Skip((pagina - 1) * PaginaDe<T>.Tamanho).Take(PaginaDe<T>.Tamanho)], pagina, todos.Count);
}
