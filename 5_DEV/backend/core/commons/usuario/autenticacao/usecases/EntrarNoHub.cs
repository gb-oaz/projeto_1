using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;
using AcaoAuditada = Boaz.Core.Commons.Usuario.Usuarios.Domain.AcaoAuditada;
using AuditoriaDaExecucao = Boaz.Core.Commons.Usuario.Usuarios.UseCases.AuditoriaDaExecucao;
using Execucao = Boaz.Core.Commons.Usuario.Usuarios.UseCases.Execucao;
using ExecucaoEmAndamento = Boaz.Core.Commons.Usuario.Usuarios.UseCases.ExecucaoEmAndamento;
using IAuditoria = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IAuditoria;
using IAuditoriaDeErros = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IAuditoriaDeErros;
using TraceId = Boaz.Core.Commons.Usuario.Usuarios.Domain.TraceId;

namespace Boaz.Core.Commons.Usuario.Autenticacao.UseCases;

/// <summary>
/// Entrar no hub com a entrada e a senha (RF-134). Ordem: bloqueio (RF-158) →
/// credencial, com a mesma resposta para entrada inexistente (RF-139) →
/// disponibilidade do usuário e da conta (RF-140) → sessão. Auditado com o
/// traceId recebido, ação ENTRAR_HUB: "INICIADO" sem quem, conta nem alvo; a
/// pessoa só é identificada depois da senha conferida, e o "CONCLUIDO" vai com
/// ela, a conta e a sessão (RN-272, RN-273). Nenhum segredo nos registros.
/// </summary>
public sealed class EntrarNoHub : IEntrarNoHub
{
    // Resumo de uma senha qualquer, conferido quando a entrada não existe, para o
    // tempo da resposta não revelar se ela existe.
    static readonly Lazy<string> ResumoDeEngano = new(() => ResumoDeSenha.Gerar(Guid.NewGuid().ToString()));

    readonly IUsuarios usuarios;
    readonly ContadorDeTentativas tentativas;
    readonly AberturaDeSessao abertura;
    readonly AuditoriaDaExecucao auditada;

    /// <summary>Monta o caso de uso com as portas de saída.</summary>
    public EntrarNoHub(IUsuarios usuarios, IContas contas, ISessoes sessoes, ITentativas tentativas,
        IPermissoes permissoes, IAssinador assinador, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    {
        this.usuarios = usuarios;
        this.tentativas = new ContadorDeTentativas(tentativas, relogio);
        abertura = new AberturaDeSessao(usuarios, contas, sessoes, permissoes, new EmissorDeTokens(sessoes, assinador), relogio);
        auditada = new AuditoriaDaExecucao(auditoria, erros, relogio);
    }

    /// <inheritdoc />
    public Task<SessaoAbertaDto> Executar(EntradaNoHubDto pedido, TraceId traceId, CancellationToken ct = default) =>
        auditada.Auditar(new Execucao(traceId, AcaoAuditada.EntrarHub, null, null, null), andamento => Entrar(pedido, andamento, ct), ct);

    /// <summary>A regra da entrada, da credencial ao "CONCLUIDO".</summary>
    async Task<SessaoAbertaDto> Entrar(EntradaNoHubDto pedido, ExecucaoEmAndamento andamento, CancellationToken ct)
    {
        var (entrada, senha, aparelho) = pedido.Validar();
        var usuario = await Identificar(entrada, senha, ct);
        andamento.Identificar(usuario.ContaId, usuario.Id);
        var conta = await abertura.ContaDisponivel(usuario, primeiroAcesso: false, ct);
        var aberta = await abertura.Abrir(usuario, conta, Frente.Hub, aparelho, null, ct);
        await RegistrarConclusao(andamento, aberta, ct);
        return aberta.Resposta;
    }

    /// <summary>O "CONCLUIDO" com a pessoa identificada, a conta e a sessão aberta como alvo (RN-272).</summary>
    Task RegistrarConclusao(ExecucaoEmAndamento andamento, SessaoEmitida aberta, CancellationToken ct) =>
        auditada.Concluir(andamento.Atual with { Alvo = aberta.Sessao.Sid }, ct);

    /// <summary>
    /// O staff da entrada, se a senha confere: bloqueio primeiro (RF-158), a
    /// mesma recusa para a entrada inexistente (RF-139) e o erro contado (RNF-204).
    /// </summary>
    /// <exception cref="FalhaDeAutenticacao">ENTRADA_BLOQUEADA ou ENTRADA_INVALIDA.</exception>
    async Task<UsuarioDaEntrada> Identificar(string entrada, string senha, CancellationToken ct)
    {
        await tentativas.ConferirBloqueio(AlvoTentativa.Entrada, entrada, ct);
        var usuario = await usuarios.BuscarPorEntrada(entrada, ct);
        var confere = ResumoDeSenha.Confere(senha, usuario?.ResumoSenha ?? ResumoDeEngano.Value);
        if (usuario is null || usuario.Tipo == TipoUsuario.Participante || !confere)
        {
            throw await tentativas.Errar(AlvoTentativa.Entrada, entrada, FalhaDeAutenticacao.EntradaInvalida(), ct);
        }
        await tentativas.Acertar(AlvoTentativa.Entrada, entrada, ct);
        return usuario;
    }
}
