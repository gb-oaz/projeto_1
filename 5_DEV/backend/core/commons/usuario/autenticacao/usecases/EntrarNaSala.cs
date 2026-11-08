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
/// Acesso anônimo a uma sala aberta, sem código de acesso (RF-136). A sessão não
/// tem tempo sem uso: termina quando a sala fecha (RN-231). Auditado com o
/// traceId recebido, ação ENTRAR_SALA: "INICIADO" sem quem, conta nem alvo;
/// "CONCLUIDO" com o acesso anônimo, a conta da sala e a sala como alvo
/// (RN-272, RN-273).
/// </summary>
public sealed class EntrarNaSala : IEntrarNaSala
{
    /// <summary>Tipo que vai no token do acesso anônimo.</summary>
    public const string TipoAnonimo = "ANONIMO";

    readonly ISalas salas;
    readonly IPermissoes permissoes;
    readonly EmissorDeTokens emissor;
    readonly AuditoriaDaExecucao auditada;
    readonly TimeProvider relogio;

    /// <summary>Monta o caso de uso com as portas de saída.</summary>
    public EntrarNaSala(ISalas salas, ISessoes sessoes, IPermissoes permissoes, IAssinador assinador,
        IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    {
        this.salas = salas;
        this.permissoes = permissoes;
        this.relogio = relogio;
        emissor = new EmissorDeTokens(sessoes, assinador);
        auditada = new AuditoriaDaExecucao(auditoria, erros, relogio);
    }

    /// <inheritdoc />
    public Task<SessaoAbertaDto> Executar(EntradaNaSalaDto pedido, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = new Execucao(traceId, AcaoAuditada.EntrarSala, null, null, null);
        return auditada.Auditar(execucao, () => Entrar(pedido, execucao, ct), ct);
    }

    /// <summary>A regra da entrada anônima, da sala aberta ao "CONCLUIDO".</summary>
    async Task<SessaoAbertaDto> Entrar(EntradaNaSalaDto pedido, Execucao execucao, CancellationToken ct)
    {
        var (codigo, aparelho) = pedido.Validar();
        var sala = await salas.BuscarAberta(codigo, ct) ?? throw FalhaDeAutenticacao.SalaFechada();
        var emitida = await Abrir(sala, aparelho, ct);
        await auditada.Concluir(execucao with { ContaId = sala.ContaId, Quem = emitida.Sessao.UsuarioId, Alvo = sala.Codigo }, ct);
        return emitida.Resposta;
    }

    /// <summary>Um acesso anônimo novo na sala, com a sessão sem tempo sem uso (RN-231).</summary>
    async Task<SessaoEmitida> Abrir(SalaAberta sala, Aparelho aparelho, CancellationToken ct)
    {
        var acesso = await salas.NovoAcessoAnonimo(sala, ct);
        var doAnonimo = await permissoes.DoAnonimo(sala, ct);
        var agora = relogio.GetUtcNow();
        var (sessao, renovacao) = Sessao.Abrir(acesso.Id, sala.ContaId, sala.Sigla, Frente.Sala, aparelho, null,
            sala.Codigo, doAnonimo.Pv, tempoSemUso: null, agora);
        return await emissor.Emitir(sessao, renovacao, TipoAnonimo, doAnonimo, agora, ct);
    }
}
