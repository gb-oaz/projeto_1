// "using" traz os tipos de outro namespace. Shapes tem Path, Geometry e as
// transformações (TranslateTransform, ScaleTransform). O alias da segunda linha
// existe porque System.IO também tem uma classe Path: sem ele o compilador não
// sabe qual das duas usar.
using Microsoft.Maui.Controls.Shapes;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace Boaz.Mobile.Core.Commons.Components;

// Abertura animada do app (90_identidade-visual RN-51, RN-52, RN-162).
//
// Como funciona um componente no .NET MAUI: ele tem duas metades, que o
// compilador junta numa classe só ("partial class").
//   - Abertura.xaml    descreve O QUE aparece (a árvore de elementos e os estilos);
//   - Abertura.xaml.cs (este arquivo) descreve o COMPORTAMENTO: posiciona as
//     peças da marca e anima.
// Todo elemento do XAML com x:Name="Algo" vira aqui um campo com esse nome.
//
// Passo a passo do que este componente faz:
//   1. Montar(): pega as peças da marca em Marca.xaml (anel, antenas, "de",
//      "boaz") e as encaixa no tamanho certo, nos dois quadros: o logo grande
//      e o símbolo pequeno do carregamento.
//   2. Iniciar(): roda quando o componente entra na tela (evento Loaded). Se o
//      aparelho pede movimento reduzido, mostra tudo parado e para por aí.
//   3. Abrir(): a abertura, que toca uma vez (cerca de 1,8 s, dentro dos 2,5 s
//      da RN-51): o anel cresce, as antenas sobem de dentro do vão e o nome entra.
//   4. Carregar(): mostra "Carregando…" embaixo e repete o carregamento em laço
//      (as antenas descem para o vão e voltam) até a tela sair.
//   5. Parar(): roda quando o componente sai da tela (evento Unloaded) e
//      interrompe as animações, para não ficar trabalhando escondido.
//
// Animar no MAUI: todo View tem métodos como FadeToAsync (opacidade),
// TranslateToAsync (posição), ScaleToAsync (tamanho) e RotateToAsync (giro).
// Cada um devolve uma Task que termina quando o movimento acaba. Então:
//   - "await A; await B;" faz A e depois B (em sequência);
//   - "await Task.WhenAll(A, B);" faz A e B ao mesmo tempo.
// As durações vêm dos tokens de movimento (MotionDuration… em Tokens.xaml),
// nunca de números soltos no código.
public partial class Abertura : ContentView
{
    // Medidas do quadro em que Marca.xaml desenha as peças (em unidades do
    // desenho, não em dp): o símbolo ocupa 540 × 793 e o nome vai até x = 2428.
    // As antenas curtas (quest) começam em y = 180; o centro do anel fica em (270, 523).
    private const double LarguraSimbolo = 540, LarguraLogo = 2428, AlturaQuadro = 793, TopoCurto = 180;
    private const double CentroAnelX = 270, CentroAnelY = 523;

    // Uma BindableProperty é uma propriedade que o XAML consegue preencher
    // (ex.: <componentes:Abertura Curto="True" />). Quando o valor muda,
    // propertyChanged chama Montar() de novo, para trocar as antenas.
    public static readonly BindableProperty CurtoProperty = BindableProperty.Create(
        nameof(Curto), typeof(bool), typeof(Abertura), false, propertyChanged: (view, _, _) => ((Abertura)view).Montar());

    /// <summary>true = antenas curtas (quest); false = antenas longas (hub).</summary>
    public bool Curto
    {
        get => (bool)GetValue(CurtoProperty);
        set => SetValue(CurtoProperty, value);
    }

    // Serve para cancelar o laço do carregamento quando a tela sai.
    private CancellationTokenSource? animacao;

    public Abertura()
    {
        // InitializeComponent() lê o Abertura.xaml e cria os elementos (e os campos x:Name).
        InitializeComponent();
        Montar();
        // Loaded: o componente entrou na tela. Unloaded: o componente saiu da tela.
        Loaded += (_, _) => Iniciar();
        Unloaded += (_, _) => Parar();
    }

    // Passo 1: encaixa as peças. O logo fica com 96 dp de altura (Spacing24) e
    // o símbolo do carregamento com 40 dp (Spacing10).
    private void Montar()
    {
        // No quest (Curto), usa as peças "…Curta" de Marca.xaml e corta o topo vazio do quadro.
        var (topo, curta) = Curto ? (TopoCurto, "Curta") : (0d, "");
        // Marca.xaml diz quanto descer o nome para ele ficar no meio da altura do símbolo.
        var nome = Recurso<double>("MarcaNomeDeslocamento" + curta);
        Posicionar(Logo, Recurso<double>("Spacing24"), topo, LarguraLogo,
            (Anel, "MarcaAnel", 0), (AntenaB, "MarcaAntenaB" + curta, 0), (AntenaD, "MarcaAntenaD" + curta, 0),
            (NomeDe, "MarcaNomeDe", nome), (NomeBoaz, "MarcaNomeBoaz", nome));
        Posicionar(Simbolo, Recurso<double>("Spacing10"), topo, LarguraSimbolo,
            (SimboloAnel, "MarcaAnel", 0), (SimboloAntenaB, "MarcaAntenaB" + curta, 0), (SimboloAntenaD, "MarcaAntenaD" + curta, 0));

        // AnchorX e AnchorY são o ponto em volta do qual o elemento cresce ou gira,
        // de 0 a 1 (0,5 = meio). O padrão é o meio do Path, que aqui é o logo
        // inteiro; apontamos para o centro do anel, para ele crescer no lugar.
        Anel.AnchorX = CentroAnelX / LarguraLogo;
        Anel.AnchorY = (CentroAnelY - topo) / (AlturaQuadro - topo);
    }

    // Encaixa as peças num quadro (um Grid) com a altura pedida, em dp.
    // As peças de Marca.xaml estão desenhadas em unidades grandes (793 de
    // altura). Em vez de escalar o Grid inteiro, que continuaria ocupando
    // 793 dp na tela, aplicamos a escala em cada desenho com RenderTransform:
    // o Grid fica do tamanho final e as peças cabem certinho dentro dele.
    private static void Posicionar(Grid quadro, double altura, double topo, double largura,
        params (Path Peca, string Chave, double Dy)[] pecas)
    {
        var escala = altura / AlturaQuadro;
        quadro.WidthRequest = largura * escala;
        quadro.HeightRequest = (AlturaQuadro - topo) * escala;
        foreach (var (peca, chave, dy) in pecas)
        {
            // Data é o desenho do Path; vem pronto de Marca.xaml (PathGeometry).
            peca.Data = Recurso<Geometry>(chave);
            // TransformGroup aplica as transformações em ordem: primeiro desloca
            // (o nome desce dy; nas antenas curtas, tudo sobe o topo vazio) e
            // depois reduz para a escala da tela.
            peca.RenderTransform = new TransformGroup
            {
                Children =
                {
                    new TranslateTransform { Y = dy - topo },
                    new ScaleTransform { ScaleX = escala, ScaleY = escala },
                },
            };
        }
    }

    // Passo 2. "async void" só é aceitável em tratadores de evento como este
    // (Loaded). Por isso as exceções são tratadas aqui dentro: nada pode escapar.
    private async void Iniciar()
    {
        Parar();
        animacao = new CancellationTokenSource();
        var token = animacao.Token;

        // Acessibilidade (RN-52): com movimento reduzido, nada se move.
        // No XAML as peças começam invisíveis (Opacity="0"); aqui elas aparecem de uma vez.
        if (MovimentoReduzido())
        {
            foreach (var peca in new View[] { Anel, AntenaB, AntenaD, NomeDe, NomeBoaz, Carregando })
            {
                peca.Opacity = 1;
            }
            return;
        }

        try
        {
            await Abrir();
            await Carregar(token);
        }
        catch (OperationCanceledException)
        {
            // a tela saiu antes de a animação terminar: não há nada a fazer
        }
    }

    // Passo 5: cancela o laço e interrompe qualquer animação em andamento.
    private void Parar()
    {
        animacao?.Cancel();
        animacao?.Dispose();
        animacao = null;
        foreach (var peca in new View[] { Anel, AntenaB, AntenaD, NomeDe, NomeBoaz, Carregando, SimboloAntenaB, SimboloAntenaD })
        {
            peca.CancelAnimations();
        }
    }

    // Passo 3: a abertura. Primeiro põe cada peça na posição de partida,
    // depois anima até a posição final (TranslationX/Y = 0, Opacity = 1, Scale = 1).
    private async Task Abrir()
    {
        // posição de partida: antenas um quarto do logo para baixo (dentro do
        // vão), nome 24 dp à esquerda (Spacing6) e anel com 60% do tamanho
        var queda = Logo.HeightRequest / 4;
        AntenaB.TranslationY = AntenaD.TranslationY = queda;
        NomeDe.TranslationX = NomeBoaz.TranslationX = -Recurso<double>("Spacing6");
        Anel.Scale = 0.6;

        // 3a. o anel aparece e cresce ao mesmo tempo (700 ms). Easing é a curva
        //     da velocidade: CubicOut começa rápido e chega devagar.
        await Task.WhenAll(
            Anel.FadeToAsync(1, Duracao("MotionDurationMedium4")),
            Anel.ScaleToAsync(1, Duracao("MotionDurationExtraLong1"), Easing.CubicOut));
        // 3b. as antenas sobem do vão; a do d sai 100 ms depois da do b (600 ms)
        var antena = Duracao("MotionDurationLong2");
        await Task.WhenAll(
            Mover(AntenaB, 0, 1, antena, Easing.CubicOut),
            Mover(AntenaD, 0, 1, antena, Easing.CubicOut, Duracao("MotionDurationShort2")));
        // 3c. o nome desliza da esquerda para o lugar enquanto aparece (500 ms)
        var nome = Duracao("MotionDurationLong2");
        await Task.WhenAll(
            Mover(NomeDe, 0, 1, nome, Easing.CubicOut),
            Mover(NomeBoaz, 0, 1, nome, Easing.CubicOut));
    }

    // Passo 4: mostra o bloco de carregamento e repete o laço até o token ser
    // cancelado (ou seja, até Parar() rodar).
    private async Task Carregar(CancellationToken token)
    {
        await Carregando.FadeToAsync(1, Duracao("MotionDurationMedium2"));
        var queda = Simbolo.HeightRequest / 3;
        var passo = Duracao("MotionDurationMedium4");
        var atraso = Duracao("MotionDurationShort4");
        while (!token.IsCancellationRequested)
        {
            // as duas antenas recolhem e voltam; a do d começa 200 ms depois
            await Task.WhenAll(Recolher(SimboloAntenaB, queda, passo, 0), Recolher(SimboloAntenaD, queda, passo, atraso));
            // pausa entre uma volta e outra; se a tela sair durante a pausa, o
            // Task.Delay lança OperationCanceledException, tratada em Iniciar()
            await Task.Delay((int)Duracao("MotionDurationLong2"), token);
        }
    }

    // Uma antena desce sumindo (para dentro do vão) e depois sobe aparecendo.
    private static async Task Recolher(View antena, double queda, uint passo, uint atraso)
    {
        await Mover(antena, queda, 0, passo, Easing.CubicIn, atraso);
        await Mover(antena, 0, 1, passo, Easing.CubicOut);
    }

    // Ajudante: espera "atraso" ms e então leva a peça, ao mesmo tempo, à
    // altura y (sem deslocamento lateral) e à opacidade pedida.
    private static async Task Mover(View peca, double y, double opacidade, uint duracao, Easing curva, uint atraso = 0)
    {
        if (atraso > 0)
        {
            await Task.Delay((int)atraso);
        }
        await Task.WhenAll(
            peca.TranslateToAsync(0, y, duracao, curva),
            peca.FadeToAsync(opacidade, duracao));
    }

    private static uint Duracao(string chave) => Recurso<uint>(chave);

    // Lê um recurso pela chave (x:Key) nos dicionários que o App.xaml juntou
    // (Tokens.xaml e Marca.xaml). Se faltar, o erro já diz o que fazer.
    private static T Recurso<T>(string chave) =>
        Application.Current is { } app && app.Resources.TryGetValue(chave, out var valor) && valor is T recurso
            ? recurso
            : throw new InvalidOperationException($"Recurso {chave} ausente: junte Tokens.xaml e Marca.xaml em App.xaml");

    // O MAUI não tem uma API pronta para "movimento reduzido", então
    // perguntamos a cada sistema. O "#if" escolhe o código na compilação: o
    // trecho ANDROID só entra no app Android, o IOS só no iPhone, e o resto nos
    // testes (net10.0).
    //   Android: Opções do desenvolvedor / Acessibilidade → "Remover animações"
    //            deixa a escala de duração das animações em 0;
    //   iOS:     Ajustes → Acessibilidade → Movimento → "Reduzir movimento".
    private static bool MovimentoReduzido()
    {
#if ANDROID
        var resolver = Android.App.Application.Context.ContentResolver;
        return Android.Provider.Settings.Global.GetFloat(resolver, Android.Provider.Settings.Global.AnimatorDurationScale, 1f) == 0f;
#elif IOS
        return UIKit.UIAccessibility.IsReduceMotionEnabled;
#else
        return false;
#endif
    }
}
