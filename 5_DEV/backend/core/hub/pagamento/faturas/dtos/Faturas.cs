using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;

namespace Boaz.Core.Hub.Pagamento.Faturas.Dtos;

// dentro do namespace: "Pagamento" sozinho seria o namespace Boaz.Core.Hub.Pagamento
using Pagamento = Boaz.Core.Hub.Pagamento.Pagamentos.Domain.Pagamento;

/// <summary>Quem pede ao Pagamento, como o token conferido diz.</summary>
/// <param name="UsuarioId">O sub do token.</param>
/// <param name="Papel">MASTER, AUTONOMO, STAFF, ADMINISTRADOR, MEMBRO_TIME…</param>
/// <param name="ContaId">A conta do token.</param>
/// <param name="UsaPagamento">O token tem MOD-01 com "U" (RN-298).</param>
public sealed record QuemPede(string UsuarioId, string Papel, string ContaId, bool UsaPagamento = true)
{
    /// <summary>O papel do recebedor: só ele, e não se delega (RN-298).</summary>
    public const string Administrador = "ADMINISTRADOR";

    /// <summary>O administrador do deboaz.</summary>
    public bool Recebedor => Papel == Administrador;

    /// <summary>Só o administrador do deboaz (RF-190 a RF-193).</summary>
    /// <exception cref="FalhaDoPagamento">ACESSO_NEGADO.</exception>
    public void SoRecebedor()
    {
        if (!Recebedor) throw FalhaDoPagamento.AcessoNegado();
    }
}

/// <summary>O pagamento em aberto de um item da lista (RN-303).</summary>
/// <param name="Id">PAG-000785.</param>
/// <param name="Forma">BOLETO.</param>
/// <param name="Status">AGUARDANDO ou PROCESSANDO.</param>
public sealed record PagamentoEmAbertoDto(string Id, string Forma, string Status);

/// <summary>Um item de GET /pagamento/faturas (RF-182).</summary>
/// <param name="Id">FAT-000402.</param>
/// <param name="Competencia">2026-11.</param>
/// <param name="Valor">3698.33.</param>
/// <param name="Vencimento">2026-12-10.</param>
/// <param name="Status">A_PAGAR.</param>
/// <param name="PagamentoEmAberto">O pagamento em aberto, ou null.</param>
public sealed record ItemDeFaturaDto(string Id, string Competencia, decimal Valor, DateOnly Vencimento, string Status,
    PagamentoEmAbertoDto? PagamentoEmAberto);

/// <summary>O resumo do topo da lista (RF-182).</summary>
/// <param name="APagar">O total a pagar hoje, com os juros.</param>
/// <param name="Vencidas">Quantas estão vencidas.</param>
/// <param name="ProximoVencimento">O vencimento mais próximo das "A_PAGAR"; null sem nenhuma.</param>
public sealed record ResumoDasFaturasDto(decimal APagar, int Vencidas, DateOnly? ProximoVencimento);

/// <summary>200 de GET /pagamento/faturas (RF-182).</summary>
/// <param name="Itens">Até 50 por página.</param>
/// <param name="Total">Quantas faturas há na situação.</param>
/// <param name="Resumo">O resumo da conta.</param>
public sealed record ListaDeFaturasDto(IReadOnlyList<ItemDeFaturaDto> Itens, int Total, ResumoDasFaturasDto Resumo);

/// <summary>A conta no detalhe da fatura.</summary>
/// <param name="Id">conta-ebfga.</param>
/// <param name="Sigla">EBFGA.</param>
/// <param name="Nome">Escola Bento Freitas.</param>
public sealed record ContaDaFaturaDto(string Id, string Sigla, string Nome);

/// <summary>Um módulo cobrado.</summary>
/// <param name="ModuloId">MOD-03.</param>
/// <param name="Nome">Usuários.</param>
/// <param name="CobrancaId">cob-ebfga-MOD-03-2026-11.</param>
/// <param name="Valor">2224.41.</param>
public sealed record ItemCobradoDto(string ModuloId, string Nome, string CobrancaId, decimal Valor);

/// <summary>Os juros de hoje (RN-310).</summary>
/// <param name="TaxaAoDia">0.1.</param>
/// <param name="Dias">4.</param>
/// <param name="Valor">1.18.</param>
public sealed record JurosDto(decimal TaxaAoDia, int Dias, decimal Valor);

/// <summary>Uma tentativa de pagamento no detalhe (RN-304).</summary>
/// <param name="Id">PAG-000773.</param>
/// <param name="Forma">CARTAO_CREDITO.</param>
/// <param name="Status">PAGO.</param>
/// <param name="CriadoEm">Quando nasceu.</param>
/// <param name="PagoPor">Quem pediu, quando o banco confirmou; null nos outros.</param>
/// <param name="TraceId">A execução que o criou (RNF-244).</param>
/// <param name="Valor">O valor cobrado.</param>
public sealed record TentativaDto(string Id, string Forma, string Status, DateTimeOffset CriadoEm, string? PagoPor, string TraceId, decimal Valor);

/// <summary>200 de GET /pagamento/faturas/{id} (RF-183) e do cancelamento (RF-191).</summary>
/// <param name="Id">FAT-000402.</param>
/// <param name="Conta">A conta.</param>
/// <param name="Competencia">2026-11.</param>
/// <param name="EmitidaEm">2026-12-01T03:00:00Z.</param>
/// <param name="Vencimento">2026-12-10.</param>
/// <param name="Status">A_PAGAR.</param>
/// <param name="Itens">Os módulos cobrados.</param>
/// <param name="Valor">A soma dos itens.</param>
/// <param name="PagaEm">Quando foi paga.</param>
/// <param name="Devolvido">O já estornado.</param>
/// <param name="Juros">Os juros de hoje; null sem atraso ou com taxa 0.</param>
/// <param name="TotalAPagar">O valor e os juros; 0.00 na paga e na cancelada.</param>
/// <param name="Pagamentos">As tentativas, a mais nova primeiro.</param>
/// <param name="CanceladaEm">Quando foi cancelada.</param>
/// <param name="Motivo">O motivo do cancelamento.</param>
public sealed record FaturaDto(
    string Id, ContaDaFaturaDto Conta, string Competencia, DateTimeOffset EmitidaEm, DateOnly Vencimento, string Status,
    IReadOnlyList<ItemCobradoDto> Itens, decimal Valor, DateTimeOffset? PagaEm, decimal Devolvido, JurosDto? Juros, decimal TotalAPagar,
    IReadOnlyList<TentativaDto> Pagamentos, DateTimeOffset? CanceladaEm, string? Motivo)
{
    /// <summary>O detalhe, com os juros de hoje e as tentativas com o nome de quem pagou.</summary>
    public static FaturaDto De(Fatura fatura, JurosDaFatura? juros, IEnumerable<Pagamento> pagamentos, IReadOnlyDictionary<string, string> nomes) =>
        new(fatura.Id, new ContaDaFaturaDto(fatura.Conta.Id, fatura.Conta.Sigla, fatura.Conta.Nome), fatura.Competencia.ToString(),
            fatura.EmitidaEm, fatura.Vencimento, fatura.Status.Texto(),
            [.. fatura.Itens.Select(i => new ItemCobradoDto(i.ModuloId, i.Nome, i.CobrancaId, i.Valor.Reais))],
            fatura.Valor.Reais, fatura.PagaEm, fatura.Devolvido.Reais,
            juros is null ? null : new JurosDto(juros.TaxaAoDia, juros.Dias, juros.Valor.Reais),
            fatura.TotalAPagar(juros).Reais,
            [.. pagamentos.OrderByDescending(p => p.CriadoEm).ThenByDescending(p => p.Id, StringComparer.Ordinal)
                .Select(p => new TentativaDto(p.Id, p.Forma.Texto(), p.Status.Texto(), p.CriadoEm,
                    p.PagoEm is null ? null : nomes.GetValueOrDefault(p.CriadoPor), p.TraceId, p.Valor.Reais))],
            fatura.Cancelamento?.Em, fatura.Cancelamento?.Motivo);
}

/// <summary>Um aviso de atraso (RN-311).</summary>
/// <param name="Tipo">FATURA_EM_ATRASO.</param>
/// <param name="FaturaId">FAT-000371.</param>
/// <param name="Vencimento">2026-11-10.</param>
/// <param name="ValorFatura">294.00.</param>
/// <param name="Juros">Os juros de hoje.</param>
/// <param name="TotalAPagar">A fatura e os juros.</param>
/// <param name="Desde">Desde quando o aviso aparece.</param>
public sealed record AvisoDto(string Tipo, string FaturaId, DateOnly Vencimento, decimal ValorFatura, decimal Juros, decimal TotalAPagar,
    DateTimeOffset Desde);

/// <summary>200 de GET /pagamento/avisos (RN-311).</summary>
/// <param name="Itens">Os avisos.</param>
/// <param name="Total">Quantos.</param>
public sealed record ListaDeAvisosDto(IReadOnlyList<AvisoDto> Itens, int Total);
