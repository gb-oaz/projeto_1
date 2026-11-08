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
    IUsuarios usuarios, IContas contas, IIndicadores indicadores, TimeProvider relogio) : ILerCardDeUsuarios
{
    /// <summary>Quantos meses o gráfico mostra.</summary>
    public const int Meses = 5;

    readonly Apoio apoio = new(usuarios, contas, relogio);

    /// <inheritdoc />
    public async Task<CardDeUsuariosDto> Executar(QuemPedeDto pedido, CancellationToken ct = default)
    {
        var quem = await QuemVeOCard(pedido.QuemPede, ct);
        var conta = await apoio.ContaDe(quem, ct);
        var agora = apoio.Agora;
        var selo = await Selo(conta, agora, ct);
        var numeros = await Numeros(quem, conta, selo, agora, ct);
        return new(numeros.Selo, numeros.Dado, NumerosDoCard.Rotulo(quem.Tipo, conta.DeAutonomo), numeros.NoMes, numeros.Meses,
            numeros.Staff, numeros.Participantes);
    }

    /// <summary>Quem pede, se não é participante.</summary>
    /// <exception cref="FalhaDeUsuarios">ACESSO_NEGADO.</exception>
    async Task<UsuarioCadastrado> QuemVeOCard(string quemPede, CancellationToken ct)
    {
        var quem = await apoio.Quem(quemPede, ct);
        return quem.Tipo == TipoUsuario.Participante ? throw FalhaDeUsuarios.AcessoNegado() : quem;
    }

    /// <summary>Os códigos de liberação da conta que vencem hoje (UTC).</summary>
    Task<int> Selo(ContaAssinante conta, DateTimeOffset agora, CancellationToken ct)
    {
        var hoje = new DateTimeOffset(agora.UtcDateTime.Date, TimeSpan.Zero);
        return indicadores.CodigosQueVencem(conta.Id, hoje, hoje.AddDays(1), ct);
    }

    /// <summary>Os números do card de quem pede: do time deboaz, do autônomo ou da instituição.</summary>
    Task<NumerosDoCard> Numeros(UsuarioCadastrado quem, ContaAssinante conta, int selo, DateTimeOffset agora, CancellationToken ct) =>
        quem.Tipo.DoTime() ? DoTime(selo, agora, ct)
        : conta.DeAutonomo ? DoAutonomo(conta, selo, agora, ct)
        : DaInstituicao(conta, selo, agora, ct);

    /// <summary>No time deboaz, as contas ativas por mês.</summary>
    async Task<NumerosDoCard> DoTime(int selo, DateTimeOffset agora, CancellationToken ct) =>
        new(selo, await indicadores.ContasAtivasPorMes(agora, Meses, ct), null, null);

    /// <summary>No autônomo, os participantes ativos.</summary>
    async Task<NumerosDoCard> DoAutonomo(ContaAssinante conta, int selo, DateTimeOffset agora, CancellationToken ct)
    {
        var meses = await indicadores.UsuariosAtivosPorMes(conta.Id, new HashSet<TipoUsuario> { TipoUsuario.Participante }, agora, Meses, ct);
        return new(selo, meses, null, await indicadores.UsuariosAtivos(conta.Id, TipoUsuario.Participante, ct));
    }

    /// <summary>Na instituição, o staff e os participantes ativos.</summary>
    async Task<NumerosDoCard> DaInstituicao(ContaAssinante conta, int selo, DateTimeOffset agora, CancellationToken ct)
    {
        var meses = await indicadores.UsuariosAtivosPorMes(
            conta.Id, new HashSet<TipoUsuario> { TipoUsuario.Staff, TipoUsuario.Participante }, agora, Meses, ct);
        return new(selo, meses,
            await indicadores.UsuariosAtivos(conta.Id, TipoUsuario.Staff, ct),
            await indicadores.UsuariosAtivos(conta.Id, TipoUsuario.Participante, ct));
    }
}
