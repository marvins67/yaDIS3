using Avalonia;

namespace YaDIS3.Desktop;

internal static class Program
{
    /// <summary>
    /// Starts the desktop application and its event loop.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Configures Avalonia for the current desktop platform.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
