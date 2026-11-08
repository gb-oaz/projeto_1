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
/// Primeiro acesso (RF-143): o comprovante recebido com a senha gerada prova que
/// ela foi anotada e abre a sessão do hub. Vale uma vez e por 72 h (RN-182).
/// Auditado com o traceId recebido, ação CONFIRMAR_SENHA: "INICIADO" sem quem,
/// conta nem alvo; a pessoa é identificada pelo comprovante válido, e o
/// "CONCLUIDO" vai com ela, a conta e a sessão (RN-272, RN-273). O comprovante
/// não vai nos registros.
/// </summary>
public sealed class ConfirmarSenha : IConfirmarSenha
{
    /// <summary>Validade da geração de senha.</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromHours(72);

    readonly IUsuarios usuarios;
    readonly IGeracoesDeSenha geracoes;
    readonly AberturaDeSessao abertura;
    readonly AuditoriaDaExecucao auditada;
    readonly TimeProvider relogio;

    /// <summary>Monta o caso de uso com as portas de saída.</summary>
    public ConfirmarSenha(IUsuarios usuarios, IContas contas, IGeracoesDeSenha geracoes, ISessoes sessoes,
        IPermissoes permissoes, IAssinador assinador, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    {
        this.usuarios = usuarios;
        this.geracoes = geracoes;
        this.relogio = relogio;
        abertura = new AberturaDeSessao(usuarios, contas, sessoes, permissoes, new EmissorDeTokens(sessoes, assinador), relogio);
        auditada = new AuditoriaDaExecucao(auditoria, erros, relogio);
    }

    /// <inheritdoc />
    public Task<SessaoAbertaDto> Executar(ConfirmacaoDeSenhaDto pedido, TraceId traceId, CancellationToken ct = default) =>
        auditada.Auditar(new Execucao(traceId, AcaoAuditada.ConfirmarSenha, null, null, null), andamento => Confirmar(pedido, andamento, ct), ct);

    /// <summary>A regra da confirmação, do comprovante ao "CONCLUIDO".</summary>
    async Task<SessaoAbertaDto> Confirmar(ConfirmacaoDeSenhaDto pedido, ExecucaoEmAndamento andamento, CancellationToken ct)
    {
        var (comprovante, aparelho) = pedido.Validar();
        var agora = relogio.GetUtcNow();
        var geracao = await GeracaoValida(comprovante, agora, ct);
        var usuario = await UsuarioDa(geracao, ct);
        andamento.Identificar(usuario.ContaId, usuario.Id);
        var conta = await abertura.ContaDisponivel(usuario, primeiroAcesso: true, ct);

        var aberta = await AbrirPrimeiraSessao(geracao, usuario, conta, aparelho, agora, ct);
        await RegistrarConclusao(andamento, aberta, ct);
        return aberta.Resposta;
    }

    /// <summary>O usuário da geração; sem ele, o comprovante não vale.</summary>
    /// <exception cref="FalhaDeAutenticacao">COMPROVANTE_INVALIDO.</exception>
    async Task<UsuarioDaEntrada> UsuarioDa(GeracaoDeSenha geracao, CancellationToken ct) =>
        await usuarios.Buscar(geracao.UsuarioId, ct) ?? throw FalhaDeAutenticacao.ComprovanteInvalido();

    /// <summary>Marca a geração como confirmada, para o comprovante valer uma vez só, e abre a sessão do hub (RN-182).</summary>
    async Task<SessaoEmitida> AbrirPrimeiraSessao(GeracaoDeSenha geracao, UsuarioDaEntrada usuario, ContaDaEntrada conta, Aparelho aparelho,
        DateTimeOffset agora, CancellationToken ct)
    {
        await geracoes.MarcarConfirmada(geracao.Id, agora, ct);
        return await abertura.Abrir(usuario, conta, Frente.Hub, aparelho, null, ct);
    }

    /// <summary>O "CONCLUIDO" com a pessoa identificada, a conta e a sessão aberta como alvo (RN-272).</summary>
    Task RegistrarConclusao(ExecucaoEmAndamento andamento, SessaoEmitida aberta, CancellationToken ct) =>
        auditada.Concluir(andamento.Atual with { Alvo = aberta.Sessao.Sid }, ct);

    /// <summary>A geração do comprovante, se ainda não foi confirmada e está dentro das 72 h (RN-182).</summary>
    /// <exception cref="FalhaDeAutenticacao">COMPROVANTE_INVALIDO.</exception>
    async Task<GeracaoDeSenha> GeracaoValida(string comprovante, DateTimeOffset agora, CancellationToken ct)
    {
        var geracao = await geracoes.BuscarPorComprovante(Credenciais.Resumo(comprovante), ct);
        return geracao is null || geracao.ConfirmadaEm is not null || agora >= geracao.PedidaEm + Validade
            ? throw FalhaDeAutenticacao.ComprovanteInvalido()
            : geracao;
    }
}
