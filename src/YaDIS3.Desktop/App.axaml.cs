using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using YaDIS3.Desktop.ViewModels;
using YaDIS3.Desktop.Views;

namespace YaDIS3.Desktop;

/// <summary>
/// Charge les ressources et crée la fenêtre principale.
/// </summary>
public sealed partial class App : Application
{
    /// <summary>
    /// Charge les ressources et le thème Avalonia.
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Crée la fenêtre principale après l'initialisation de
    /// l'application.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (
            ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop
        )
        {
            ServiceProvider services =
                DesktopServices.CreateProvider();
            desktop.Exit += (_, _) => services.Dispose();
            MainWindowViewModel model =
                services.GetRequiredService<MainWindowViewModel>();
            MainWindow window = new() { DataContext = model };
            window.Opened += async (_, _) =>
                await model.RestoreWorkspaceAsync();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
