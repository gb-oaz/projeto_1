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
/// Entrar no quest com o código de acesso do participante, como ele ou como um
/// responsável (RF-135, RN-228). O quest não tem limite de sessões (RN-247) e o
/// código errado conta para o bloqueio como a senha (RNF-204). Auditado com o
/// traceId recebido, ação ENTRAR_QUEST: "INICIADO" sem quem, conta nem alvo; o
/// participante é identificado depois do código conferido, e o "CONCLUIDO" vai
/// com ele, a conta e a sessão (RN-272, RN-273). O código não vai nos registros.
/// </summary>
public sealed class EntrarNoQuest : IEntrarNoQuest
{
    readonly IUsuarios usuarios;
    readonly ContadorDeTentativas tentativas;
    readonly AberturaDeSessao abertura;
    readonly AuditoriaDaExecucao auditada;

    /// <summary>Monta o caso de uso com as portas de saída.</summary>
    public EntrarNoQuest(IUsuarios usuarios, IContas contas, ISessoes sessoes, ITentativas tentativas,
        IPermissoes permissoes, IAssinador assinador, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    {
        this.usuarios = usuarios;
        this.tentativas = new ContadorDeTentativas(tentativas, relogio);
        abertura = new AberturaDeSessao(usuarios, contas, sessoes, permissoes, new EmissorDeTokens(sessoes, assinador), relogio);
        auditada = new AuditoriaDaExecucao(auditoria, erros, relogio);
    }

    /// <inheritdoc />
    public Task<SessaoAbertaDto> Executar(EntradaNoQuestDto pedido, TraceId traceId, CancellationToken ct = default) =>
        auditada.Auditar(new Execucao(traceId, AcaoAuditada.EntrarQuest, null, null, null), andamento => Entrar(pedido, andamento, ct), ct);

    /// <summary>A regra da entrada, do código ao "CONCLUIDO".</summary>
    async Task<SessaoAbertaDto> Entrar(EntradaNoQuestDto pedido, ExecucaoEmAndamento andamento, CancellationToken ct)
    {
        var (codigo, aparelho, quem) = pedido.Validar();
        var usuario = await Identificar(codigo, ct);
        andamento.Identificar(usuario.ContaId, usuario.Id);
        var conta = await abertura.ContaDisponivel(usuario, primeiroAcesso: false, ct);
        var aberta = await abertura.Abrir(usuario, conta, Frente.Quest, aparelho, quem, ct);
        await RegistrarConclusao(andamento, aberta, ct);
        return aberta.Resposta;
    }

    /// <summary>O "CONCLUIDO" com a pessoa identificada, a conta e a sessão aberta como alvo (RN-272).</summary>
    Task RegistrarConclusao(ExecucaoEmAndamento andamento, SessaoEmitida aberta, CancellationToken ct) =>
        auditada.Concluir(andamento.Atual with { Alvo = aberta.Sessao.Sid }, ct);

    /// <summary>O participante do código: bloqueio primeiro (RF-158) e o erro contado (RNF-204).</summary>
    /// <exception cref="FalhaDeAutenticacao">ENTRADA_BLOQUEADA ou CODIGO_INVALIDO.</exception>
    async Task<UsuarioDaEntrada> Identificar(string codigo, CancellationToken ct)
    {
        await tentativas.ConferirBloqueio(AlvoTentativa.CodigoAcesso, codigo, ct);
        var usuario = await usuarios.BuscarPorCodigoDeAcesso(codigo, ct);
        if (usuario is not { Tipo: TipoUsuario.Participante })
        {
            throw await tentativas.Errar(AlvoTentativa.CodigoAcesso, codigo, FalhaDeAutenticacao.CodigoInvalido(), ct);
        }
        await tentativas.Acertar(AlvoTentativa.CodigoAcesso, codigo, ct);
        return usuario;
    }
}
