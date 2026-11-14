using System.Globalization;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Hub.Sac.Card.Domain;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.MsMono.Secundario.Out.Commons;
using MongoDB.Bson;
using MongoDB.Driver;
using Boaz.Core.Hub.Sac.Card.Ports.Out;

namespace Boaz.MsMono.Secundario.Out.Hub.Sac;

// O card do SAC (RN-295): o dia corrente é contado em chamados, numa só
// agregação por escopo (a conta, ou todas para o time); a versão é a soma das
// versões dos chamados do escopo, que sobe a cada mudança. cardSac guarda os
// dias fechados à 00:00 (15 RN-278), de onde saem as séries.
public sealed class CardDoSacNoBanco(Armazenamento armazenamento) : IContagemDoSac, ISeriesDoSac
{
    public async Task<ContagemDoSac> Contar(string? contaId, DateTimeOffset agora, CancellationToken ct = default)
    {
        var hoje = CalendarioDoCard.Dia(agora);
        var inicioDoMes = new DateOnly(hoje.Year, hoje.Month, 1).ToDateTime(TimeOnly.MinValue);
        var mes = new DateTimeOffset(inicioDoMes, CalendarioDoCard.Fuso.GetUtcOffset(inicioDoMes)).UtcDateTime;
        var andamento = new BsonArray
        {
            StatusDoChamado.Aberto.Texto(), StatusDoChamado.EmAtendimento.Texto(), StatusDoChamado.AguardandoCliente.Texto(),
        };
        static BsonDocument Se(BsonValue condicao) => new("$sum", new BsonDocument("$cond", new BsonArray { condicao, 1, 0 }));
        static BsonDocument Status(string status) => new("$eq", new BsonArray { "$status", status });
        var emAndamento = new BsonDocument("$in", new BsonArray { "$status", andamento });
        var grupo = new BsonDocument
        {
            ["_id"] = BsonNull.Value,
            ["total"] = new BsonDocument("$sum", 1),
            ["emAndamento"] = Se(emAndamento),
            ["aguardando"] = Se(Status(StatusDoChamado.AguardandoCliente.Texto())),
            ["emAtendimento"] = Se(Status(StatusDoChamado.EmAtendimento.Texto())),
            ["resolvidosNoMes"] = Se(new BsonDocument("$gte", new BsonArray { "$resolvidoEm", new BsonDateTime(mes) })),
            ["naoLidos"] = Se(new BsonDocument("$eq", new BsonArray { "$naoLidoPelaConta", true })),
            ["semResposta"] = Se(new BsonDocument("$and", new BsonArray
            {
                new BsonDocument("$in", new BsonArray { "$status", new BsonArray { StatusDoChamado.Aberto.Texto(), StatusDoChamado.EmAtendimento.Texto() } }),
                new BsonDocument("$ne", new BsonArray { "$esperandoTimeDesde", BsonNull.Value }),
            })),
            ["atrasados"] = Se(new BsonDocument("$and", new BsonArray
            {
                emAndamento,
                new BsonDocument("$eq", new BsonArray { "$primeiraRespostaEm", BsonNull.Value }),
                new BsonDocument("$lt", new BsonArray { "$prazoResposta", new BsonDateTime(agora.UtcDateTime) }),
            })),
            ["notas"] = Se(new BsonDocument("$gt", new BsonArray { "$avaliacao.nota", BsonNull.Value })),
            ["somaDasNotas"] = new BsonDocument("$sum", "$avaliacao.nota"),
            ["versao"] = new BsonDocument("$sum", "$versao"),
            ["atualizadoEm"] = new BsonDocument("$max", "$atualizadoEm"),
        };
        var onde = contaId is null ? new BsonDocument() : new BsonDocument("contaId", contaId);
        var r = await armazenamento.Colecao(Colecoes.Chamados)
            .Aggregate<BsonDocument>(new BsonDocument[] { new("$match", onde), new("$group", grupo) }, cancellationToken: ct)
            .FirstOrDefaultAsync(ct);
        if (r is null) return new ContagemDoSac(0, 0, 0, 0, 0, 0, 0, 0, null, 0, null);
        var notas = r.Inteiro("notas");
        decimal? media = notas == 0 ? null : Math.Round((decimal)r["somaDasNotas"].ToDouble() / notas, 1, MidpointRounding.AwayFromZero);
        return new ContagemDoSac(r.Inteiro("total"), r.Inteiro("emAndamento"), r.Inteiro("aguardando"), r.Inteiro("emAtendimento"),
            r.Inteiro("resolvidosNoMes"), r.Inteiro("naoLidos"), r.Inteiro("semResposta"), r.Inteiro("atrasados"), media, r.Longo("versao"),
            r.Data("atualizadoEm"));
    }

    public async Task<IReadOnlyList<string>> ContasComChamado(CancellationToken ct = default)
    {
        var contas = await armazenamento.Colecao(Colecoes.Chamados)
            .DistinctAsync<string>("contaId", Bson.Filtro.Ne("contaId", BsonNull.Value), cancellationToken: ct);
        return await contas.ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<DateOnly, int>> Fechados(string escopo, DateOnly desde, CancellationToken ct = default)
    {
        var lidos = await armazenamento.Colecao(Colecoes.CardSac)
            .Find(Bson.Filtro.Eq("escopo", escopo) & Bson.Filtro.Gte("dia", CalendarioDoCard.ChaveDoDia(desde))).ToListAsync(ct);
        return lidos.ToDictionary(
            d => DateOnly.ParseExact(d.Texto("dia")!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            d => d.Inteiro("emAndamento"));
    }

    public Task Fechar(string escopo, DateOnly dia, ContagemDoSac contagem, CancellationToken ct = default)
    {
        var chave = CalendarioDoCard.ChaveDoDia(dia);
        return armazenamento.Colecao(Colecoes.CardSac).ReplaceOneAsync(Bson.Filtro.Eq("_id", $"{escopo}:{chave}"), new BsonDocument
        {
            ["_id"] = $"{escopo}:{chave}",
            ["escopo"] = escopo,
            ["dia"] = chave,
            ["emAndamento"] = contagem.EmAndamento,
            ["resolvidos"] = contagem.ResolvidosNoMes,
            ["semResposta"] = contagem.SemResposta,
            ["atrasados"] = contagem.Atrasados,
            ["notaMedia"] = contagem.NotaMedia is { } m ? (BsonValue)new BsonDecimal128(m) : BsonNull.Value,
        }, new ReplaceOptions { IsUpsert = true }, ct);
    }
}
