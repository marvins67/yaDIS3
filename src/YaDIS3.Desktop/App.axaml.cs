using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using YaDIS3.Desktop.Views;
using YaDIS3.Desktop.ViewModels;
namespace YaDIS3.Desktop;
public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = new MainWindow { DataContext = new MainWindowViewModel() };
        base.OnFrameworkInitializationCompleted();
    }
}
