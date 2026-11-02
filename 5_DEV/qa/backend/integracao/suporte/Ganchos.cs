using Reqnroll;

namespace Boaz.Qa.Integracao.Suporte;

[Binding]
public sealed class Ganchos
{
    [BeforeScenario]
    public static Task Limpar() => Estado.Limpar();
}
