using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.MsMono.Infra.Out.Commons.Usuario;

// A conta do time deboaz nos adaptadores: a sigla e o id saem da regra do core
// (Sigla.DoTime → conta-dboaz). A conta e o administrador nascem na partida,
// antes do primeiro pedido (IInicializarContas, RN-190), e não são recriados
// no meio do caminho.
public static class TimeDeboaz
{
    // A sigla do time, regra do core (RN-179).
    public static string Sigla => Core.Commons.Usuario.Usuarios.Domain.Sigla.DoTime;

    // A conta do time: conta-<sigla>, como toda conta.
    public static string ContaId => ContaAssinante.IdDa(Sigla);
}
