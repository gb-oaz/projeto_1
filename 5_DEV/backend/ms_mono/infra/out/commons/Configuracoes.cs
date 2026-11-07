namespace Boaz.MsMono.Infra.Out.Commons;

// Parâmetros do backend, lidos da configuração: appsettings.json traz os
// padrões, e cada um pode ser trocado por variável de ambiente com "__" no
// lugar de ":" (Time__Nome, Mongo__Banco…). O código não
// guarda valor padrão: o que faltar na configuração para a partida com uma
// mensagem dizendo qual variável definir. A lista completa está no README do
// backend. As entradas e senhas das contas iniciais (USUARIO_*) não são uma
// seção: o adaptador da partida as lê pelo nome exato da variável
// (infra/in/commons/usuario/usuarios/Partida.cs, RNF-225).

// Seção "Mongo": o banco de dados dentro do servidor de ConnectionStrings:MongoDb.
public sealed class OpcoesDoMongo
{
    public const string Secao = "Mongo";

    // Nome do banco (Mongo__Banco).
    public string Banco { get; set; } = "";
}

// Seção "Time": o cadastro da conta do time deboaz, gravado só quando ela nasce.
// A sigla e o id não são parâmetros: vêm da regra do core (Sigla.DoTime, RN-179).
public sealed class OpcoesDoTime
{
    public const string Secao = "Time";

    // Nome da conta (Time__Nome).
    public string Nome { get; set; } = "";

    // CNPJ da empresa (Time__Documento); vazio enquanto não houver.
    public string Documento { get; set; } = "";

    // Responsável pela conta (Time__Responsavel).
    public string Responsavel { get; set; } = "";

    // Telefone do responsável (Time__Telefone).
    public string Telefone { get; set; } = "";
}

// Seção "Autenticacao": a chave que assina o token de acesso (RNF-207).
public sealed class OpcoesDaAutenticacao
{
    public const string Secao = "Autenticacao";

    // Identificador da chave, o "kid" do token (Autenticacao__ChaveId).
    public string ChaveId { get; set; } = "";

    // Chave privada RSA em PKCS#8 base64 (Autenticacao__ChavePrivada). Segredo:
    // sem ela, uma chave nova é gerada a cada partida e os tokens caem quando o
    // backend reinicia, o que só serve à máquina de quem desenvolve.
    public string? ChavePrivada { get; set; }
}

public static class Configuracoes
{
    // Liga cada seção à configuração e confere na partida o que é obrigatório.
    public static IServiceCollection AddConfiguracoes(this IServiceCollection servicos)
    {
        servicos.AddOptions<OpcoesDoMongo>().BindConfiguration(OpcoesDoMongo.Secao)
            .Validate(o => Preenchido(o.Banco), Falta("Mongo__Banco"))
            .ValidateOnStart();
        servicos.AddOptions<OpcoesDoTime>().BindConfiguration(OpcoesDoTime.Secao)
            .Validate(o => Preenchido(o.Nome), Falta("Time__Nome"))
            .ValidateOnStart();
        servicos.AddOptions<OpcoesDaAutenticacao>().BindConfiguration(OpcoesDaAutenticacao.Secao)
            .Validate(o => Preenchido(o.ChaveId), Falta("Autenticacao__ChaveId"))
            .Validate(o => !Preenchido(o.ChavePrivada) || ChaveRsaPkcs8(o.ChavePrivada!),
                "Autenticacao__ChavePrivada: chave RSA em PKCS#8 base64 (MIIEv…); converta com openssl pkcs8 -topk8 -nocrypt -outform DER.")
            .ValidateOnStart();
        return servicos;
    }

    static bool Preenchido(string? valor) => !string.IsNullOrWhiteSpace(valor);

    static string Falta(string variavel) => $"Defina {variavel} (appsettings.json ou variável de ambiente).";

    // A chave importa como PKCS#8. O OpenSSL 3 grava "genpkey -outform DER" em
    // PKCS#1 (MIIEo…), que o ImportPkcs8PrivateKey recusa: sem esta conferência,
    // o backend subia e só o pedido que assina um token respondia 500.
    static bool ChaveRsaPkcs8(string base64)
    {
        try
        {
            using var rsa = System.Security.Cryptography.RSA.Create();
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(base64), out _);
            return true;
        }
        catch (Exception erro) when (erro is FormatException or System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }
}
