using System.Globalization;
using System.Text;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;

namespace Boaz.MsMono.Secundario.Out.Hub.Pagamento;

// O QR Code do PIX desenhado pelo deboaz (RF-185-api, pagamento-hexagonal v03):
// o "copia e cola" em modo byte (UTF-8), correção de erro nível M, a menor
// versão que cabe (1 a 40) e a máscara de menor penalidade (ISO/IEC 18004).
// O SVG é vetorial: um quadrado por módulo escuro num só <path>, com a margem
// de 4 módulos que os leitores pedem; nunca a imagem do banco embrulhada.
public sealed class DesenhoDoQrCode : IDesenhoDoQrCode
{
    // a margem branca em volta, em módulos (a "quiet zone")
    public const int Margem = 4;

    public string Svg(string texto)
    {
        var modulos = QrCode.Codificar(Encoding.UTF8.GetBytes(texto));
        var lado = modulos.GetLength(0);
        var total = lado + 2 * Margem;
        var caminho = new StringBuilder();
        for (var y = 0; y < lado; y++)
        {
            for (var x = 0; x < lado; x++)
            {
                if (modulos[y, x]) caminho.Append(CultureInfo.InvariantCulture, $"M{x + Margem} {y + Margem}h1v1h-1z");
            }
        }
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {total} {total}\" shape-rendering=\"crispEdges\">" +
            $"<rect width=\"{total}\" height=\"{total}\" fill=\"#fff\"/><path fill=\"#000\" d=\"{caminho}\"/></svg>";
    }
}

// O codificador do QR Code, só com o .NET: modo byte, nível M. Devolve a
// matriz [linha, coluna], true = módulo escuro.
static class QrCode
{
    // nível M, por versão (índice 0 sem uso): códigos de correção por bloco e quantos blocos
    static readonly int[] CorrecaoPorBloco =
    [
        -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
        28, 28, 28, 28, 28, 28,
    ];

    static readonly int[] Blocos =
    [
        -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45,
        47, 49,
    ];

    // o nível M nos bits de formato
    const int NivelM = 0;

    public static bool[,] Codificar(byte[] dados)
    {
        var versao = Versao(dados.Length);
        var codigos = ComCorrecao(Dados(dados, versao), versao);
        var matriz = new Matriz(versao);
        matriz.DesenharFuncoes();
        matriz.DesenharCodigos(codigos);

        var melhor = -1;
        var menor = int.MaxValue;
        for (var mascara = 0; mascara < 8; mascara++)
        {
            matriz.AplicarMascara(mascara);
            matriz.DesenharFormato(mascara);
            var penalidade = matriz.Penalidade();
            if (penalidade < menor) (melhor, menor) = (mascara, penalidade);
            matriz.AplicarMascara(mascara); // desfaz (XOR)
        }
        matriz.AplicarMascara(melhor);
        matriz.DesenharFormato(melhor);
        return matriz.Modulos;
    }

    // a menor versão em que o modo byte cabe
    static int Versao(int bytes)
    {
        for (var v = 1; v <= 40; v++)
        {
            var bits = 4 + BitsDoTamanho(v) + bytes * 8;
            if (bits <= CodigosDeDados(v) * 8) return v;
        }
        throw new ArgumentException("Texto grande demais para um QR Code.", nameof(bytes));
    }

    static int BitsDoTamanho(int versao) => versao <= 9 ? 8 : 16;

    static int ModulosDeDados(int versao)
    {
        var total = (16 * versao + 128) * versao + 64;
        if (versao >= 2)
        {
            var alinhamentos = versao / 7 + 2;
            total -= (25 * alinhamentos - 10) * alinhamentos - 55;
            if (versao >= 7) total -= 36;
        }
        return total;
    }

    static int CodigosDeDados(int versao) => ModulosDeDados(versao) / 8 - CorrecaoPorBloco[versao] * Blocos[versao];

    // modo byte (0100), o tamanho, os bytes, o terminador e o preenchimento 0xEC 0x11
    static byte[] Dados(byte[] dados, int versao)
    {
        var bits = new List<bool>();
        void Por(int valor, int quantos)
        {
            for (var i = quantos - 1; i >= 0; i--) bits.Add(((valor >> i) & 1) != 0);
        }
        Por(0b0100, 4);
        Por(dados.Length, BitsDoTamanho(versao));
        foreach (var b in dados) Por(b, 8);
        var capacidade = CodigosDeDados(versao) * 8;
        Por(0, Math.Min(4, capacidade - bits.Count));
        Por(0, (8 - bits.Count % 8) % 8);
        for (var preenche = 0xEC; bits.Count < capacidade; preenche ^= 0xEC ^ 0x11) Por(preenche, 8);
        var resultado = new byte[bits.Count / 8];
        for (var i = 0; i < bits.Count; i++)
        {
            if (bits[i]) resultado[i >> 3] |= (byte)(1 << (7 - (i & 7)));
        }
        return resultado;
    }

    // os blocos com a correção Reed-Solomon, intercalados
    static byte[] ComCorrecao(byte[] dados, int versao)
    {
        var blocos = Blocos[versao];
        var correcao = CorrecaoPorBloco[versao];
        var brutos = ModulosDeDados(versao) / 8;
        var curtos = blocos - brutos % blocos;
        var tamanhoCurto = brutos / blocos;
        var divisor = Divisor(correcao);
        var todos = new List<byte[]>();
        for (int i = 0, k = 0; i < blocos; i++)
        {
            var tamanho = tamanhoCurto - correcao + (i < curtos ? 0 : 1);
            var parte = dados[k..(k + tamanho)];
            k += tamanho;
            var resto = Resto(parte, divisor);
            var bloco = new byte[tamanhoCurto + 1];
            parte.CopyTo(bloco, 0);
            resto.CopyTo(bloco, tamanhoCurto + 1 - correcao);
            todos.Add(bloco);
        }
        var resultado = new List<byte>(brutos);
        for (var i = 0; i < todos[0].Length; i++)
        {
            for (var j = 0; j < todos.Count; j++)
            {
                // o byte que falta nos blocos curtos não entra
                if (i != tamanhoCurto - correcao || j >= curtos) resultado.Add(todos[j][i]);
            }
        }
        return [.. resultado];
    }

    static byte[] Divisor(int grau)
    {
        var resultado = new byte[grau];
        resultado[grau - 1] = 1;
        var raiz = 1;
        for (var i = 0; i < grau; i++)
        {
            for (var j = 0; j < grau; j++)
            {
                resultado[j] = Multiplicar(resultado[j], raiz);
                if (j + 1 < grau) resultado[j] ^= resultado[j + 1];
            }
            raiz = Multiplicar(raiz, 0x02);
        }
        return resultado;
    }

    static byte[] Resto(byte[] dados, byte[] divisor)
    {
        var resultado = new byte[divisor.Length];
        foreach (var b in dados)
        {
            var fator = b ^ resultado[0];
            Array.Copy(resultado, 1, resultado, 0, resultado.Length - 1);
            resultado[^1] = 0;
            for (var i = 0; i < resultado.Length; i++) resultado[i] ^= Multiplicar(divisor[i], fator);
        }
        return resultado;
    }

    // a multiplicação no GF(2^8) do QR Code (polinômio 0x11D)
    static byte Multiplicar(int x, int y)
    {
        var z = 0;
        for (var i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            z ^= ((y >> i) & 1) * x;
        }
        return (byte)z;
    }

    sealed class Matriz(int versao)
    {
        readonly int lado = versao * 4 + 17;
        readonly bool[,] funcao = new bool[versao * 4 + 17, versao * 4 + 17];

        public bool[,] Modulos { get; } = new bool[versao * 4 + 17, versao * 4 + 17];

        void Fixar(int x, int y, bool escuro)
        {
            Modulos[y, x] = escuro;
            funcao[y, x] = true;
        }

        public void DesenharFuncoes()
        {
            for (var i = 0; i < lado; i++)
            {
                Fixar(6, i, i % 2 == 0);
                Fixar(i, 6, i % 2 == 0);
            }
            Localizador(3, 3);
            Localizador(lado - 4, 3);
            Localizador(3, lado - 4);
            var posicoes = Alinhamentos();
            var n = posicoes.Length;
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    if (!(i == 0 && j == 0 || i == 0 && j == n - 1 || i == n - 1 && j == 0)) Alinhamento(posicoes[i], posicoes[j]);
                }
            }
            DesenharFormato(0);
            DesenharVersao();
        }

        void Localizador(int x, int y)
        {
            for (var dy = -4; dy <= 4; dy++)
            {
                for (var dx = -4; dx <= 4; dx++)
                {
                    var distancia = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    int xx = x + dx, yy = y + dy;
                    if (xx >= 0 && xx < lado && yy >= 0 && yy < lado) Fixar(xx, yy, distancia != 2 && distancia != 4);
                }
            }
        }

        void Alinhamento(int x, int y)
        {
            for (var dy = -2; dy <= 2; dy++)
            {
                for (var dx = -2; dx <= 2; dx++) Fixar(x + dx, y + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
            }
        }

        int[] Alinhamentos()
        {
            if (versao == 1) return [];
            var quantos = versao / 7 + 2;
            var passo = (versao * 8 + quantos * 3 + 5) / (quantos * 4 - 4) * 2;
            var resultado = new int[quantos];
            resultado[0] = 6;
            for (int i = quantos - 1, pos = lado - 7; i >= 1; i--, pos -= passo) resultado[i] = pos;
            return resultado;
        }

        public void DesenharFormato(int mascara)
        {
            var dados = NivelM << 3 | mascara;
            var resto = dados;
            for (var i = 0; i < 10; i++) resto = (resto << 1) ^ ((resto >> 9) * 0x537);
            var bits = (dados << 10 | resto) ^ 0x5412;
            bool Bit(int i) => ((bits >> i) & 1) != 0;

            for (var i = 0; i <= 5; i++) Fixar(8, i, Bit(i));
            Fixar(8, 7, Bit(6));
            Fixar(8, 8, Bit(7));
            Fixar(7, 8, Bit(8));
            for (var i = 9; i < 15; i++) Fixar(14 - i, 8, Bit(i));

            for (var i = 0; i < 8; i++) Fixar(lado - 1 - i, 8, Bit(i));
            for (var i = 8; i < 15; i++) Fixar(8, lado - 15 + i, Bit(i));
            Fixar(8, lado - 8, true);
        }

        void DesenharVersao()
        {
            if (versao < 7) return;
            var resto = versao;
            for (var i = 0; i < 12; i++) resto = (resto << 1) ^ ((resto >> 11) * 0x1F25);
            var bits = versao << 12 | resto;
            for (var i = 0; i < 18; i++)
            {
                var escuro = ((bits >> i) & 1) != 0;
                int a = lado - 11 + i % 3, b = i / 3;
                Fixar(a, b, escuro);
                Fixar(b, a, escuro);
            }
        }

        public void DesenharCodigos(byte[] codigos)
        {
            var i = 0;
            for (var direita = lado - 1; direita >= 1; direita -= 2)
            {
                if (direita == 6) direita = 5;
                for (var vertical = 0; vertical < lado; vertical++)
                {
                    for (var j = 0; j < 2; j++)
                    {
                        var x = direita - j;
                        var subindo = ((direita + 1) & 2) == 0;
                        var y = subindo ? lado - 1 - vertical : vertical;
                        if (funcao[y, x] || i >= codigos.Length * 8) continue;
                        Modulos[y, x] = ((codigos[i >> 3] >> (7 - (i & 7))) & 1) != 0;
                        i++;
                    }
                }
            }
        }

        public void AplicarMascara(int mascara)
        {
            for (var y = 0; y < lado; y++)
            {
                for (var x = 0; x < lado; x++)
                {
                    var inverte = mascara switch
                    {
                        0 => (x + y) % 2 == 0,
                        1 => y % 2 == 0,
                        2 => x % 3 == 0,
                        3 => (x + y) % 3 == 0,
                        4 => (x / 3 + y / 2) % 2 == 0,
                        5 => x * y % 2 + x * y % 3 == 0,
                        6 => (x * y % 2 + x * y % 3) % 2 == 0,
                        _ => ((x + y) % 2 + x * y % 3) % 2 == 0,
                    };
                    if (inverte && !funcao[y, x]) Modulos[y, x] = !Modulos[y, x];
                }
            }
        }

        // as quatro regras de penalidade da norma: sequências, blocos 2x2, padrão de localizador e equilíbrio
        public int Penalidade()
        {
            var total = 0;
            for (var y = 0; y < lado; y++) total += Linha(i => Modulos[y, i]);
            for (var x = 0; x < lado; x++) total += Linha(i => Modulos[i, x]);
            for (var y = 0; y < lado - 1; y++)
            {
                for (var x = 0; x < lado - 1; x++)
                {
                    var cor = Modulos[y, x];
                    if (cor == Modulos[y, x + 1] && cor == Modulos[y + 1, x] && cor == Modulos[y + 1, x + 1]) total += 3;
                }
            }
            var escuros = 0;
            foreach (var m in Modulos) if (m) escuros++;
            var area = lado * lado;
            var k = (int)((Math.Abs(escuros * 20L - area * 10L) + area - 1) / area) - 1;
            return total + k * 10;
        }

        static readonly bool[] Padrao = [true, false, true, true, true, false, true];

        int Linha(Func<int, bool> modulo)
        {
            var total = 0;
            var corrida = 1;
            for (var i = 1; i <= lado; i++)
            {
                if (i < lado && modulo(i) == modulo(i - 1))
                {
                    corrida++;
                    continue;
                }
                if (corrida >= 5) total += 3 + corrida - 5;
                corrida = 1;
            }
            // 1:1:3:1:1 com 4 claros de um dos lados, como um localizador
            for (var i = 0; i + 7 <= lado; i++)
            {
                var igual = true;
                for (var j = 0; j < 7 && igual; j++) igual = modulo(i + j) == Padrao[j];
                if (!igual) continue;
                if (Claros(modulo, i - 4, i) || Claros(modulo, i + 7, i + 11)) total += 40;
            }
            return total;
        }

        bool Claros(Func<int, bool> modulo, int de, int ate)
        {
            for (var i = de; i < ate; i++)
            {
                if (i >= 0 && i < lado && modulo(i)) return false;
            }
            return true;
        }
    }
}
