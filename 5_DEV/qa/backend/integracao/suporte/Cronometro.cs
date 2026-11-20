using System.Diagnostics;

namespace Boaz.Qa.Integracao.Suporte;

// O relógio monotônico da suíte (Stopwatch) para medir prazos e esperas: o
// relógio de parede (UtcNow) da máquina ou do Docker pode saltar e encolher
// uma espera. Agora é o tempo desde o início da suíte; só a diferença entre
// dois instantes tem sentido. Datas que o backend grava ou devolve seguem no
// relógio de parede.
public static class Cronometro
{
    static readonly long inicio = Stopwatch.GetTimestamp();

    public static TimeSpan Agora => Stopwatch.GetElapsedTime(inicio);
}
