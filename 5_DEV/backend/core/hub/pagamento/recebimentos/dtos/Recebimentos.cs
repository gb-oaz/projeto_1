using Boaz.Core.Hub.Pagamento.Faturas.Domain;

namespace Boaz.Core.Hub.Pagamento.Recebimentos.Dtos;

/// <summary>Um número do resumo: o valor e quantas faturas.</summary>
/// <param name="Valor">Em reais.</param>
/// <param name="Faturas">Quantas faturas.</param>
public sealed record NumeroDto(decimal Valor, int Faturas);

/// <summary>O recebido de uma forma (RF-190).</summary>
/// <param name="Forma">PIX.</param>
/// <param name="Valor">O recebido.</param>
/// <param name="Faturas">Quantas faturas.</param>
public sealed record PorFormaDto(string Forma, decimal Valor, int Faturas);

/// <summary>Os números de um tipo de conta (RF-190).</summary>
/// <param name="Tipo">INSTITUICAO ou AUTONOMO.</param>
/// <param name="Recebido">O recebido.</param>
/// <param name="Pagas">Quantas pagas.</param>
/// <param name="Vencido">O vencido, sem os juros.</param>
/// <param name="Vencidas">Quantas vencidas.</param>
public sealed record PorTipoDeContaDto(string Tipo, decimal Recebido, int Pagas, decimal Vencido, int Vencidas);

/// <summary>200 de GET /pagamento/recebimentos/resumo (RF-190, RN-308).</summary>
/// <param name="Competencia">2026-10.</param>
/// <param name="Faturado">As faturas da competência, sem as canceladas.</param>
/// <param name="Recebido">O pago nas faturas, com juros, menos o estornado.</param>
/// <param name="AReceber">As "A_PAGAR", ainda no prazo.</param>
/// <param name="Vencido">As "VENCIDA", sem os juros.</param>
/// <param name="Inadimplencia">O vencido dividido pelo faturado, em %, com uma casa.</param>
/// <param name="Duplicidades">Pagamentos de faturas já pagas ou canceladas (RN-303).</param>
/// <param name="PorForma">As quatro formas, na ordem da tela.</param>
/// <param name="PorTipoDeConta">INSTITUICAO e AUTONOMO.</param>
public sealed record ResumoDosRecebimentosDto(string Competencia, NumeroDto Faturado, NumeroDto Recebido, NumeroDto AReceber, NumeroDto Vencido,
    decimal Inadimplencia, int Duplicidades, IReadOnlyList<PorFormaDto> PorForma, IReadOnlyList<PorTipoDeContaDto> PorTipoDeConta);

/// <summary>Uma competência de GET /pagamento/recebimentos/competencias (RF-190).</summary>
/// <param name="Competencia">2026-10.</param>
/// <param name="Faturado">O faturado, em reais.</param>
/// <param name="Recebido">O recebido, em reais.</param>
/// <param name="Vencido">O vencido, em reais.</param>
/// <param name="Inadimplencia">Em %, com uma casa.</param>
public sealed record CompetenciaDto(string Competencia, decimal Faturado, decimal Recebido, decimal Vencido, decimal Inadimplencia);

/// <summary>A conta num item da lista do recebedor.</summary>
/// <param name="Id">conta-pedro-reis.</param>
/// <param name="Nome">Pedro Reis.</param>
/// <param name="Tipo">AUTONOMO.</param>
public sealed record ContaDoItemDto(string Id, string Nome, string Tipo);

/// <summary>Um item de GET /pagamento/recebimentos/faturas (RF-191).</summary>
/// <param name="Id">FAT-000371.</param>
/// <param name="Conta">A conta.</param>
/// <param name="Competencia">2026-10.</param>
/// <param name="Valor">294.00.</param>
/// <param name="Vencimento">2026-11-10.</param>
/// <param name="Status">VENCIDA.</param>
/// <param name="DiasDeAtraso">Dias desde o vencimento; 0 se não está vencida.</param>
public sealed record FaturaDoRecebedorDto(string Id, ContaDoItemDto Conta, string Competencia, decimal Valor, DateOnly Vencimento, string Status,
    int DiasDeAtraso)
{
    /// <summary>Da fatura, com o atraso de hoje.</summary>
    public static FaturaDoRecebedorDto De(Fatura f, DateOnly hoje) =>
        new(f.Id, new ContaDoItemDto(f.Conta.Id, f.Conta.Nome, f.Conta.Tipo), f.Competencia.ToString(), f.Valor.Reais, f.Vencimento, f.Status.Texto(),
            f.DiasDeAtraso(hoje));
}

/// <summary>200 de GET /pagamento/recebimentos/faturas (RF-191).</summary>
/// <param name="Itens">Até 50 por página.</param>
/// <param name="Total">Quantas há no filtro.</param>
public sealed record FaturasDoRecebedorDto(IReadOnlyList<FaturaDoRecebedorDto> Itens, int Total);

/// <summary>O corpo de POST /pagamento/recebimentos/faturas/{id}/cancelar (RF-191).</summary>
/// <param name="Motivo">Obrigatório.</param>
public sealed record PedidoDeCancelamento(string? Motivo);

/// <summary>O corpo de POST /pagamento/recebimentos/pagamentos/{id}/estornos (RF-192).</summary>
/// <param name="Valor">Maior que zero, com até duas casas.</param>
/// <param name="Motivo">Obrigatório.</param>
public sealed record PedidoDeEstornoDto(decimal? Valor, string? Motivo)
{
    /// <summary>O valor e o motivo conferidos (RN-306).</summary>
    /// <exception cref="FalhaDoPagamento">DADOS_INVALIDOS em "valor" ou "motivo".</exception>
    public (Dinheiro Valor, string Motivo) Validar()
    {
        if (Valor is not { } valor || valor <= 0m || !Dinheiro.TemDuasCasas(valor)) throw FalhaDoPagamento.DadosInvalidos("valor");
        return string.IsNullOrWhiteSpace(Motivo) ? throw FalhaDoPagamento.DadosInvalidos("motivo") : (Dinheiro.De(valor), Motivo.Trim());
    }
}

/// <summary>201 de POST /pagamento/recebimentos/pagamentos/{id}/estornos (RF-192).</summary>
/// <param name="Id">EST-000012.</param>
/// <param name="PagamentoId">PAG-000773.</param>
/// <param name="Valor">1473.92.</param>
/// <param name="Motivo">Quiz cancelado.</param>
/// <param name="Status">PENDENTE.</param>
/// <param name="PedidoEm">Quando foi pedido.</param>
public sealed record EstornoDto(string Id, string PagamentoId, decimal Valor, string Motivo, string Status, DateTimeOffset PedidoEm);
