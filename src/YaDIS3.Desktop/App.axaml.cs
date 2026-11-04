using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using YaDIS3.Core;
using YaDIS3.Desktop.Localization;
using YaDIS3.Desktop.ViewModels;
using YaDIS3.Desktop.Views;

namespace YaDIS3.Desktop;

/// <summary>
/// Charge les ressources et crée la fenêtre principale.
/// </summary>
public sealed partial class App : Application
{
    private ServiceProvider? _services;
    private Exception? _languageFailure;

    /// <summary>
    /// Charge les ressources et le thème Avalonia.
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        _services = DesktopServices.CreateProvider();
        try
        {
            LanguageResources.Apply(
                Resources,
                _services.GetRequiredService<LegacyLanguageCatalog>()
            );
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or System.Xml.XmlException
            )
        {
            _languageFailure = exception;
        }
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
            ServiceProvider services = _services!;
            if (_languageFailure is not null)
            {
                ShowLanguageFailure(desktop);
                base.OnFrameworkInitializationCompleted();
                return;
            }
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

    private static void ShowLanguageFailure(
        IClassicDesktopStyleApplicationLifetime desktop
    )
    {
        // Ces textes de démarrage sont aussi en dur dans yaDIS2 :
        // aucun catalogue ne peut être consulté lorsqu'il est illisible.
        Window error = new()
        {
            Title = "Error !",
            Width = 470,
            Height = 150,
        };
        Button close = new() { Content = "OK" };
        close.Click += (_, _) => desktop.Shutdown(1);
        error.Content = new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text =
                        "Impossible to load the language files !\r\nThe application will close...",
                },
                close,
            },
        };
        desktop.MainWindow = error;
    }
}
