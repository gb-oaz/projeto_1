using Boaz.Core.Hub.Pagamento.Configuracao.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Dtos;

namespace Boaz.Core.Hub.Pagamento.Configuracao.Dtos;

/// <summary>Os prazos no corpo (RF-193).</summary>
/// <param name="PixMinutos">De 5 a 1.440.</param>
/// <param name="DebitoMinutos">De 5 a 30.</param>
/// <param name="BoletoDiasUteis">De 1 a 30.</param>
public sealed record PrazosDto(int? PixMinutos, int? DebitoMinutos, int? BoletoDiasUteis);

/// <summary>200 de GET e PUT /pagamento/recebimentos/configuracao (RF-193).</summary>
/// <param name="Versao">O If-Match do próximo PUT.</param>
/// <param name="Formas">PIX, CARTAO_CREDITO, CARTAO_DEBITO e BOLETO → ligada.</param>
/// <param name="JurosAoDia">% ao dia.</param>
/// <param name="ToleranciaAvisoDias">Dias de atraso antes do aviso.</param>
/// <param name="DiaVencimento">Sempre 10.</param>
/// <param name="Prazos">Os prazos de cada forma.</param>
public sealed record ConfiguracaoDto(int Versao, IReadOnlyDictionary<string, bool> Formas, decimal JurosAoDia, int ToleranciaAvisoDias,
    int DiaVencimento, PrazosDto Prazos)
{
    /// <summary>Da versão guardada, com as formas na ordem da tela.</summary>
    public static ConfiguracaoDto De(ConfiguracaoDoPagamento c) =>
        new(c.Versao, TextosDoPagamento.Formas.ToDictionary(f => f.Texto(), c.Aceita), c.JurosAoDia, c.ToleranciaAvisoDias, c.DiaVencimento,
            new PrazosDto(c.Prazos.PixMinutos, c.Prazos.DebitoMinutos, c.Prazos.BoletoDiasUteis));
}

/// <summary>O corpo de PUT /pagamento/recebimentos/configuracao (RF-193): o do GET, sem a versão, que vai no If-Match.</summary>
/// <param name="Formas">As quatro, todas presentes.</param>
/// <param name="JurosAoDia">De 0 a 1.</param>
/// <param name="ToleranciaAvisoDias">De 0 a 30.</param>
/// <param name="DiaVencimento">10.</param>
/// <param name="Prazos">Os prazos.</param>
public sealed record PedidoDeConfiguracao(IReadOnlyDictionary<string, bool?>? Formas, decimal? JurosAoDia, int? ToleranciaAvisoDias,
    int? DiaVencimento, PrazosDto? Prazos)
{
    /// <summary>
    /// O corpo conferido, campo a campo (RF-193); o campo errado volta citado.
    /// Nenhuma forma ligada: FORMA_OBRIGATORIA.
    /// </summary>
    /// <exception cref="FalhaDoPagamento">DADOS_INVALIDOS ou FORMA_OBRIGATORIA.</exception>
    public (Dictionary<FormaDePagamento, bool> Formas, decimal Juros, int Tolerancia, PrazosDoPagamento Prazos) Validar()
    {
        var formas = new Dictionary<FormaDePagamento, bool>();
        foreach (var forma in TextosDoPagamento.Formas)
        {
            if (Formas?.GetValueOrDefault(forma.Texto()) is not { } ligada) throw FalhaDoPagamento.DadosInvalidos($"formas.{forma.Texto()}");
            formas[forma] = ligada;
        }
        if (JurosAoDia is not ({ } juros and >= 0m and <= 1m)) throw FalhaDoPagamento.DadosInvalidos("jurosAoDia");
        if (ToleranciaAvisoDias is not ({ } tolerancia and >= 0 and <= 30)) throw FalhaDoPagamento.DadosInvalidos("toleranciaAvisoDias");
        if (DiaVencimento != ConfiguracaoDoPagamento.Dia) throw FalhaDoPagamento.DadosInvalidos("diaVencimento");
        if (Prazos?.PixMinutos is not ({ } pix and >= 5 and <= 1440)) throw FalhaDoPagamento.DadosInvalidos("prazos.pixMinutos");
        if (Prazos.DebitoMinutos is not ({ } debito and >= 5 and <= 30)) throw FalhaDoPagamento.DadosInvalidos("prazos.debitoMinutos");
        if (Prazos.BoletoDiasUteis is not ({ } boleto and >= 1 and <= 30)) throw FalhaDoPagamento.DadosInvalidos("prazos.boletoDiasUteis");
        if (!formas.Values.Any(l => l)) throw FalhaDoPagamento.FormaObrigatoria();
        return (formas, juros, tolerancia, new PrazosDoPagamento(pix, debito, boleto));
    }
}

/// <summary>O intermediador do cartão em GET /pagamento/formas (RF-184-api): a tela gera o token direto nele.</summary>
/// <param name="Intermediador">MERCADO_PAGO; fora de produção, com o falso, FALSO.</param>
/// <param name="ChavePublica">A chave pública do intermediador, como "APP_USR-3f1c…".</param>
public sealed record CartaoDasFormasDto(string Intermediador, string ChavePublica);

/// <summary>200 de GET /pagamento/formas (RF-184): as formas ligadas, na ordem da tela, o cartão e o último endereço do boleto.</summary>
/// <param name="Formas">PIX, CARTAO_CREDITO…</param>
/// <param name="Cartao">O intermediador e a chave pública do cartão.</param>
/// <param name="EnderecoDoBoleto">O endereço do último boleto da conta; null se nenhum.</param>
public sealed record FormasDto(IReadOnlyList<string> Formas, CartaoDasFormasDto Cartao, EnderecoDto? EnderecoDoBoleto);
