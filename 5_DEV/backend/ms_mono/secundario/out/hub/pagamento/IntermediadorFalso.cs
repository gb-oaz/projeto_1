using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;

namespace Boaz.MsMono.Secundario.Out.Hub.Pagamento;

// O que o falso responde às cobranças (PUT /_teste/pagamento/banco).
public enum ComportamentoDoBanco
{
    // crédito aprovado na hora; PIX, débito e boleto à espera do aviso
    Normal,

    // cartão recusado pelo emissor
    Recusar,

    // cartão em análise: PROCESSANDO
    EmAnalise,

    // o banco não responde no prazo: nenhuma chamada tem resposta
    SemResposta,
}

// Um pedido que o falso recebeu do deboaz (GET /_teste/pagamento/banco/pedidos):
// na cobrança, também o cartão (método e emissor), quem paga e o endereço do boleto.
public sealed record PedidoAoBancoFalso(string Tipo, string? IdExterno, string? Forma, decimal Valor, string TraceId, string? Pagamento, string? Estorno,
    CartaoTokenizado? Cartao = null, Pagador? Pagador = null, EnderecoDoPagador? Endereco = null);

// O intermediador falso (pagamento-hexagonal v03, RNF-242): "FALSO", só fora
// de produção, nos testes e na máquina; nunca chama serviço externo. Os tokens dos
// exemplos: "tok_9f2c71" é um VISA de final 4242, "tok_d41b07" um MASTERCARD de
// final 5100 e "tok_vencido" um token já usado. O aviso do banco vem com
// X-Assinatura: sha256=<HMAC-SHA256 do corpo, em hexadecimal> e o corpo
// { id, tipo, idExterno, em, metadados: { traceId } } (5_DEV/qa/…/BancoFalso.cs).
public sealed class IntermediadorFalso(string segredo, TimeProvider relogio) : IIntermediador
{
    // o segredo padrão do falso, o mesmo do qa (PAGAMENTO_SEGREDO_DO_AVISO); nunca vale em produção
    public const string SegredoPadrao = "segredo-do-intermediador-falso";

    // o cabeçalho do aviso do falso: X-Assinatura: sha256=… (o roteamento do aviso, RNF-242)
    public const string Assinatura = "x-assinatura";
    public const string PrefixoDaAssinatura = "sha256=";

    // a "chave pública" do falso em GET /pagamento/formas: a tela de teste não gera token nele
    public const string ChavePublicaDoFalso = "FALSO-chave-publica";

    public string Nome => NomeDoIntermediador.Falso;

    public string ChavePublica => ChavePublicaDoFalso;

    static readonly Dictionary<string, CartaoMascarado> Cartoes = new(StringComparer.Ordinal)
    {
        ["tok_9f2c71"] = new("VISA", "4242"),
        ["tok_d41b07"] = new("MASTERCARD", "5100"),
    };

    readonly ConcurrentQueue<PedidoAoBancoFalso> pedidos = new();

    public ComportamentoDoBanco Comportamento { get; set; } = ComportamentoDoBanco.Normal;

    // o que o falso recebeu, na ordem de chegada
    public IReadOnlyList<PedidoAoBancoFalso> Pedidos => [.. pedidos];

    public Task<ResultadoDaCobranca> Cobrar(PedidoDeCobranca pedido, CancellationToken ct = default)
    {
        var idExterno = $"{Prefixo(pedido.Forma)}{Aleatorio()}";
        pedidos.Enqueue(new PedidoAoBancoFalso("COBRANCA", idExterno, pedido.Forma.Texto(), pedido.Valor.Reais, pedido.TraceId, pedido.PagamentoId, null,
            pedido.Cartao, pedido.Pagador, pedido.Endereco));
        if (Comportamento == ComportamentoDoBanco.SemResposta) return Task.FromResult(new ResultadoDaCobranca(SituacaoDaCobranca.Indisponivel));
        return Task.FromResult(pedido.Forma switch
        {
            FormaDePagamento.Pix => new ResultadoDaCobranca(SituacaoDaCobranca.Aguardando, idExterno, PixCopiaECola: CopiaECola(pedido)),
            FormaDePagamento.Boleto => new ResultadoDaCobranca(SituacaoDaCobranca.Aguardando, idExterno,
                LinhaDigitavel: LinhaDigitavel(pedido), CodigoBarras: CodigoBarras(pedido)),
            _ => Cartao(pedido, idExterno),
        });
    }

    public Task<ConsultaNoBanco> Consultar(string idExterno, CancellationToken ct = default) =>
        Task.FromResult(new ConsultaNoBanco(SituacaoNoBanco.Aguardando));

    public Task Cancelar(string idExterno, CancellationToken ct = default) => Task.CompletedTask;

    public Task<ResultadoDoEstorno> Estornar(PedidoDeEstorno pedido, CancellationToken ct = default)
    {
        var idExterno = $"ref_{Aleatorio()}";
        pedidos.Enqueue(new PedidoAoBancoFalso("ESTORNO", idExterno, null, pedido.Valor.Reais, pedido.TraceId, pedido.PagamentoId, pedido.EstornoId));
        return Task.FromResult(Comportamento == ComportamentoDoBanco.SemResposta
            ? new ResultadoDoEstorno(SituacaoDoEstorno.Indisponivel)
            : new ResultadoDoEstorno(SituacaoDoEstorno.Pendente, idExterno));
    }

    // o aviso do falso traz o id externo do pagamento ou do estorno: quem diz qual é a leitura
    public ConferenciaDoAviso Conferir(AvisoRecebido recebido) => new(AssinaturaConfere(recebido), null);

    bool AssinaturaConfere(AvisoRecebido recebido)
    {
        var esperada = PrefixoDaAssinatura + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(segredo),
            Encoding.UTF8.GetBytes(recebido.Corpo)));
        var veio = recebido.Cabecalhos.GetValueOrDefault(Assinatura) ?? "";
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(esperada), Encoding.UTF8.GetBytes(veio));
    }

    public Task<LeituraDoAviso> LerAviso(AvisoRecebido recebido, CancellationToken ct = default)
    {
        if (!AssinaturaConfere(recebido)) return Task.FromResult(new LeituraDoAviso(false, null));
        return Task.FromResult(new LeituraDoAviso(true, Ler(recebido.Corpo)));
    }

    // o corpo do falso; o que não tem id, tipo conhecido e idExterno é ignorado (200 sem efeito)
    static AvisoDoBanco? Ler(string corpo)
    {
        try
        {
            using var json = JsonDocument.Parse(corpo);
            var raiz = json.RootElement;
            string? Texto(JsonElement e, string campo) =>
                e.ValueKind == JsonValueKind.Object && e.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            TipoDoAviso? tipo = Texto(raiz, "tipo") switch
            {
                "PAGAMENTO_CONFIRMADO" => TipoDoAviso.PagamentoConfirmado,
                "PAGAMENTO_RECUSADO" => TipoDoAviso.PagamentoRecusado,
                "ESTORNO_CONCLUIDO" => TipoDoAviso.EstornoConcluido,
                "ESTORNO_RECUSADO" => TipoDoAviso.EstornoRecusado,
                _ => null,
            };
            if (Texto(raiz, "id") is not { } id || tipo is not { } t || Texto(raiz, "idExterno") is not { } externo) return null;
            var em = DateTimeOffset.TryParse(Texto(raiz, "em"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var quando)
                ? quando : DateTimeOffset.UtcNow;
            var origem = raiz.TryGetProperty("metadados", out var metadados) ? Texto(metadados, "traceId") : null;
            return new AvisoDoBanco(id, t, externo, em, origem);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    ResultadoDaCobranca Cartao(PedidoDeCobranca pedido, string idExterno)
    {
        if (pedido.Cartao?.Token is not { } token || !Cartoes.TryGetValue(token, out var cartao))
        {
            return new ResultadoDaCobranca(SituacaoDaCobranca.TokenInvalido);
        }
        var sufixo = token["tok_".Length..];
        return Comportamento switch
        {
            ComportamentoDoBanco.Recusar => new ResultadoDaCobranca(SituacaoDaCobranca.Recusada, idExterno, Cartao: cartao),
            ComportamentoDoBanco.EmAnalise => new ResultadoDaCobranca(SituacaoDaCobranca.EmAnalise, idExterno, Cartao: cartao),
            _ when pedido.Forma == FormaDePagamento.CartaoDebito => new ResultadoDaCobranca(SituacaoDaCobranca.Aguardando, idExterno,
                ConfirmacaoUrl: $"https://banco.exemplo/confirmar/{sufixo}", Cartao: cartao),
            _ => new ResultadoDaCobranca(SituacaoDaCobranca.Aprovada, idExterno, Cartao: cartao, PagoEm: relogio.GetUtcNow()),
        };
    }

    static string Prefixo(FormaDePagamento forma) => forma switch
    {
        FormaDePagamento.Pix => "pix_",
        FormaDePagamento.CartaoCredito => "card_",
        FormaDePagamento.CartaoDebito => "deb_",
        _ => "bol_",
    };

    static string Aleatorio() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(3));

    // o "copia e cola" no formato EMV do PIX (começa com 000201), só de exemplo; o QR Code é do deboaz
    static string CopiaECola(PedidoDeCobranca pedido) =>
        $"00020101021226{pedido.PagamentoId}5204000053039865406{pedido.Valor.Reais.ToString("0.00", CultureInfo.InvariantCulture)}6304A1B2";

    static string Valor10(PedidoDeCobranca pedido) => pedido.Valor.Centavos.ToString("D10", CultureInfo.InvariantCulture);

    static string LinhaDigitavel(PedidoDeCobranca pedido) => $"23793.38128 60000.000003 00000.000400 1 1296{Valor10(pedido)}";

    static string CodigoBarras(PedidoDeCobranca pedido) => $"237911296{Valor10(pedido)}3381260000000000000000400";
}
