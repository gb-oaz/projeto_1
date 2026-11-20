using System.Globalization;
using System.Text;
using Boaz.Core.Hub.Pagamento.Pagamentos.Dtos;

namespace Boaz.MsMono.Primario.Out.Hub.Pagamento;

// O PDF do boleto (RF-188) e do comprovante (RF-189): uma página A4 com o
// texto do documento, em PDF 1.4 escrito à mão (sem biblioteca externa). O
// boleto sai em nome e documento da conta, com o endereço de quem paga, a
// linha digitável e o código de barras desenhado em vetor no padrão ITF
// (intercalado 2 de 5) da Febraban, a partir dos 44 dígitos.
public static class PdfDoPagamento
{
    public static byte[] Boleto(DocumentoDoPagamentoDto d) => Gerar("Boleto deboaz",
    [
        $"Fatura: {d.FaturaId}",
        $"Pagamento: {d.PagamentoId}",
        $"Pagador: {d.Conta}",
        $"CPF/CNPJ: {d.Documento}",
        $"Endereço: {Endereco(d.Endereco)}",
        $"Competência: {d.Competencia}",
        $"Valor: R$ {Reais(d.Valor)}",
        $"Vencimento: {d.Vencimento?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}",
        $"Linha digitável: {d.LinhaDigitavel}",
        $"Código de barras: {d.CodigoBarras}",
    ], Itf.Barras(d.CodigoBarras));

    public static byte[] Comprovante(DocumentoDoPagamentoDto d) => Gerar("Comprovante de pagamento deboaz",
    [
        $"Fatura: {d.FaturaId}",
        $"Pagamento: {d.PagamentoId}",
        $"Conta: {d.Conta}",
        $"Competência: {d.Competencia}",
        $"Forma: {d.Forma}{(d.Cartao is { } c ? $" ({c})" : "")}",
        $"Valor pago: R$ {Reais(d.Valor)}",
        $"Pago em: {d.PagoEm?.ToOffset(TimeSpan.FromHours(-3)).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)} (Brasília)",
        $"Autenticação do banco: {d.Autenticacao}",
    ]);

    static string Endereco(EnderecoDto? e) =>
        e is null ? "" : $"{e.Logradouro}, {e.Numero} - {e.Bairro} - {e.Cidade}/{e.Uf} - CEP {e.Cep}";

    static string Reais(decimal valor) => valor.ToString("#,##0.00", CultureInfo.InvariantCulture).Replace(',', ' ').Replace('.', ',').Replace(' ', '.');

    static byte[] Gerar(string titulo, IReadOnlyList<string> linhas, string? desenho = null)
    {
        var texto = new StringBuilder();
        texto.Append("BT /F1 16 Tf 56 780 Td (").Append(Escapar(titulo)).Append(") Tj ET\n");
        var y = 740;
        foreach (var linha in linhas)
        {
            texto.Append(CultureInfo.InvariantCulture, $"BT /F1 11 Tf 56 {y} Td (").Append(Escapar(linha)).Append(") Tj ET\n");
            y -= 22;
        }
        if (desenho is { Length: > 0 }) texto.Append(CultureInfo.InvariantCulture, $"q 0 g 1 0 0 1 56 {y - 60} cm\n").Append(desenho).Append("Q\n");
        var conteudo = Latin1(texto.ToString());
        string[] objetos =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {conteudo.Length} >>\nstream\n{Encoding.Latin1.GetString(conteudo)}endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
        ];
        var pdf = new StringBuilder("%PDF-1.4\n");
        var posicoes = new List<int>();
        for (var i = 0; i < objetos.Length; i++)
        {
            posicoes.Add(Encoding.Latin1.GetByteCount(pdf.ToString()));
            pdf.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objetos[i]}\nendobj\n");
        }
        var xref = Encoding.Latin1.GetByteCount(pdf.ToString());
        pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objetos.Length + 1}\n0000000000 65535 f \n");
        foreach (var p in posicoes) pdf.Append(CultureInfo.InvariantCulture, $"{p:D10} 00000 n \n");
        pdf.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objetos.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(pdf.ToString());
    }

    // o texto em Latin-1 (os acentos do português cabem no WinAnsi da Helvetica)
    static byte[] Latin1(string texto) => Encoding.Latin1.GetBytes(texto);

    static string Escapar(string texto) => texto.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}

// O código de barras do boleto no padrão ITF (intercalado 2 de 5): início
// 0000 (estreita), os dígitos aos pares (o 1º do par nas barras, o 2º nos
// espaços), fim 100 (larga, estreita, estreita). Larga = 3 estreitas; a
// estreita mede 0,96 pt e a altura, 50 pt (a do boleto é 13 mm).
static class Itf
{
    const double Estreita = 0.96;
    const double Altura = 50;

    // 1 = larga, 0 = estreita, de 0 a 9
    static readonly string[] Padroes = ["00110", "10001", "01001", "11000", "00101", "10100", "01100", "00011", "10010", "01010"];

    // os retângulos das barras em operadores de PDF; vazio se o código não tiver 44 dígitos
    public static string Barras(string? codigo)
    {
        if (codigo is not { Length: 44 } || !codigo.All(char.IsAsciiDigit)) return "";
        var larguras = new List<int> { 1, 1, 1, 1 };
        for (var i = 0; i < codigo.Length; i += 2)
        {
            var barras = Padroes[codigo[i] - '0'];
            var espacos = Padroes[codigo[i + 1] - '0'];
            for (var j = 0; j < 5; j++)
            {
                larguras.Add(barras[j] == '1' ? 3 : 1);
                larguras.Add(espacos[j] == '1' ? 3 : 1);
            }
        }
        larguras.AddRange([3, 1, 1]);
        var desenho = new StringBuilder();
        var x = 0.0;
        for (var i = 0; i < larguras.Count; i++)
        {
            var largura = larguras[i] * Estreita;
            if (i % 2 == 0) desenho.Append(CultureInfo.InvariantCulture, $"{x:0.##} 0 {largura:0.##} {Altura} re\n");
            x += largura;
        }
        return desenho.Append("f\n").ToString();
    }
}
