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
        if (chave is null || !token.ConfereCom(chave) || agora >= claims.Exp || claims.Aud != ClaimsDeAcesso.AudDe(pedido.Frente))
        {
            throw FalhaDeAutenticacao.TokenInvalido();
        }

        var sessao = await sessoes.Buscar(claims.Sid, ct);
        if (sessao is null || sessao.UsuarioId != claims.Sub) throw FalhaDeAutenticacao.SessaoTerminada();
        if (!sessao.EstaAtiva(agora))
        {
            await sessoes.Apagar(sessao, MotivoFim.TempoSemUso, ct);
            throw FalhaDeAutenticacao.SessaoTerminada();
        }
        if (claims.Pv != sessao.Pv) throw FalhaDeAutenticacao.PermissoesDesatualizadas();

        sessao.Tocar(agora);
        await sessoes.Gravar(sessao, ct);
        return new AcessoConferido(sessao.Sid, claims.Sub, sessao.Frente == Frente.Sala ? null : sessao.UsuarioId,
            sessao.ContaId, sessao.Sigla, claims.Tipo, sessao.Frente, sessao.Aparelho, sessao.QuemEntrou,
            claims.Papel, claims.Mods, sessao.Sala);
    }
}
