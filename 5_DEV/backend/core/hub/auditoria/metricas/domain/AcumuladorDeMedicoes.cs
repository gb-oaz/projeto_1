using System.Collections.Concurrent;

namespace Boaz.Core.Hub.Auditoria.Metricas.Domain;

/// <summary>
/// Soma na memória os pedidos de cada endereço no minuto em que chegaram (RNF-247), sem trava global: o middleware soma a cada
/// pedido e o agendamento retira os minutos fechados, a cada 1 min, para gravar. Não guarda quem pediu, a conta, o corpo
/// nem o id do endereço.
/// </summary>
public sealed class AcumuladorDeMedicoes
{
    /// <summary>O máximo de medições esperando para gravar; o que passa disso é descartado, para um flood de endereços não encher a memória.</summary>
    public const int Maximo = 10_000;

    readonly ConcurrentDictionary<(DateTimeOffset Minuto, string Metodo, string Endereco), Medicao> medicoes = new();

    /// <summary>Soma o pedido no minuto dele.</summary>
    public void Somar(string metodo, string endereco, int status, long duracaoMs, DateTimeOffset agora)
    {
        var minuto = Janelas.Minuto(agora);
        var chave = (minuto, metodo, endereco);
        if (medicoes.Count >= Maximo && !medicoes.ContainsKey(chave)) return;
        while (true)
        {
            var medicao = medicoes.GetOrAdd(chave, _ => new Medicao(minuto, metodo, endereco, ModuloDoEndereco.De(endereco)));
            if (medicao.Somar(status, duracaoMs)) return;
            // retirada para gravar no meio da soma: recomeça num objeto novo
            medicoes.TryRemove(new KeyValuePair<(DateTimeOffset, string, string), Medicao>(chave, medicao));
        }
    }

    /// <summary>Retira as medições dos minutos antes do dado, fechadas para nada mais ser somado nelas.</summary>
    public IReadOnlyList<Medicao> Retirar(DateTimeOffset antesDe)
    {
        var retiradas = new List<Medicao>();
        foreach (var (chave, medicao) in medicoes)
        {
            if (chave.Minuto >= antesDe || !medicoes.TryRemove(new KeyValuePair<(DateTimeOffset, string, string), Medicao>(chave, medicao))) continue;
            medicao.Fechar();
            retiradas.Add(medicao);
        }
        return retiradas;
    }

    /// <summary>Quantas medições esperam para gravar.</summary>
    public int Esperando => medicoes.Count;
}
