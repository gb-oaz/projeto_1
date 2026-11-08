using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;
using AcaoAuditada = Boaz.Core.Commons.Usuario.Usuarios.Domain.AcaoAuditada;
using AuditoriaDaExecucao = Boaz.Core.Commons.Usuario.Usuarios.UseCases.AuditoriaDaExecucao;
using Execucao = Boaz.Core.Commons.Usuario.Usuarios.UseCases.Execucao;
using IAuditoria = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IAuditoria;
using IAuditoriaDeErros = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IAuditoriaDeErros;
using TraceId = Boaz.Core.Commons.Usuario.Usuarios.Domain.TraceId;

namespace Boaz.Core.Commons.Usuario.Autenticacao.UseCases;

/// <summary>
/// Troca o token de renovação por tokens novos (RF-137, RNF-213). A sessão
/// precisa estar ativa e o token ser o último entregue: um já trocado encerra a
/// sessão. As permissões são recalculadas (RN-230), e o usuário e a conta são
/// conferidos de novo: fora de uso, a sessão termina. Auditado com o traceId
/// recebido, ação RENOVAR_SESSAO: "INICIADO" sem quem, conta nem alvo, e toda
/// recusa também sem eles; o "CONCLUIDO" vai com a pessoa, a conta e a sessão
/// (RN-272, RN-273). O token de renovação não vai nos registros.
/// </summary>
public sealed class RenovarSessao : IRenovarSessao
{
    readonly IUsuarios usuarios;
    readonly IContas contas;
    readonly ISessoes sessoes;
    readonly IPermissoes permissoes;
    readonly ISalas salas;
    readonly EmissorDeTokens emissor;
    readonly AuditoriaDaExecucao auditada;
    readonly TimeProvider relogio;

    /// <summary>Monta o caso de uso com as portas de saída.</summary>
    public RenovarSessao(IUsuarios usuarios, IContas contas, ISessoes sessoes, IPermissoes permissoes, ISalas salas,
        IAssinador assinador, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    {
        this.usuarios = usuarios;
        this.contas = contas;
        this.sessoes = sessoes;
        this.permissoes = permissoes;
        this.salas = salas;
        this.relogio = relogio;
        emissor = new EmissorDeTokens(sessoes, assinador);
        auditada = new AuditoriaDaExecucao(auditoria, erros, relogio);
    }

    /// <inheritdoc />
    public Task<SessaoAbertaDto> Executar(RenovacaoDto pedido, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = new Execucao(traceId, AcaoAuditada.RenovarSessao, null, null, null);
        return auditada.Auditar(execucao, () => Renovar(pedido, execucao, ct), ct);
    }

    /// <summary>A regra da renovação, da sessão viva ao "CONCLUIDO".</summary>
    async Task<SessaoAbertaDto> Renovar(RenovacaoDto pedido, Execucao execucao, CancellationToken ct)
    {
        var token = pedido.Validar();
        var agora = relogio.GetUtcNow();
        var sessao = await SessaoDaRenovacao(token, agora, ct);
        var (tipo, atuais) = await PermissoesAtuais(sessao, ct);
        var emitida = await Emitir(sessao, tipo, atuais, agora, ct);
        await RegistrarConclusao(execucao, sessao, ct);
        return emitida.Resposta;
    }

    /// <summary>O tipo e as permissões recalculadas do acesso anônimo ou do usuário da sessão (RN-230).</summary>
    /// <exception cref="FalhaDeAutenticacao">SESSAO_TERMINADA, se a sala, o usuário ou a conta saíram de uso.</exception>
    Task<(string, PermissoesNoToken)> PermissoesAtuais(Sessao sessao, CancellationToken ct) =>
        sessao.Frente == Frente.Sala ? DoAnonimo(sessao, ct) : DoUsuario(sessao, ct);

    /// <summary>Troca o token de renovação e emite os tokens novos com as permissões atuais (RNF-213).</summary>
    async Task<SessaoEmitida> Emitir(Sessao sessao, string tipo, PermissoesNoToken atuais, DateTimeOffset agora, CancellationToken ct)
    {
        var renovacao = sessao.Renovar(atuais.Pv, agora);
        return await emissor.Emitir(sessao, renovacao, tipo, atuais, agora, ct);
    }

    /// <summary>O "CONCLUIDO" com a pessoa, a conta e a sessão renovada como alvo (RN-272).</summary>
    Task RegistrarConclusao(Execucao execucao, Sessao sessao, CancellationToken ct) =>
        auditada.Concluir(execucao with { ContaId = sessao.ContaId, Quem = sessao.UsuarioId, Alvo = sessao.Sid }, ct);

    /// <summary>
    /// A sessão do token, se está ativa e o token é o último entregue: o já
    /// trocado é sinal de cópia e encerra a sessão (RNF-214).
    /// </summary>
    /// <exception cref="FalhaDeAutenticacao">SESSAO_TERMINADA.</exception>
    async Task<Sessao> SessaoDaRenovacao(TokenDeRenovacao token, DateTimeOffset agora, CancellationToken ct)
    {
        var sessao = await sessoes.Buscar(token.Sid, ct) ?? throw FalhaDeAutenticacao.SessaoTerminada();
        if (!sessao.EstaAtiva(agora)) await Encerrar(sessao, MotivoFim.TempoSemUso, ct);
        if (!sessao.RenovacaoConfere(token)) await Encerrar(sessao, MotivoFim.CopiaDaRenovacao, ct);
        return sessao;
    }

    /// <summary>As permissões do usuário, se ele e a conta continuam em uso; fora disso, a sessão termina.</summary>
    /// <exception cref="FalhaDeAutenticacao">SESSAO_TERMINADA.</exception>
    async Task<(string, PermissoesNoToken)> DoUsuario(Sessao sessao, CancellationToken ct)
    {
        var usuario = await usuarios.Buscar(sessao.UsuarioId, ct);
        if (usuario is not { Estado: EstadoUsuario.Ativo }) await Encerrar(sessao, MotivoFim.UsuarioDesativado, ct);
        if (await contas.Buscar(sessao.ContaId, ct) is not { Estado: EstadoConta.Ativa })
        {
            await Encerrar(sessao, MotivoFim.ContaIndisponivel, ct);
        }
        return (usuario!.Tipo.Texto(), await permissoes.DoUsuario(usuario, ct));
    }

    /// <summary>As permissões do acesso anônimo, se a sala continua aberta; fechada, a sessão termina (RN-231).</summary>
    /// <exception cref="FalhaDeAutenticacao">SESSAO_TERMINADA.</exception>
    async Task<(string, PermissoesNoToken)> DoAnonimo(Sessao sessao, CancellationToken ct)
    {
        var sala = await salas.BuscarAberta(sessao.Sala!, ct);
        if (sala is null) await Encerrar(sessao, MotivoFim.SalaFechada, ct);
        return (EntrarNaSala.TipoAnonimo, await permissoes.DoAnonimo(sala!, ct));
    }

    // Apaga a sessão e recusa a renovação: sempre lança.
    async Task Encerrar(Sessao sessao, MotivoFim motivo, CancellationToken ct)
    {
        await sessoes.Apagar(sessao, motivo, ct);
        throw FalhaDeAutenticacao.SessaoTerminada();
    }
}
