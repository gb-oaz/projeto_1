using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Infra.Out.Commons.Usuario;

// A conta do time deboaz (conta-dboaz) e o administrador (usr-dboaz-admin) não
// nascem por endereço: vêm da configuração (Administrador:Entrada e
// Administrador:Senha). Garantir() os cria se faltarem, sem tocar no que já
// existe; os adaptadores chamam quando uma busca pelo time não acha nada, para
// o time voltar mesmo depois de o banco ser esvaziado (como o qa faz).
public sealed class TimeDeboaz(Armazenamento armazenamento, IConfiguration configuracao)
{
    public const string ContaId = "conta-dboaz";
    public const string AdminId = "usr-dboaz-admin";
    public const string Sigla = "DBOAZ";
    public const string EntradaPadrao = "admin.dboaz@deboaz.com";

    // o resumo da senha custa caro (PBKDF2): sai uma vez por processo
    readonly Lazy<string?> resumoDaSenha = new(() =>
        configuracao["Administrador:Senha"] is { Length: > 0 } senha ? ResumoDeSenha.Gerar(senha) : null);

    // Entrada do administrador, em minúsculas como toda entrada.
    public string EntradaDoAdmin => Credenciais.Entrada(configuracao["Administrador:Entrada"] is { Length: > 0 } e ? e : EntradaPadrao);

    // Cria a conta do time e o administrador se não existirem.
    public async Task Garantir(CancellationToken ct = default)
    {
        var agora = new BsonDateTime(DateTime.UtcNow);
        await armazenamento.Colecao(Colecoes.Contas).UpdateOneAsync(Bson.Filtro.Eq("_id", ContaId), Bson.Mudar
            .SetOnInsert("sigla", Sigla)
            .SetOnInsert("dominio", "dboaz")
            .SetOnInsert("nome", "Time deboaz")
            .SetOnInsert("tipo", "INSTITUICAO")
            .SetOnInsert("estado", "ATIVA")
            .SetOnInsert("documento", "")
            .SetOnInsert("responsavel", "")
            .SetOnInsert("telefone", "")
            .SetOnInsert("criadaEm", agora),
            new UpdateOptions { IsUpsert = true }, ct);
        await armazenamento.Colecao(Colecoes.Usuarios).UpdateOneAsync(Bson.Filtro.Eq("_id", AdminId), Bson.Mudar
            .SetOnInsert("contaId", ContaId)
            .SetOnInsert("sigla", Sigla)
            .SetOnInsert("tipo", "ADMINISTRADOR")
            .SetOnInsert("nome", "Administrador do deboaz")
            .SetOnInsert("nomeCurto", "admin")
            .SetOnInsert("login", EntradaDoAdmin)
            .SetOnInsert("matricula", BsonNull.Value)
            .SetOnInsert("estado", "ATIVO")
            .SetOnInsert("resumoSenha", Bson.Valor(resumoDaSenha.Value))
            .SetOnInsert("responsaveis", new BsonArray())
            .SetOnInsert("criadoPor", BsonNull.Value)
            .SetOnInsert("criadoEm", agora),
            new UpdateOptions { IsUpsert = true }, ct);
    }

    // A busca falhou e é pelo time? Cria o time e diz se vale buscar de novo.
    public async Task<bool> GarantirSeFor(string? usuarioId = null, string? contaId = null, string? entrada = null, CancellationToken ct = default)
    {
        if (usuarioId != AdminId && contaId != ContaId && entrada != EntradaDoAdmin) return false;
        await Garantir(ct);
        return true;
    }
}
