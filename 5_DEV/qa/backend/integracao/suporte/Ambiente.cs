namespace Boaz.Qa.Integracao.Suporte;

// Variáveis do contrato da suíte (RNF-191) e as do preparo do estado. Os
// padrões servem para rodar na máquina contra o backend local.
public static class Ambiente
{
    public static string ApiUrl => Ler("API_URL", "http://localhost:8080");
    public static string MongoUrl => Ler("MONGO_URL", "mongodb://localhost:27017");
    public static string RedisUrl => Ler("REDIS_URL", "localhost:6379");
    // banco do backend no MongoDB, onde o preparo grava (versão das permissões, contratações)
    public static string MongoBanco => Ler("MONGO_BANCO", "boaz");

    public static bool PodeLimpar => Ler("QA_PODE_LIMPAR", "") == "sim";

    // entrada e senha do administrador do deboaz: no Key Vault em homologação e
    // produção, entregues como variável de ambiente na partida (RN-190, RNF-225).
    // A suíte lê as mesmas variáveis que o backend; ADMIN_ENTRADA e ADMIN_SENHA
    // são os nomes antigos, aceitos enquanto o CI não passa os novos.
    public static string AdminEntrada => Ler("USUARIO_ADMIN_ENTRADA", Ler("ADMIN_ENTRADA", "admin.dboaz@deboaz.com"));
    public static string AdminSenha => Ler("USUARIO_ADMIN_SENHA", Ler("ADMIN_SENHA", ""));

    // chave de assinatura do token, a mesma do backend: PKCS#8 em base64
    public static string ChavePrivada => Ler("CHAVE_PRIVADA", "");

    // comando que sobe uma instância nova do backend (o artefato publicado, nunca
    // o código), para os cenários de inicialização (RN-190, RN-266, RN-267,
    // RNF-226). Sem ele, esses cenários ficam pendentes. Ver suporte/Partida.cs.
    public static string ApiComando => Ler("API_COMANDO", "");

    // valor de uma variável do Key Vault (USUARIO_*), ou vazio
    public static string Segredo(string nome) => Ler(nome, "");

    // variáveis com o prefixo PARTIDA_, repassadas sem o prefixo à instância de
    // Partida (conexões, chave, banco próprio): a suíte não conhece a
    // configuração interna do backend, só repassa o que o CI mandou
    public static IEnumerable<KeyValuePair<string, string>> DaPartida()
    {
        foreach (System.Collections.DictionaryEntry v in Environment.GetEnvironmentVariables())
        {
            if (v.Key is string nome && nome.StartsWith("PARTIDA_", StringComparison.Ordinal) && v.Value is string valor)
            {
                yield return KeyValuePair.Create(nome["PARTIDA_".Length..], valor);
            }
        }
    }

    static string Ler(string nome, string padrao) =>
        Environment.GetEnvironmentVariable(nome) is { Length: > 0 } valor ? valor : padrao;
}
