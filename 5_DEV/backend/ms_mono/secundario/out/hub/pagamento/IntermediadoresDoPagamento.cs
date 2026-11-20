using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;

namespace Boaz.MsMono.Secundario.Out.Hub.Pagamento;

// Os intermediadores do ambiente (RNF-242, pagamento-hexagonal v03), montados
// no Program.cs: o ligado cobra os pagamentos novos; os disponíveis (o Mercado
// Pago com a chave de acesso, o falso só fora de produção) seguem atendendo os
// pagamentos que geraram. O aviso vai ao dono do cabeçalho de assinatura:
// x-signature e x-request-id → Mercado Pago; X-Assinatura "sha256=…" → falso;
// nenhum dos dois, ou o de um intermediador que o ambiente não tem (o falso em
// produção) → o ligado, cuja assinatura não confere (401, registrado com o nome dele).
public sealed class IntermediadoresDoPagamento : IIntermediadores
{
    readonly Dictionary<string, IIntermediador> disponiveis;

    public IntermediadoresDoPagamento(IIntermediador ligado, IEnumerable<IIntermediador> outros)
    {
        Ligado = ligado;
        disponiveis = new Dictionary<string, IIntermediador>(StringComparer.Ordinal) { [ligado.Nome] = ligado };
        foreach (var outro in outros) disponiveis.TryAdd(outro.Nome, outro);
    }

    public IIntermediador Ligado { get; }

    // o pagamento de antes do campo "intermediador", ou de um que o ambiente não tem mais, fica com o ligado
    public IIntermediador Do(string? nome) =>
        nome is { Length: > 0 } && disponiveis.TryGetValue(nome, out var intermediador) ? intermediador : Ligado;

    public IIntermediador? DoAviso(AvisoRecebido recebido)
    {
        var cabecalhos = recebido.Cabecalhos;
        string? nome = null;
        if (cabecalhos.ContainsKey(IntermediadorMercadoPago.Assinatura) && cabecalhos.ContainsKey(IntermediadorMercadoPago.Pedido))
        {
            nome = Core.Hub.Pagamento.Pagamentos.Domain.NomeDoIntermediador.MercadoPago;
        }
        else if (cabecalhos.GetValueOrDefault(IntermediadorFalso.Assinatura)?.StartsWith(IntermediadorFalso.PrefixoDaAssinatura, StringComparison.Ordinal)
            == true)
        {
            nome = Core.Hub.Pagamento.Pagamentos.Domain.NomeDoIntermediador.Falso;
        }
        return nome is not null && disponiveis.TryGetValue(nome, out var dono) ? dono : Ligado;
    }
}
