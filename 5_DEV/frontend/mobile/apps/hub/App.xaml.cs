using Boaz.Mobile.Hub.Infra.In.Commons;

namespace Boaz.Mobile.Hub;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    // Passo 2: o MAUI chama CreateWindow ao abrir o app; a janela começa pela
    // página de abertura (antes dela, o sistema já mostrou a splash nativa
    // configurada no .csproj).
    protected override Window CreateWindow(IActivationState? activationState) => new(new AberturaPage());
}
