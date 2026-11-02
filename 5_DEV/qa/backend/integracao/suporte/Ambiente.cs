namespace Boaz.Qa.Integracao.Suporte;

// Variáveis do contrato da suíte (RNF-191) e as do preparo do estado. Os
// padrões servem para rodar na máquina contra o backend local.
public static class Ambiente
{
    public static string ApiUrl => Ler("API_URL", "http://localhost:8080");
    public static string MongoUrl => Ler("MONGO_URL", "mongodb://localhost:27017");
    public static string RedisUrl => Ler("REDIS_URL", "localhost:6379");

    public static bool PodeLimpar => Ler("QA_PODE_LIMPAR", "") == "sim";

    // entrada e senha do administrador do deboaz: no Key Vault em homologação e
    // produção, em variável de ambiente no CI local (RN-190)
    public static string AdminEntrada => Ler("ADMIN_ENTRADA", "admin.dboaz@deboaz.com");
    public static string AdminSenha => Ler("ADMIN_SENHA", "");

    // chave de assinatura do token, a mesma do backend: PKCS#8 em base64
    public static string ChavePrivada => Ler("CHAVE_PRIVADA", "");

    static string Ler(string nome, string padrao) =>
        Environment.GetEnvironmentVariable(nome) is { Length: > 0 } valor ? valor : padrao;
}
