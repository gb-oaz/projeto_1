using System.Runtime.CompilerServices;

namespace Boaz.MsMono.Tests;

// O backend não sobe sem o segredo do administrador (17 RNF-226) e garante as
// contas iniciais no banco antes do primeiro pedido (RN-190). Os testes que
// sobem o backend com os padrões do appsettings.json (HealthTests,
// DocumentacaoApiTests, ConfiguracoesTests) recebem aqui, uma vez por
// processo, um administrador de teste e um banco próprio, apagado no fim; o
// que já vier do ambiente vale mais.
static class Padroes
{
    public const string AdminSenha = "Ad#Padrao314159";

    [ModuleInitializer]
    internal static void Definir()
    {
        Padrao("USUARIO_ADMIN_ENTRADA", "admin.dboaz@deboaz.com");
        Padrao("USUARIO_ADMIN_SENHA", AdminSenha);
        if (Padrao("Mongo__Banco", $"boaz_testes_padrao_{Guid.NewGuid():N}") is { } banco)
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try
                {
                    new MongoDB.Driver.MongoClient(Environment.GetEnvironmentVariable("ConnectionStrings__MongoDb") ?? "mongodb://localhost:27017")
                        .DropDatabase(banco);
                }
                catch (Exception erro) when (erro is MongoDB.Driver.MongoException or TimeoutException)
                {
                    // sem banco no fim, nada a apagar
                }
            };
        }
    }

    // define a variável se ela não veio do ambiente; devolve o valor definido
    static string? Padrao(string nome, string valor)
    {
        if (Environment.GetEnvironmentVariable(nome) is { Length: > 0 }) return null;
        Environment.SetEnvironmentVariable(nome, valor);
        return valor;
    }
}
