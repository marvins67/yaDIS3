using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using YaDIS3.Desktop.ViewModels;
using YaDIS3.Desktop.Views;

namespace YaDIS3.Desktop;

/// <summary>
/// Loads application resources and creates the desktop
/// window.
/// </summary>
public sealed partial class App : Application
{
    /// <summary>
    /// Loads the Avalonia application resources and theme.
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Creates the main window when desktop initialization
    /// completes.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (
            ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop
        )
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
