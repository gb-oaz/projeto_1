using System.Globalization;
using System.Text.RegularExpressions;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.MsMono.Sistema;

// O relógio de toda a aplicação (tokens, sessões, card, fechamento): o do
// sistema, mais um deslocamento que só o relógio de teste muda (15 RN-278,
// POST /_teste/relogio). Andando no ritmo real a partir da hora definida.
public sealed partial class RelogioDaAplicacao : TimeProvider
{
    long deslocamento;

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + TimeSpan.FromTicks(Interlocked.Read(ref deslocamento));

    // a aplicação passa a contar a partir de agora
    public void Definir(DateTimeOffset agora) => Interlocked.Exchange(ref deslocamento, (agora - System.GetUtcNow()).Ticks);

    // volta ao relógio real
    public void Real() => Interlocked.Exchange(ref deslocamento, 0);

    // O relógio de teste só existe em homologação, com Teste__Relogio=sim (o
    // backend do qa no ci.sh); no Azure o Terraform fixa "nao".
    public static bool DeTesteLigado(IConfiguration configuracao) =>
        new AmbienteDaAplicacao(configuracao[ContasIniciais.VariavelDoAmbiente]).EmHomologacao
        && string.Equals(configuracao["Teste:Relogio"], "sim", StringComparison.OrdinalIgnoreCase);

    // data e hora com o fuso: 2027-02-28T23:50:00-03:00 ou …Z
    public static DateTimeOffset? Ler(string? texto) =>
        texto is not null && ComFuso().IsMatch(texto)
        && DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var agora)
            ? agora
            : null;

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex ComFuso();
}
