using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boaz.MsMono.Infra.Out.Commons.Usuario;

// A conta do time deboaz e o administrador não nascem por endereço: vêm da
// configuração (seções Time e Administrador, em Configuracoes.cs). A sigla e o
// id da conta saem da regra do core (Sigla.DoTime → conta-dboaz); o id do
// administrador, da sigla e do nome curto configurado (usr-dboaz-admin).
// Garantir() os cria se faltarem, sem tocar no que já existe; os adaptadores
// chamam quando uma busca pelo time não acha nada, para o time voltar mesmo
// depois de o banco ser esvaziado (como o qa faz).
public sealed class TimeDeboaz(Armazenamento armazenamento, IOptions<OpcoesDoTime> time, IOptions<OpcoesDoAdministrador> administrador)
{
    // A sigla do time, regra do core (RN-179).
    public static string Sigla => Core.Commons.Usuario.Usuarios.Domain.Sigla.DoTime;

    // A conta do time: conta-<sigla>, como toda conta.
    public static string ContaId => ContaAssinante.IdDa(Sigla);

    // O administrador: usr-<sigla>-<nome curto>, como todo usuário.
    public string AdminId => $"usr-{Sigla.ToLowerInvariant()}-{administrador.Value.NomeCurto}";

    // Entrada do administrador, em minúsculas como toda entrada.
    public string EntradaDoAdmin => administrador.Value.EntradaNormalizada();

    // o resumo da senha custa caro (PBKDF2): sai uma vez por processo
    readonly Lazy<string?> resumoDaSenha = new(() =>
        administrador.Value.Senha is { Length: > 0 } senha ? ResumoDeSenha.Gerar(senha) : null);

    // Cria a conta do time e o administrador se não existirem.
    public async Task Garantir(CancellationToken ct = default)
    {
        var agora = new BsonDateTime(DateTime.UtcNow);
        var conta = time.Value;
        var admin = administrador.Value;
        await armazenamento.Colecao(Colecoes.Contas).UpdateOneAsync(Bson.Filtro.Eq("_id", ContaId), Bson.Mudar
            .SetOnInsert("sigla", Sigla)
            .SetOnInsert("dominio", Sigla.ToLowerInvariant())
            .SetOnInsert("nome", conta.Nome)
            .SetOnInsert("tipo", "INSTITUICAO")
            .SetOnInsert("estado", "ATIVA")
            .SetOnInsert("documento", conta.Documento)
            .SetOnInsert("responsavel", conta.Responsavel)
            .SetOnInsert("telefone", conta.Telefone)
            .SetOnInsert("criadaEm", agora),
            new UpdateOptions { IsUpsert = true }, ct);
        await armazenamento.Colecao(Colecoes.Usuarios).UpdateOneAsync(Bson.Filtro.Eq("_id", AdminId), Bson.Mudar
            .SetOnInsert("contaId", ContaId)
            .SetOnInsert("sigla", Sigla)
            .SetOnInsert("tipo", "ADMINISTRADOR")
            .SetOnInsert("nome", admin.Nome)
            .SetOnInsert("nomeCurto", admin.NomeCurto)
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
