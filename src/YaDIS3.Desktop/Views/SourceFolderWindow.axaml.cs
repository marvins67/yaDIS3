using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using YaDIS3.Desktop.ViewModels;

namespace YaDIS3.Desktop.Views;

/// <summary>
/// Héberge la saisie et le sélecteur natif ; la validation
/// appartient au modèle de vue.
/// </summary>
public sealed partial class SourceFolderWindow : Window
{
    private SourceFolderViewModel Model =>
        (SourceFolderViewModel)DataContext!;

    /// <summary>
    /// Charge la boîte d'ajout ou d'édition.
    /// </summary>
    public SourceFolderWindow() => InitializeComponent();

    private void Accept(object? sender, RoutedEventArgs args) =>
        Close(true);

    private void Cancel(object? sender, RoutedEventArgs args) =>
        Close(false);

    private async void Browse(
        object? sender,
        RoutedEventArgs args
    )
    {
        IReadOnlyList<IStorageFolder> folders =
            await StorageProvider.OpenFolderPickerAsync(
                new()
                {
                    Title = Model.Texts.GetString("353"),
                    AllowMultiple = false,
                }
            );
        if (
            folders.Count > 0
            && folders[0].TryGetLocalPath() is { } path
        )
        {
            await (
                (SourceFolderViewModel)DataContext!
            ).ChooseFolderAsync(path);
        }
    }
}
