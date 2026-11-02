using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// GET /usuarios/card (RN-189): na instituição, staff e participantes ativos; no
/// autônomo, os participantes; no time deboaz, as contas ativas. O selo conta
/// os códigos de liberação que vencem hoje (UTC).
/// </summary>
public sealed class LerCardDeUsuarios(
    IUsuarios usuarios, IContas contas, IIndicadores indicadores, IAuditoria auditoria, TimeProvider relogio) : ILerCardDeUsuarios
{
    /// <summary>Quantos meses o gráfico mostra.</summary>
    public const int Meses = 5;

    readonly Apoio apoio = new(usuarios, contas, auditoria, relogio);

    /// <inheritdoc />
    public async Task<CardDeUsuariosDto> Executar(QuemPedeDto pedido, CancellationToken ct = default)
    {
        var quem = await apoio.Quem(pedido.QuemPede, ct);
        if (quem.Tipo == TipoUsuario.Participante) throw FalhaDeUsuarios.AcessoNegado();
        var conta = await apoio.ContaDe(quem, ct);
        var agora = apoio.Agora;
        var hoje = new DateTimeOffset(agora.UtcDateTime.Date, TimeSpan.Zero);
        var selo = await indicadores.CodigosQueVencem(conta.Id, hoje, hoje.AddDays(1), ct);

        NumerosDoCard numeros;
        if (quem.Tipo.DoTime())
        {
            numeros = new(selo, await indicadores.ContasAtivasPorMes(agora, Meses, ct), null, null);
        }
        else if (conta.DeAutonomo)
        {
            var meses = await indicadores.UsuariosAtivosPorMes(conta.Id, new HashSet<TipoUsuario> { TipoUsuario.Participante }, agora, Meses, ct);
            numeros = new(selo, meses, null, await indicadores.UsuariosAtivos(conta.Id, TipoUsuario.Participante, ct));
        }
        else
        {
            var meses = await indicadores.UsuariosAtivosPorMes(
                conta.Id, new HashSet<TipoUsuario> { TipoUsuario.Staff, TipoUsuario.Participante }, agora, Meses, ct);
            numeros = new(selo, meses,
                await indicadores.UsuariosAtivos(conta.Id, TipoUsuario.Staff, ct),
                await indicadores.UsuariosAtivos(conta.Id, TipoUsuario.Participante, ct));
        }
        return new(numeros.Selo, numeros.Dado, NumerosDoCard.Rotulo(quem.Tipo, conta.DeAutonomo), numeros.NoMes, numeros.Meses,
            numeros.Staff, numeros.Participantes);
    }
}
