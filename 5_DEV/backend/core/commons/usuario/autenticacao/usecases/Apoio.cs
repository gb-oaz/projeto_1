using System.Text;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autenticacao.UseCases;

// Peças internas que mais de um caso de uso usa. Não são casos de uso: não têm
// porta de entrada e não aparecem fora do core.

/// <summary>Grava a sessão e assina o token de acesso dela.</summary>
sealed class EmissorDeTokens(ISessoes sessoes, IAssinador assinador)
{
    /// <summary>Grava a sessão e devolve a resposta com os dois tokens.</summary>
    public async Task<SessaoAbertaDto> Emitir(Sessao sessao, TokenDeRenovacao renovacao, string tipo,
        PermissoesNoToken permissoes, DateTimeOffset agora, CancellationToken ct)
    {
        await sessoes.Gravar(sessao, ct);
        var claims = ClaimsDeAcesso.Para(sessao, sessao.UsuarioId, tipo, permissoes, agora);
        var kid = await assinador.KidAtual(ct);
        var paraAssinar = TokenDeAcesso.ParaAssinar(kid, claims);
        var assinatura = await assinador.Assinar(kid, Encoding.ASCII.GetBytes(paraAssinar), ct);
        return new SessaoAbertaDto(TokenDeAcesso.Completar(paraAssinar, assinatura), "Bearer",
            (int)ClaimsDeAcesso.Validade.TotalSeconds, renovacao.ToString(), sessao.Aparelho);
    }
}

/// <summary>Abre a sessão de um usuário já identificado, no hub ou no quest.</summary>
sealed class AberturaDeSessao(IUsuarios usuarios, IContas contas, ISessoes sessoes, IPermissoes permissoes,
    EmissorDeTokens emissor, TimeProvider relogio)
{
    /// <summary>
    /// A conta do usuário, se ele e ela estão em uso (RF-140, RN-175). Conferido
    /// depois da credencial, para não revelar o estado a quem não a tem.
    /// </summary>
    /// <param name="usuario">O usuário que entra.</param>
    /// <param name="primeiroAcesso">Aceita o usuário PENDENTE: é a confirmação da senha que o ativa.</param>
    /// <exception cref="FalhaDeAutenticacao">ACESSO_INDISPONIVEL.</exception>
    public async Task<ContaDaEntrada> ContaDisponivel(UsuarioDaEntrada usuario, bool primeiroAcesso, CancellationToken ct)
    {
        var usuarioEmUso = usuario.Estado == EstadoUsuario.Ativo || (primeiroAcesso && usuario.Estado == EstadoUsuario.Pendente);
        var conta = await contas.Buscar(usuario.ContaId, ct);
        return usuarioEmUso && conta is { Estado: EstadoConta.Ativa } ? conta : throw FalhaDeAutenticacao.AcessoIndisponivel();
    }

    /// <summary>
    /// Abre a sessão: tempo sem uso da pessoa (RN-226), limite do hub (RN-247),
    /// permissões da autorização e o último acesso do usuário.
    /// </summary>
    public async Task<SessaoAbertaDto> Abrir(UsuarioDaEntrada usuario, ContaDaEntrada conta, Frente frente,
        Aparelho aparelho, QuemEntrou? quem, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        var time = await contas.BuscarTimeDeboaz(ct);
        var tempo = TempoDeSessao.SemUso(usuario.Tipo, aparelho, conta.Tempo, time.Tempo);
        if (frente == Frente.Hub)
        {
            var ativas = (await sessoes.DoUsuario(usuario.Id, ct)).Where(s => s.EstaAtiva(agora));
            if (Sessao.MaisAntigaAlemDoLimite(ativas) is { } maisAntiga)
            {
                await sessoes.Apagar(maisAntiga, MotivoFim.LimiteDoHub, ct);
            }
        }
        var doUsuario = await permissoes.DoUsuario(usuario, ct);
        var (sessao, renovacao) = Sessao.Abrir(usuario.Id, conta.Id, conta.Sigla, frente, aparelho, quem, null,
            doUsuario.Pv, tempo, agora);
        var resposta = await emissor.Emitir(sessao, renovacao, usuario.Tipo.Texto(), doUsuario, agora, ct);
        await usuarios.RegistrarUltimoAcesso(usuario.Id, agora, ct);
        return resposta;
    }
}

/// <summary>Conta os erros seguidos e bloqueia (RNF-204).</summary>
sealed class ContadorDeTentativas(ITentativas tentativas, TimeProvider relogio)
{
    /// <summary>Recusa já, mesmo com a credencial certa, se a chave está bloqueada (RF-158).</summary>
    /// <exception cref="FalhaDeAutenticacao">ENTRADA_BLOQUEADA.</exception>
    public async Task ConferirBloqueio(AlvoTentativa alvo, string chave, CancellationToken ct)
    {
        var guardadas = await tentativas.Buscar(alvo, chave, ct);
        if (guardadas is { BloqueadoAte: { } fim } && guardadas.EstaBloqueada(relogio.GetUtcNow()))
        {
            throw FalhaDeAutenticacao.EntradaBloqueada(fim);
        }
    }

    /// <summary>
    /// Conta o erro e lança: ENTRADA_BLOQUEADA se ele bloqueou (o quinto erro já
    /// recebe 429), senão a falha da credencial.
    /// </summary>
    public async Task<FalhaDeAutenticacao> Errar(AlvoTentativa alvo, string chave, FalhaDeAutenticacao falha, CancellationToken ct)
    {
        var guardadas = await tentativas.Buscar(alvo, chave, ct) ?? new TentativasDeEntrada(alvo, chave);
        var bloqueou = guardadas.RegistrarErro(relogio.GetUtcNow());
        await tentativas.Gravar(guardadas, ct);
        return bloqueou ? FalhaDeAutenticacao.EntradaBloqueada(guardadas.BloqueadoAte!.Value) : falha;
    }

    /// <summary>A credencial certa zera a contagem.</summary>
    public Task Acertar(AlvoTentativa alvo, string chave, CancellationToken ct) => tentativas.Apagar(alvo, chave, ct);
}
