using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autenticacao.UseCases;

/// <summary>
/// Troca o token de renovação por tokens novos (RF-137, RNF-213). A sessão
/// precisa estar ativa e o token ser o último entregue: um já trocado encerra a
/// sessão. As permissões são recalculadas (RN-230), e o usuário e a conta são
/// conferidos de novo: fora de uso, a sessão termina.
/// </summary>
public sealed class RenovarSessao : IRenovarSessao
{
    readonly IUsuarios usuarios;
    readonly IContas contas;
    readonly ISessoes sessoes;
    readonly IPermissoes permissoes;
    readonly ISalas salas;
    readonly EmissorDeTokens emissor;
    readonly TimeProvider relogio;

    /// <summary>Monta o caso de uso com as portas de saída.</summary>
    public RenovarSessao(IUsuarios usuarios, IContas contas, ISessoes sessoes, IPermissoes permissoes, ISalas salas,
        IAssinador assinador, TimeProvider relogio)
    {
        this.usuarios = usuarios;
        this.contas = contas;
        this.sessoes = sessoes;
        this.permissoes = permissoes;
        this.salas = salas;
        this.relogio = relogio;
        emissor = new EmissorDeTokens(sessoes, assinador);
    }

    /// <inheritdoc />
    public async Task<SessaoAbertaDto> Executar(RenovacaoDto pedido, CancellationToken ct = default)
    {
        var token = pedido.Validar();
        var agora = relogio.GetUtcNow();
        var sessao = await sessoes.Buscar(token.Sid, ct) ?? throw FalhaDeAutenticacao.SessaoTerminada();
        if (!sessao.EstaAtiva(agora)) await Encerrar(sessao, MotivoFim.TempoSemUso, ct);
        if (!sessao.RenovacaoConfere(token)) await Encerrar(sessao, MotivoFim.CopiaDaRenovacao, ct);

        var (tipo, atuais) = sessao.Frente == Frente.Sala ? await DoAnonimo(sessao, ct) : await DoUsuario(sessao, ct);
        var renovacao = sessao.Renovar(atuais.Pv, agora);
        return await emissor.Emitir(sessao, renovacao, tipo, atuais, agora, ct);
    }

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
