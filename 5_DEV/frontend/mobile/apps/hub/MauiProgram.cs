namespace Boaz.Mobile.Hub;

// Ponto de composição do app hub (RN-149): liga as portas do core aos
// adaptadores de infra/ e monta o app. Ainda não há features.
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        return builder.Build();
    }
}
