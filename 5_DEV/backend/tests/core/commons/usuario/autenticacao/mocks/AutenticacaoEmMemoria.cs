using System.Security.Cryptography;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;
using Boaz.Core.Commons.Usuario.Autenticacao.UseCases;
using EtapaDaExecucao = Boaz.Core.Commons.Usuario.Usuarios.Domain.EtapaDaExecucao;
using IAuditoria = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IAuditoria;
using IAuditoriaDeErros = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IAuditoriaDeErros;
using NivelDoErro = Boaz.Core.Commons.Usuario.Usuarios.Domain.NivelDoErro;
using RegistroDeAuditoria = Boaz.Core.Commons.Usuario.Usuarios.Domain.RegistroDeAuditoria;
using RegistroDeErro = Boaz.Core.Commons.Usuario.Usuarios.Domain.RegistroDeErro;

namespace Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks;

/// <summary>
/// Mock de todas as portas de saída da autenticação, em memória, já ligado aos
/// casos de uso. Imita o comportamento dos adaptadores reais: guarda cópias (o
/// que não for gravado não muda), e sessões e tentativas somem quando expiram,
/// como no Redis.
/// </summary>
public sealed class AutenticacaoEmMemoria :
    IUsuarios, IContas, IGeracoesDeSenha, ISessoes, ITentativas, IAssinador, IPermissoes, ISalas, IAuditoria, IAuditoriaDeErros
{
    /// <summary>Iterações baixas, só para os testes não demorarem.</summary>
    public const int Iteracoes = 1_000;

    /// <summary>Id da conta do time deboaz.</summary>
    public const string ContaDoTime = "conta-dboaz";

    readonly Dictionary<string, UsuarioDaEntrada> usuarios = [];
    readonly Dictionary<string, string> entradas = [];
    readonly Dictionary<string, string> codigos = [];
    readonly Dictionary<string, ContaDaEntrada> contas = [];
    readonly Dictionary<string, (GeracaoDeSenha Geracao, string Resumo)> geracoes = [];
    readonly Dictionary<string, Sessao> sessoes = [];
    readonly Dictionary<string, TentativasDeEntrada> tentativas = [];
    readonly Dictionary<string, RSA> chaves = [];
    readonly Dictionary<string, PermissoesNoToken> permissoes = [];
    readonly Dictionary<string, SalaAberta> salas = [];
    int acessosAnonimos;

    /// <summary>Cria o mock com a conta do time deboaz e a chave "deboaz-2026-11".</summary>
    public AutenticacaoEmMemoria()
    {
        NovaConta(ContaDoTime, "DBOAZ");
        TrocarChave("deboaz-2026-11");

        EntrarNoHub = new EntrarNoHub(this, this, this, this, this, this, this, this, Relogio);
        EntrarNoQuest = new EntrarNoQuest(this, this, this, this, this, this, this, this, Relogio);
        EntrarNaSala = new EntrarNaSala(this, this, this, this, this, this, Relogio);
        ConfirmarSenha = new ConfirmarSenha(this, this, this, this, this, this, this, this, Relogio);
        RenovarSessao = new RenovarSessao(this, this, this, this, this, this, this, this, Relogio);
        ConferirAcesso = new ConferirAcesso(this, this, Relogio);
        SairDoAparelho = new SairDoAparelho(this, this, this, Relogio);
        ListarSessoes = new ListarSessoes(ConferirAcesso, this, Relogio);
        EncerrarSessao = new EncerrarSessao(this, this, this, Relogio);
        EncerrarTodasAsSessoes = new EncerrarTodasAsSessoes(this, this, this, Relogio);
        EncerrarSessoesDoUsuario = new EncerrarSessoesDoUsuario(this);
        AvisarPermissoesNovas = new AvisarPermissoesNovas(this);
        LerChavesPublicas = new LerChavesPublicas(this);
    }

    // ── os casos de uso, como o ms_mono vai expô-los ───────────────────────────

    /// <summary>O relógio de todos os casos de uso.</summary>
    public RelogioDeTeste Relogio { get; } = new();

    /// <summary>POST /auth/hub/entrar.</summary>
    public IEntrarNoHub EntrarNoHub { get; }
    /// <summary>POST /auth/quest/entrar.</summary>
    public IEntrarNoQuest EntrarNoQuest { get; }
    /// <summary>POST /auth/sala/entrar.</summary>
    public IEntrarNaSala EntrarNaSala { get; }
    /// <summary>POST /auth/hub/confirmar-senha.</summary>
    public IConfirmarSenha ConfirmarSenha { get; }
    /// <summary>POST /auth/renovar.</summary>
    public IRenovarSessao RenovarSessao { get; }
    /// <summary>A conferência de cada pedido.</summary>
    public IConferirAcesso ConferirAcesso { get; }
    /// <summary>POST /auth/sair.</summary>
    public ISairDoAparelho SairDoAparelho { get; }
    /// <summary>GET /auth/sessoes.</summary>
    public IListarSessoes ListarSessoes { get; }
    /// <summary>DELETE /auth/sessoes/{sid}.</summary>
    public IEncerrarSessao EncerrarSessao { get; }
    /// <summary>DELETE /auth/sessoes.</summary>
    public IEncerrarTodasAsSessoes EncerrarTodasAsSessoes { get; }
    /// <summary>Para Usuários: encerrar as sessões de alguém.</summary>
    public IEncerrarSessoesDoUsuario EncerrarSessoesDoUsuario { get; }
    /// <summary>Para a Autorização: permissões novas.</summary>
    public IAvisarPermissoesNovas AvisarPermissoesNovas { get; }
    /// <summary>GET /auth/chaves.</summary>
    public ILerChavesPublicas LerChavesPublicas { get; }

    // ── preparo do cenário ─────────────────────────────────────────────────────

    /// <summary>Uma conta, ativa e com os tempos padrão se nada for dito.</summary>
    public ContaDaEntrada NovaConta(string id, string sigla, EstadoConta estado = EstadoConta.Ativa, TempoDeSessao? tempo = null) =>
        contas[id] = new ContaDaEntrada(id, sigla, estado, tempo ?? TempoDeSessao.Padrao);

    /// <summary>Muda o estado de uma conta (suspender, desativar…).</summary>
    public void MudarConta(string id, EstadoConta estado) => contas[id] = contas[id] with { Estado = estado };

    /// <summary>Um usuário com entrada e senha (sem senha: participante com código).</summary>
    public UsuarioDaEntrada NovoUsuario(string id, string contaId, TipoUsuario tipo, string? entrada = null, string? senha = null,
        string? codigo = null, EstadoUsuario estado = EstadoUsuario.Ativo)
    {
        var usuario = new UsuarioDaEntrada(id, contaId, contas[contaId].Sigla, tipo, estado,
            senha is null ? null : ResumoDeSenha.Gerar(senha, Iteracoes));
        if (entrada is not null) entradas[entrada] = id;
        if (codigo is not null) codigos[codigo] = id;
        permissoes.TryAdd(id, new PermissoesNoToken(tipo.Texto(), new Dictionary<string, string> { ["MOD-03"] = "CU" }, 1));
        return usuarios[id] = usuario;
    }

    /// <summary>Muda o estado de um usuário (desativar…).</summary>
    public void MudarUsuario(string id, EstadoUsuario estado) => usuarios[id] = usuarios[id] with { Estado = estado };

    /// <summary>Uma geração de senha pedida agora, com o comprovante em claro.</summary>
    public string NovaGeracao(string usuarioId)
    {
        var comprovante = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var id = $"ger-{geracoes.Count + 1}";
        geracoes[id] = (new GeracaoDeSenha(id, usuarioId, Relogio.Agora, null), Credenciais.Resumo(comprovante));
        return comprovante;
    }

    /// <summary>As permissões que a Autorização daria ao usuário.</summary>
    public void DefinirPermissoes(string usuarioId, PermissoesNoToken doUsuario) => permissoes[usuarioId] = doUsuario;

    /// <summary>Abre uma sala ao vivo.</summary>
    public void AbrirSala(string codigo, string contaId, string moduloId) =>
        salas[codigo] = new SalaAberta(codigo, contaId, contas[contaId].Sigla, moduloId);

    /// <summary>Fecha a sala.</summary>
    public void FecharSala(string codigo) => salas.Remove(codigo);

    /// <summary>Uma chave nova passa a assinar; as anteriores continuam aceitas.</summary>
    public void TrocarChave(string kid)
    {
        chaves[kid] = RSA.Create(2048);
        Kid = kid;
    }

    /// <summary>A chave deixa de ser aceita na hora (vazamento).</summary>
    public void RevogarChave(string kid) => chaves.Remove(kid);

    // ── o que os testes conferem ───────────────────────────────────────────────

    /// <summary>A chave que assina agora.</summary>
    public string Kid { get; private set; } = "";

    /// <summary>O último acesso gravado de cada usuário.</summary>
    public Dictionary<string, DateTimeOffset> UltimosAcessos { get; } = [];

    /// <summary>Quem entrou no último acesso de cada usuário (o parentesco do responsável).</summary>
    public Dictionary<string, string?> QuemDosUltimosAcessos { get; } = [];

    /// <summary>Por que cada sessão apagada terminou.</summary>
    public Dictionary<string, MotivoFim> Encerradas { get; } = [];

    /// <summary>Os registros de "auditoria" (RN-272), na ordem em que foram gravados.</summary>
    public List<RegistroDeAuditoria> Auditoria { get; } = [];

    /// <summary>Os registros de "auditoriaErros" (RN-273).</summary>
    public List<RegistroDeErro> Erros { get; } = [];

    /// <summary>O que foi gravado, em ordem: INICIADO, CONCLUIDO ou "nivel codigo" do erro.</summary>
    public List<string> Gravacoes { get; } = [];

    /// <summary>Simula o Redis fora do ar: as sessões e as tentativas falham com ela.</summary>
    public Exception? FalhaDoRedis { get; set; }

    /// <summary>As sessões que ainda existem.</summary>
    public IReadOnlyList<Sessao> Sessoes => sessoes.Values.Where(Viva).Select(Copia).ToList();

    /// <summary>O usuário como está guardado.</summary>
    public UsuarioDaEntrada Usuario(string id) => usuarios[id];

    /// <summary>As tentativas guardadas, se existirem.</summary>
    public TentativasDeEntrada? Tentativas(AlvoTentativa alvo, string chave) =>
        tentativas.GetValueOrDefault(Chave(alvo, chave)) is { } t && t.ExpiraEm > Relogio.Agora ? t : null;

    // ── IUsuarios ──────────────────────────────────────────────────────────────

    Task<UsuarioDaEntrada?> IUsuarios.BuscarPorEntrada(string entrada, CancellationToken ct) =>
        Task.FromResult(entradas.TryGetValue(entrada, out var id) ? usuarios[id] : null);

    Task<UsuarioDaEntrada?> IUsuarios.BuscarPorCodigoDeAcesso(string codigo, CancellationToken ct) =>
        Task.FromResult(codigos.TryGetValue(codigo, out var id) ? usuarios[id] : null);

    Task<UsuarioDaEntrada?> IUsuarios.Buscar(string id, CancellationToken ct) => Task.FromResult(usuarios.GetValueOrDefault(id));

    Task IUsuarios.RegistrarUltimoAcesso(string usuarioId, DateTimeOffset quando, string? quem, CancellationToken ct)
    {
        UltimosAcessos[usuarioId] = quando;
        QuemDosUltimosAcessos[usuarioId] = quem;
        return Task.CompletedTask;
    }

    // ── IContas ────────────────────────────────────────────────────────────────

    Task<ContaDaEntrada?> IContas.Buscar(string id, CancellationToken ct) => Task.FromResult(contas.GetValueOrDefault(id));

    Task<ContaDaEntrada> IContas.BuscarTimeDeboaz(CancellationToken ct) => Task.FromResult(contas[ContaDoTime]);

    // ── IGeracoesDeSenha ───────────────────────────────────────────────────────

    Task<GeracaoDeSenha?> IGeracoesDeSenha.BuscarPorComprovante(string resumo, CancellationToken ct) =>
        Task.FromResult(geracoes.Values.Where(g => g.Resumo == resumo).Select(g => g.Geracao).FirstOrDefault());

    Task IGeracoesDeSenha.MarcarConfirmada(string geracaoId, DateTimeOffset quando, CancellationToken ct)
    {
        var (geracao, resumo) = geracoes[geracaoId];
        geracoes[geracaoId] = (geracao with { ConfirmadaEm = quando }, resumo);
        if (usuarios[geracao.UsuarioId].Estado == EstadoUsuario.Pendente) MudarUsuario(geracao.UsuarioId, EstadoUsuario.Ativo);
        return Task.CompletedTask;
    }

    // ── ISessoes ───────────────────────────────────────────────────────────────

    Task<Sessao?> ISessoes.Buscar(string sid, CancellationToken ct) =>
        FalhaDoRedis is { } falha
            ? Task.FromException<Sessao?>(falha)
            : Task.FromResult(sessoes.TryGetValue(sid, out var s) && Viva(s) ? Copia(s) : null);

    Task ISessoes.Gravar(Sessao sessao, CancellationToken ct)
    {
        if (FalhaDoRedis is { } falha) return Task.FromException(falha);
        sessoes[sessao.Sid] = Copia(sessao);
        return Task.CompletedTask;
    }

    Task ISessoes.Apagar(Sessao sessao, MotivoFim motivo, CancellationToken ct)
    {
        if (sessoes.Remove(sessao.Sid)) Encerradas[sessao.Sid] = motivo;
        return Task.CompletedTask;
    }

    Task<IReadOnlyList<Sessao>> ISessoes.DoUsuario(string usuarioId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Sessao>>(Sessoes.Where(s => s.UsuarioId == usuarioId).ToList());

    // a chave some quando expira, como no Redis
    bool Viva(Sessao s) => s.ExpiraEm is not { } fim || fim > Relogio.Agora;

    static Sessao Copia(Sessao s) => new(s.Sid, s.UsuarioId, s.ContaId, s.Sigla, s.Frente, s.Aparelho, s.QuemEntrou,
        s.Sala, s.Pv, s.ResumoRenovacao, s.Inicio, s.UltimaAcao, s.TempoSemUso);

    // ── ITentativas ────────────────────────────────────────────────────────────

    Task<TentativasDeEntrada?> ITentativas.Buscar(AlvoTentativa alvo, string chave, CancellationToken ct) =>
        FalhaDoRedis is { } falha ? Task.FromException<TentativasDeEntrada?>(falha) : Task.FromResult(Tentativas(alvo, chave) is { } t
            ? new TentativasDeEntrada(t.Alvo, t.Chave, t.ErrosSeguidos, t.BloqueiosSeguidos, t.BloqueadoAte, t.UltimoErro)
            : null);

    Task ITentativas.Gravar(TentativasDeEntrada t, CancellationToken ct)
    {
        tentativas[Chave(t.Alvo, t.Chave)] =
            new TentativasDeEntrada(t.Alvo, t.Chave, t.ErrosSeguidos, t.BloqueiosSeguidos, t.BloqueadoAte, t.UltimoErro);
        return Task.CompletedTask;
    }

    Task ITentativas.Apagar(AlvoTentativa alvo, string chave, CancellationToken ct)
    {
        tentativas.Remove(Chave(alvo, chave));
        return Task.CompletedTask;
    }

    static string Chave(AlvoTentativa alvo, string chave) => $"tentativas:{alvo.Texto()}:{chave}";

    // ── IAuditoria e IAuditoriaDeErros ─────────────────────────────────────────

    Task IAuditoria.Registrar(RegistroDeAuditoria registro, CancellationToken ct)
    {
        Auditoria.Add(registro);
        Gravacoes.Add(registro.Etapa == EtapaDaExecucao.Iniciado ? "INICIADO" : "CONCLUIDO");
        return Task.CompletedTask;
    }

    Task IAuditoriaDeErros.Registrar(RegistroDeErro registro, CancellationToken ct)
    {
        Erros.Add(registro);
        Gravacoes.Add($"{(registro.Nivel == NivelDoErro.Alerta ? "ALERTA" : "ERRO")} {registro.Codigo}");
        return Task.CompletedTask;
    }

    // ── IAssinador ─────────────────────────────────────────────────────────────

    Task<string> IAssinador.KidAtual(CancellationToken ct) => Task.FromResult(Kid);

    Task<byte[]> IAssinador.Assinar(string kid, byte[] dados, CancellationToken ct) =>
        Task.FromResult(chaves[kid].SignData(dados, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

    Task<IReadOnlyList<ChavePublica>> IAssinador.ChavesAceitas(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ChavePublica>>(chaves.Select(c => ChavePublica.De(c.Key, c.Value)).ToList());

    // ── IPermissoes ────────────────────────────────────────────────────────────

    Task<PermissoesNoToken> IPermissoes.DoUsuario(UsuarioDaEntrada usuario, CancellationToken ct) =>
        Task.FromResult(permissoes[usuario.Id]);

    Task<PermissoesNoToken> IPermissoes.DoAnonimo(SalaAberta sala, CancellationToken ct) =>
        Task.FromResult(new PermissoesNoToken("ANONIMO", new Dictionary<string, string> { [sala.ModuloId] = "U" }, 0));

    // ── ISalas ─────────────────────────────────────────────────────────────────

    Task<SalaAberta?> ISalas.BuscarAberta(string codigo, CancellationToken ct) => Task.FromResult(salas.GetValueOrDefault(codigo));

    Task<AcessoAnonimo> ISalas.NovoAcessoAnonimo(SalaAberta sala, CancellationToken ct)
    {
        var numero = ++acessosAnonimos;
        return Task.FromResult(new AcessoAnonimo($"anon-{numero}", $"PUPIL-{sala.Sigla}-{13464 + numero}"));
    }
}
