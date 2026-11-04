using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;

namespace YaDIS3.Desktop.Views;

/// <summary>
/// Héberge les contrôles ; le modèle de vue n'applique les
/// modifications que sur OK.
/// </summary>
public sealed partial class ConfigurationWindow : Window
{
    private int _draggedShortcut = -1;
    private ConfigurationViewModel Model =>
        (ConfigurationViewModel)DataContext!;

    /// <summary>
    /// Charge la vue et bloque la fermeture pendant
    /// l'enregistrement.
    /// </summary>
    public ConfigurationWindow()
    {
        InitializeComponent();
        Closing += (_, args) =>
            args.Cancel =
                DataContext
                    is ConfigurationViewModel { IsBusy: true };
        ListBox shortcuts = this.FindControl<ListBox>(
            "ShortcutList"
        )!;
        shortcuts.AddHandler(
            InputElement.PointerPressedEvent,
            ShortcutPointerPressed,
            RoutingStrategies.Tunnel,
            true
        );
        shortcuts.AddHandler(
            InputElement.PointerReleasedEvent,
            ShortcutPointerReleased,
            RoutingStrategies.Tunnel,
            true
        );
    }

    private async void Accept(
        object? sender,
        RoutedEventArgs args
    )
    {
        if (await Model.ApplyAsync())
        {
            Close(true);
        }
    }

    private void Cancel(object? sender, RoutedEventArgs args) =>
        Close(false);

    private async Task<string?> PickFolder(string title)
    {
        IReadOnlyList<IStorageFolder> folders =
            await StorageProvider.OpenFolderPickerAsync(
                new() { Title = title, AllowMultiple = false }
            );
        return folders.Count > 0
            ? folders[0].TryGetLocalPath()
            : null;
    }

    private async void ChooseWorkspace(
        object? sender,
        RoutedEventArgs args
    )
    {
        if (await PickFolder("Espace de travail") is { } path)
        {
            Model.ChooseWorkspace(path);
        }
    }

    private async void ChooseDestination(
        object? sender,
        RoutedEventArgs args
    )
    {
        if (
            await PickFolder(
                "Emplacement de l’interface Dune"
            ) is
            { } path
        )
        {
            Model.ChooseDestination(path);
        }
    }

    private async void ChooseTvdb(
        object? sender,
        RoutedEventArgs args
    )
    {
        IReadOnlyList<IStorageFile> files =
            await StorageProvider.OpenFilePickerAsync(
                new()
                {
                    Title = "Configuration TVDB v4",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType(
                            "Configuration JSON"
                        )
                        {
                            Patterns = ["*.json"],
                        },
                    ],
                }
            );
        if (
            files.Count > 0
            && files[0].TryGetLocalPath() is { } path
        )
        {
            Model.TvdbPath = path;
        }
    }

    private async void AddFolder(
        object? sender,
        RoutedEventArgs args
    )
    {
        SourceFolderViewModel draft = Model.CreateFolderDraft(
            new("", 0, "")
        );
        SourceFolderWindow dialog = new()
        {
            DataContext = draft,
        };
        if (await dialog.ShowDialog<bool>(this))
        {
            Model.UpdateFolder(draft);
        }
    }

    private async void EditFolder(
        object? sender,
        RoutedEventArgs args
    )
    {
        if (Model.SelectedFolder is not { } current)
        {
            return;
        }
        SourceFolderViewModel draft = Model.CreateFolderDraft(
            current.Snapshot()
        );
        SourceFolderWindow dialog = new()
        {
            DataContext = draft,
            Title = "Éditer un dossier",
        };
        if (await dialog.ShowDialog<bool>(this))
        {
            Model.UpdateFolder(draft, current);
        }
    }

    private async void EnlargePreview(
        object? sender,
        RoutedEventArgs args
    )
    {
        if (Model.TemplatePreview is null)
        {
            return;
        }
        Window dialog = new()
        {
            Title = Model.SelectedTemplate?.Name ?? "Thème",
            Width = 960,
            Height = 570,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner,
            Content = new Image
            {
                Source = Model.TemplatePreview,
                Stretch = Avalonia.Media.Stretch.Uniform,
            },
        };
        await dialog.ShowDialog(this);
    }

    private void ShortcutKeyDown(
        object? sender,
        KeyEventArgs args
    )
    {
        if (args.Key == Key.Delete && sender is ListBox list)
        {
            Model.RemoveShortcuts(
                list.SelectedItems!.OfType<DuneShortcut>()
            );
            args.Handled = true;
        }
    }

    private void ShortcutPointerPressed(
        object? sender,
        PointerPressedEventArgs args
    ) =>
        _draggedShortcut = ShortcutIndex(
            sender as ListBox,
            args
        );

    private void ShortcutPointerReleased(
        object? sender,
        PointerReleasedEventArgs args
    )
    {
        Model.MoveShortcut(
            _draggedShortcut,
            ShortcutIndex(sender as ListBox, args)
        );
        _draggedShortcut = -1;
    }

    private static int ShortcutIndex(
        ListBox? list,
        PointerEventArgs args
    )
    {
        if (list is null)
        {
            return -1;
        }
        ListBoxItem? container = list.GetVisualAt(
                args.GetPosition(list)
            )
            ?.GetSelfAndVisualAncestors()
            .OfType<ListBoxItem>()
            .FirstOrDefault();
        return container is null
            ? -1
            : list.IndexFromContainer(container);
    }
}
