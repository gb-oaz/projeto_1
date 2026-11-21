namespace Boaz.Core.Hub.Auditoria.Metricas.Domain;

/// <summary>
/// As faixas do tempo de resposta (RN-317): cada pedido cai na primeira faixa cujo fim é maior ou igual ao tempo.
/// O percentil é o fim da faixa em que ele cai.
/// </summary>
public enum FaixaDeTempo
{
    /// <summary>Até 50 ms.</summary>
    Ate50,
    /// <summary>Até 100 ms.</summary>
    Ate100,
    /// <summary>Até 250 ms.</summary>
    Ate250,
    /// <summary>Até 500 ms.</summary>
    Ate500,
    /// <summary>Até 1 s.</summary>
    Ate1000,
    /// <summary>Até 3 s.</summary>
    Ate3000,
    /// <summary>Até 5 s.</summary>
    Ate5000,
    /// <summary>Até 10 s.</summary>
    Ate10000,
    /// <summary>Acima de 10 s.</summary>
    Mais,
}

/// <summary>As faixas pelo tempo e pelo nome gravado no banco (f50 … fMais).</summary>
public static class FaixasDeTempo
{
    static readonly int[] Fins = [50, 100, 250, 500, 1000, 3000, 5000, 10000];

    /// <summary>Quantas faixas há.</summary>
    public static int Quantas => Enum.GetValues<FaixaDeTempo>().Length;

    /// <summary>A faixa do tempo dado.</summary>
    public static FaixaDeTempo De(long duracaoMs)
    {
        for (var i = 0; i < Fins.Length; i++)
        {
            if (duracaoMs <= Fins[i]) return (FaixaDeTempo)i;
        }
        return FaixaDeTempo.Mais;
    }

    /// <summary>O fim da faixa, em ms; null em "Mais".</summary>
    public static int? FimMs(this FaixaDeTempo faixa) => faixa == FaixaDeTempo.Mais ? null : Fins[(int)faixa];

    /// <summary>O campo do documento: f50, f100 … f10000, fMais.</summary>
    public static string Campo(this FaixaDeTempo faixa) => faixa == FaixaDeTempo.Mais ? "fMais" : $"f{Fins[(int)faixa]}";

    /// <summary>Os campos das 9 faixas, na ordem.</summary>
    public static IReadOnlyList<string> Campos { get; } = [.. Enum.GetValues<FaixaDeTempo>().Select(f => f.Campo())];
}

/// <summary>A contagem de pedidos por faixa do tempo de resposta, de onde saem os percentis (RN-317).</summary>
public sealed record Histograma(IReadOnlyList<long> Contagens)
{
    /// <summary>Nenhum pedido.</summary>
    public static Histograma Vazio { get; } = new(new long[FaixasDeTempo.Quantas]);

    /// <summary>Quantos pedidos.</summary>
    public long Total => Contagens.Sum();

    /// <summary>A soma com outro.</summary>
    public Histograma Somar(Histograma outro) => new([.. Contagens.Zip(outro.Contagens, (a, b) => a + b)]);

    /// <summary>
    /// O fim da faixa onde cai o percentil (50, 95 ou 99): a primeira faixa em que a soma passa a ser pelo menos o
    /// posto do percentil. Em "Mais" (acima de 10 s) ou sem pedido, null.
    /// </summary>
    public int? Percentil(int percentil)
    {
        var total = Total;
        if (total == 0) return null;
        var posto = (long)Math.Ceiling(total * percentil / 100.0);
        long soma = 0;
        for (var i = 0; i < Contagens.Count; i++)
        {
            soma += Contagens[i];
            if (soma >= posto) return ((FaixaDeTempo)i).FimMs();
        }
        return null;
    }
}

/// <summary>
/// Os pedidos de um endereço num minuto ou numa hora (RNF-247, medicoesMinuto e medicoesHora): o método, o modelo
/// do endereço, o módulo, quantos, quantos com cada resultado e as faixas do tempo. Nunca guarda quem pediu, a
/// conta, o corpo nem o id do endereço.
/// </summary>
public sealed class Medicao(DateTimeOffset inicio, string metodo, string endereco, string? modulo)
{
    readonly Lock trava = new();
    readonly long[] tempos = new long[FaixasDeTempo.Quantas];
    long pedidos, ok, erros4xx, erros5xx;
    bool fechada;

    /// <summary>O começo do minuto ou da hora.</summary>
    public DateTimeOffset Inicio { get; } = inicio;

    /// <summary>GET, POST…</summary>
    public string Metodo { get; } = metodo;

    /// <summary>O modelo da rota, como /pagamento/faturas/{id}/pagamentos.</summary>
    public string Endereco { get; } = endereco;

    /// <summary>O módulo do endereço; null fora dos módulos.</summary>
    public string? Modulo { get; } = modulo;

    /// <summary>Quantos pedidos.</summary>
    public long Pedidos { get { lock (trava) return pedidos; } }

    /// <summary>Com 1xx a 3xx.</summary>
    public long Ok { get { lock (trava) return ok; } }

    /// <summary>Com 4xx.</summary>
    public long Erros4xx { get { lock (trava) return erros4xx; } }

    /// <summary>Com 5xx.</summary>
    public long Erros5xx { get { lock (trava) return erros5xx; } }

    /// <summary>As faixas do tempo.</summary>
    public Histograma Tempos { get { lock (trava) return new Histograma([.. tempos]); } }

    /// <summary>Taxa de erros 5xx, em %, com 2 casas; null sem pedido.</summary>
    public decimal? TaxaErro5xx => Pedidos == 0 ? null : Math.Round(100m * Erros5xx / Pedidos, 2, MidpointRounding.AwayFromZero);

    /// <summary>Lento: o p95 passa de 1 s, ou está acima de 10 s (RF-198).</summary>
    public bool Lento => Tempos.Percentil(95) is null or > 1000;

    /// <summary>Soma pedidos com o status e o tempo. false se a medição já foi retirada para gravar.</summary>
    public bool Somar(int status, long duracaoMs, long quantidade = 1)
    {
        lock (trava)
        {
            if (fechada) return false;
            pedidos += quantidade;
            if (status >= 500) erros5xx += quantidade;
            else if (status >= 400) erros4xx += quantidade;
            else ok += quantidade;
            tempos[(int)FaixasDeTempo.De(duracaoMs)] += quantidade;
            return true;
        }
    }

    /// <summary>Fecha a medição para gravar: nada mais é somado nela.</summary>
    public void Fechar()
    {
        lock (trava) fechada = true;
    }
}

/// <summary>Os pedidos somados de um conjunto de medições: o que a saúde e as métricas leem.</summary>
/// <param name="Pedidos">Quantos pedidos.</param>
/// <param name="Erros4xx">Com 4xx.</param>
/// <param name="Erros5xx">Com 5xx.</param>
/// <param name="Tempos">As faixas do tempo.</param>
public sealed record ResumoDeMedicoes(long Pedidos, long Erros4xx, long Erros5xx, Histograma Tempos)
{
    /// <summary>Sem nenhum pedido.</summary>
    public static ResumoDeMedicoes Vazio { get; } = new(0, 0, 0, Histograma.Vazio);

    /// <summary>Taxa de erros 5xx, em %, com 2 casas; null sem pedido.</summary>
    public decimal? TaxaErro5xx => Pedidos == 0 ? null : Math.Round(100m * Erros5xx / Pedidos, 2, MidpointRounding.AwayFromZero);

    /// <summary>O p95 em ms; null sem pedido ou acima de 10 s.</summary>
    public int? P95Ms => Tempos.Percentil(95);

    /// <summary>Lento: o p95 passa de 1 s, ou está acima de 10 s (RF-198).</summary>
    public bool Lento => P95Ms is null or > 1000;

    /// <summary>A soma com outro.</summary>
    public ResumoDeMedicoes Somar(ResumoDeMedicoes outro) =>
        new(Pedidos + outro.Pedidos, Erros4xx + outro.Erros4xx, Erros5xx + outro.Erros5xx, Tempos.Somar(outro.Tempos));
}

/// <summary>Os endereços e os módulos que a Auditoria mede (RN-316).</summary>
public static class ModuloDoEndereco
{
    /// <summary>Os módulos medidos, com o nome.</summary>
    public static IReadOnlyDictionary<string, string> Modulos { get; } = new Dictionary<string, string>
    {
        ["MOD-01"] = "Pagamento",
        ["MOD-02"] = "SAC",
        ["MOD-03"] = "Usuários",
        ["MOD-14"] = "Catálogo",
        ["MOD-17"] = "Auditoria",
    };

    /// <summary>O módulo do endereço pelo primeiro trecho: /auth, /usuarios, /contas → MOD-03; /sac; /pagamento; /catalogo; /auditoria; outro → null.</summary>
    public static string? De(string endereco)
    {
        var trecho = endereco.TrimStart('/').Split('/', 2)[0];
        return trecho switch
        {
            "auth" or "usuarios" or "contas" => "MOD-03",
            "sac" => "MOD-02",
            "pagamento" => "MOD-01",
            "catalogo" => "MOD-14",
            "auditoria" => "MOD-17",
            _ => null,
        };
    }

    /// <summary>O módulo é um dos medidos.</summary>
    public static bool Valido(string? modulo) => modulo is not null && Modulos.ContainsKey(modulo);
}
