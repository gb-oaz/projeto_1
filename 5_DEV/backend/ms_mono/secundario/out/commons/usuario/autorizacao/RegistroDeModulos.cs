using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;
using MongoDB.Bson;
using MongoDB.Driver;
using static Boaz.Core.Commons.Usuario.Autorizacao.Domain.FrenteDeUso;
using static Boaz.Core.Commons.Usuario.Autorizacao.Domain.Perfil;

namespace Boaz.MsMono.Secundario.Out.Commons.Usuario.Autorizacao;

// O registro de módulos (15_modulos): id, nome, tipo e quem usa cada um em que
// frente. O backend grava o registro na coleção modulos ao subir; a leitura
// pega o nome e o tipo do banco e completa quem usa pelo registro daqui, para
// um módulo gravado sem esse campo (pelo qa, por exemplo) também valer.
public sealed class RegistroDeModulos(Armazenamento armazenamento) : IModulos
{
    // Os módulos do 15_modulos.
    public static readonly IReadOnlyList<ModuloRegistrado> Todos =
    [
        Modulo("MOD-01", "Contas", TipoDeModulo.DaConta, (Master, Hub), (Autonomo, Hub)),
        Modulo("MOD-02", "SAC", TipoDeModulo.DaConta, (Master, Hub), (Autonomo, Hub), (MembroTime, Hub)),
        Modulo("MOD-03", "Usuários", TipoDeModulo.DaConta, (Administrador, Hub), (MembroTime, Hub), (Master, Hub), (Autonomo, Hub), (Staff, Hub)),
        Modulo("MOD-04", "Sugestão", TipoDeModulo.DaConta, (Staff, Hub), (Master, Hub)),
        Modulo("MOD-05", "Quiz", TipoDeModulo.Contratavel, (Staff, Hub), (Autonomo, Hub), (Participante, Quest), (Anonimo, Quest)),
        Modulo("MOD-06", "Presença", TipoDeModulo.Contratavel, (Master, Hub), (Staff, Hub), (Participante, Quest), (Responsavel, Quest)),
        Modulo("MOD-07", "Carteirinha", TipoDeModulo.Contratavel, (Master, Hub), (Participante, Quest), (Responsavel, Quest)),
        Modulo("MOD-08", "Turmas", TipoDeModulo.DaConta, (Master, Hub), (Staff, Hub)),
        Modulo("MOD-09", "Tarefas", TipoDeModulo.DaConta, (Staff, Hub), (Autonomo, Hub), (Participante, Quest)),
        Modulo("MOD-12", "Agenda", TipoDeModulo.Contratavel, (Master, Hub), (Participante, Quest), (Responsavel, Quest)),
        Modulo("MOD-13", "Perfil", TipoDeModulo.DaConta, (Participante, Quest), (Responsavel, Quest)),
        Modulo("MOD-14", "Catálogo", TipoDeModulo.DoTime, (Administrador, Hub), (MembroTime, Hub)),
    ];

    static readonly Dictionary<string, ModuloRegistrado> PorId = Todos.ToDictionary(m => m.Id);

    // Grava o registro no banco, sem apagar módulos de fora dele.
    public async Task Gravar(CancellationToken ct = default)
    {
        var modulos = armazenamento.Colecao(Colecoes.Modulos);
        foreach (var m in Todos)
        {
            await modulos.UpdateOneAsync(Bson.Filtro.Eq("_id", m.Id), Bson.Mudar
                .Set("nome", m.Nome)
                .Set("tipo", Texto(m.Tipo))
                .Set("quemUsa", new BsonArray(m.QuemUsa.Select(q => new BsonDocument
                {
                    ["perfil"] = q.Perfil.ToString().ToUpperInvariant(),
                    ["frente"] = q.Frente.ToString().ToUpperInvariant(),
                }))),
                new UpdateOptions { IsUpsert = true }, ct);
        }
    }

    public async Task<IReadOnlyList<ModuloRegistrado>> Registrados(CancellationToken ct = default)
    {
        var lidos = await armazenamento.Colecao(Colecoes.Modulos).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct);
        if (lidos.Count == 0) return Todos;
        return lidos.Select(d =>
        {
            var id = d["_id"].AsString;
            var conhecido = PorId.GetValueOrDefault(id);
            var tipo = TipoDe(d.Texto("tipo")) ?? conhecido?.Tipo ?? TipoDeModulo.DaConta;
            return new ModuloRegistrado(id, d.Texto("nome") ?? conhecido?.Nome ?? id, tipo, conhecido?.QuemUsa ?? QuemUsa(d));
        }).ToList();
    }

    static IReadOnlySet<(Perfil, FrenteDeUso)> QuemUsa(BsonDocument d) =>
        d.TryGetValue("quemUsa", out var v) && v.IsBsonArray
            ? v.AsBsonArray.OfType<BsonDocument>()
                .Select(q => (Enum.TryParse<Perfil>(q.Texto("perfil"), true, out var p), Enum.TryParse<FrenteDeUso>(q.Texto("frente"), true, out var f), p, f))
                .Where(q => q.Item1 && q.Item2)
                .Select(q => (q.p, q.f))
                .ToHashSet()
            : new HashSet<(Perfil, FrenteDeUso)>();

    static string Texto(TipoDeModulo tipo) => tipo switch
    {
        TipoDeModulo.DaConta => "DA_CONTA",
        TipoDeModulo.Contratavel => "CONTRATAVEL",
        _ => "DO_TIME",
    };

    static TipoDeModulo? TipoDe(string? texto) => texto switch
    {
        "DA_CONTA" => TipoDeModulo.DaConta,
        "CONTRATAVEL" => TipoDeModulo.Contratavel,
        "DO_TIME" => TipoDeModulo.DoTime,
        _ => null,
    };

    static ModuloRegistrado Modulo(string id, string nome, TipoDeModulo tipo, params (Perfil, FrenteDeUso)[] quemUsa) =>
        new(id, nome, tipo, quemUsa.ToHashSet());
}
