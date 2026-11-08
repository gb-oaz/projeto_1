using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autenticacao.UseCases;

/// <summary>
/// Confere o token de cada pedido (RNF-216): assinatura de uma chave aceita
/// (RNF-218), validade de 15 min, frente do endereço (RN-227), sessão ativa no
/// servidor (RN-224) e versão das permissões (RN-230). O pedido conta como uso
/// da sessão (RN-226).
/// </summary>
public sealed class ConferirAcesso(ISessoes sessoes, IAssinador assinador, TimeProvider relogio) : IConferirAcesso
{
    /// <inheritdoc />
    public async Task<AcessoConferido> Executar(ConferenciaDeAcessoDto pedido, CancellationToken ct = default)
    {
        var token = TokenDeAcesso.Ler(pedido.Validar());
        var chave = (await assinador.ChavesAceitas(ct)).FirstOrDefault(c => c.Kid == token.Kid);
        var agora = relogio.GetUtcNow();
        var claims = token.Claims;
        GarantirTokenValido(token, chave, pedido.Frente, agora);

        var sessao = await SessaoAtiva(claims, agora, ct);
        if (claims.Pv != sessao.Pv) throw FalhaDeAutenticacao.PermissoesDesatualizadas();
        await Tocar(sessao, agora, ct);
        return Montar(sessao, claims);
    }

    /// <summary>Assinatura de uma chave aceita (RNF-218), dentro dos 15 min e na frente do endereço (RN-227).</summary>
    /// <exception cref="FalhaDeAutenticacao">TOKEN_INVALIDO.</exception>
    static void GarantirTokenValido(TokenDeAcesso token, ChavePublica? chave, Frente frente, DateTimeOffset agora)
    {
        var claims = token.Claims;
        if (chave is null || !token.ConfereCom(chave) || agora >= claims.Exp || claims.Aud != ClaimsDeAcesso.AudDe(frente))
        {
            throw FalhaDeAutenticacao.TokenInvalido();
        }
    }

    /// <summary>A sessão do token, se é da mesma pessoa e está ativa; sem uso além do tempo, ela é apagada (RN-224).</summary>
    /// <exception cref="FalhaDeAutenticacao">SESSAO_TERMINADA.</exception>
    async Task<Sessao> SessaoAtiva(ClaimsDeAcesso claims, DateTimeOffset agora, CancellationToken ct)
    {
        var sessao = await sessoes.Buscar(claims.Sid, ct);
        if (sessao is null || sessao.UsuarioId != claims.Sub) throw FalhaDeAutenticacao.SessaoTerminada();
        if (sessao.EstaAtiva(agora)) return sessao;
        await sessoes.Apagar(sessao, MotivoFim.TempoSemUso, ct);
        throw FalhaDeAutenticacao.SessaoTerminada();
    }

    /// <summary>O pedido conta como uso da sessão (RN-226).</summary>
    async Task Tocar(Sessao sessao, DateTimeOffset agora, CancellationToken ct)
    {
        sessao.Tocar(agora);
        await sessoes.Gravar(sessao, ct);
    }

    /// <summary>O acesso conferido, com o que o token e a sessão dizem de quem pede.</summary>
    static AcessoConferido Montar(Sessao sessao, ClaimsDeAcesso claims) =>
        new(sessao.Sid, claims.Sub, sessao.Frente == Frente.Sala ? null : sessao.UsuarioId,
            sessao.ContaId, sessao.Sigla, claims.Tipo, sessao.Frente, sessao.Aparelho, sessao.QuemEntrou,
            claims.Papel, claims.Mods, sessao.Sala);
}
