using System.Globalization;
using System.Text;
using Boaz.Core.Hub.Pagamento.Pagamentos.Dtos;

namespace Boaz.MsMono.Primario.Out.Hub.Pagamento;

// O PDF do boleto (RF-188) e do comprovante (RF-189): uma página A4 com o
// texto do documento, em PDF 1.4 escrito à mão (sem biblioteca externa). O
// boleto traz a linha digitável e o código de barras em texto; a imagem do
// código de barras fica para quando o intermediador de produção a entregar.
public static class PdfDoPagamento
{
    public static byte[] Boleto(DocumentoDoPagamentoDto d) => Gerar("Boleto deboaz",
    [
        $"Fatura: {d.FaturaId}",
        $"Pagamento: {d.PagamentoId}",
        $"Conta: {d.Conta}",
        $"Competência: {d.Competencia}",
        $"Valor: R$ {Reais(d.Valor)}",
        $"Vencimento: {d.Vencimento?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}",
        $"Linha digitável: {d.LinhaDigitavel}",
        $"Código de barras: {d.CodigoBarras}",
    ]);

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

    static string Reais(decimal valor) => valor.ToString("#,##0.00", CultureInfo.InvariantCulture).Replace(',', ' ').Replace('.', ',').Replace(' ', '.');

    static byte[] Gerar(string titulo, IReadOnlyList<string> linhas)
    {
        var texto = new StringBuilder();
        texto.Append("BT /F1 16 Tf 56 780 Td (").Append(Escapar(titulo)).Append(") Tj ET\n");
        var y = 740;
        foreach (var linha in linhas)
        {
            texto.Append(CultureInfo.InvariantCulture, $"BT /F1 11 Tf 56 {y} Td (").Append(Escapar(linha)).Append(") Tj ET\n");
            y -= 22;
        }
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
