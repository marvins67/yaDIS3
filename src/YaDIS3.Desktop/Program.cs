using Avalonia;

namespace YaDIS3.Desktop;

internal static class Program
{
    /// <summary>
    /// Démarre l'application et sa boucle d'événements.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Configure Avalonia pour la plateforme courante.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
