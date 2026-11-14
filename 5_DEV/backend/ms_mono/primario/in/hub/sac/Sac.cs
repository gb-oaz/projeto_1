using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.MsMono.Primario.In.Commons;

namespace Boaz.MsMono.Primario.In.Hub.Sac;

// O que os controllers do SAC repetem: o módulo e quem pede, como o token
// conferido por [ComAcesso] ou [ComAcessoAuditado] diz.
static class Sac
{
    // o MOD-02 (15_modulos)
    public const string Modulo = "MOD-02";

    // a conferência das conexões ao vivo da conversa (RNF-230: em até 5 s)
    public static readonly TimeSpan Conferencia = TimeSpan.FromSeconds(4);

    // ": ping" a cada 30 s
    public static readonly TimeSpan Ping = TimeSpan.FromSeconds(30);

    public static QuemPede QuemPedeAoSac(this HttpContext http)
    {
        var acesso = http.Acesso();
        return new QuemPede(acesso.Sub, acesso.Papel, acesso.ContaId);
    }
}
