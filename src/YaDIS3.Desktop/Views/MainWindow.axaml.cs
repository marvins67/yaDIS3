using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;
namespace YaDIS3.Desktop.Views;

public sealed partial class MainWindow : Window
{
    private bool allowClose, prompting;
    private MainWindowViewModel Model => (MainWindowViewModel)DataContext!;
    public MainWindow()
    {
        InitializeComponent(); Closing += CheckClosing;
        Closed += (_, _) => (DataContext as IDisposable)?.Dispose();
    }
    private async Task<string?> Folder(string title)
    {
        var selected = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return selected.FirstOrDefault()?.TryGetLocalPath();
    }
    private async Task<bool> ConfirmSwitch()
    {
        if (!Model.HasChanges) return true;
        var choice = await UnsavedDialog();
        if (choice == "discard") return true;
        if (choice != "save") return false;
        await Model.RunAsync(Model.SaveAllAsync); return !Model.HasChanges;
    }
    private async void OpenWorkspace(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmSwitch()) return;
        var path = await Folder("Ouvrir le workspace contenant data");
        if (path is not null) await Model.RunAsync(() => Model.OpenAsync(path));
    }
    private async void CreateWorkspace(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmSwitch()) return;
        var path = await Folder("Choisir un dossier pour la bibliothèque");
        if (path is not null) await Model.RunAsync(() => Model.OpenAsync(path, true));
    }
    private async void AddManualSeries(object? sender, RoutedEventArgs e)
    {
        var path = await Folder("Dossier des vidéos de la série");
        if (path is not null) await Model.RunAsync(() => Model.AddManualSeriesAsync(path));
    }
    private async void ScanEpisodes(object? sender, RoutedEventArgs e)
    {
        var path = await Folder("Dossier des vidéos à analyser");
        if (path is not null) await Model.RunAsync(() => Model.ScanAsync(path));
    }
    private async void ChooseTemplate(object? sender, RoutedEventArgs e)
    {
        var directory = await Folder("Dossier contenant template.xml");
        if (directory is not null) await Model.RunAsync(() => { Model.LoadTemplate(directory); return Task.CompletedTask; });
    }
    private async void ChooseExportDestination(object? sender, RoutedEventArgs e)
    {
        var directory = await Folder("Dossier où créer l'export yaDIS3-modern");
        if (directory is not null) Model.ExportParentPath = directory;
    }
    private async void ConfigureTvdb(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Votre tvdb-v4.json", AllowMultiple = false, FileTypeFilter = [new FilePickerFileType("Configuration JSON") { Patterns = ["*.json"] }] });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) await Model.RunAsync(() => { Model.ConfigureTvdb(path); return Task.CompletedTask; });
    }
    private async void SearchSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.OfType<SeriesMatch>().FirstOrDefault() is { } match) Model.SelectedMatch = match;
        if (Model.SelectedMatch is not null && !Model.IsBusy) await Model.RunAsync(Model.LoadOrdersAsync);
    }
    private async Task Import(ArtworkKind kind)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Choisir une image", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Images") { Patterns = ["*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.webp"] }] });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) await Model.RunAsync(() => { Model.ImportImage(kind, path); return Task.CompletedTask; });
    }
    private async void ImportFanart(object? sender, RoutedEventArgs e) => await Import(ArtworkKind.Fanart);
    private async void ImportBanner(object? sender, RoutedEventArgs e) => await Import(ArtworkKind.Banner);
    private async void ImportPoster(object? sender, RoutedEventArgs e) => await Import(ArtworkKind.Poster);
    private async void ImportEpisodeImage(object? sender, RoutedEventArgs e) => await Import(ArtworkKind.Episode);
    private Task<string?> UnsavedDialog()
    {
        var dialog = new Window { Title = "Modifications en cours", Width = 540, Height = 200, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 20 };
        panel.Children.Add(new TextBlock { Text = "Enregistrer les fiches modifiées avant de continuer ?", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var (text, value) in new[] { ("Enregistrer", "save"), ("Ignorer", "discard"), ("Annuler", "cancel") })
        { var button = new Button { Content = text }; button.Click += (_, _) => dialog.Close(value); buttons.Children.Add(button); }
        panel.Children.Add(buttons); dialog.Content = panel; return dialog.ShowDialog<string?>(this);
    }
    private async void CheckClosing(object? sender, WindowClosingEventArgs e)
    {
        if (allowClose || DataContext is not MainWindowViewModel model) return;
        if (model.IsBusy) { e.Cancel = true; model.Status = "Attendez la fin de l'opération avant de fermer."; return; }
        if (!model.HasChanges) return;
        e.Cancel = true; if (prompting) return;
        prompting = true;
        try { if (await ConfirmSwitch()) { allowClose = true; Close(); } }
        finally { prompting = false; }
    }
}
