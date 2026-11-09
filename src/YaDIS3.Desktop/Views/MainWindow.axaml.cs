using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;

namespace YaDIS3.Desktop.Views;

/// <summary>
/// Héberge l'éditeur et les sélecteurs de fichiers natifs.
/// </summary>
public sealed partial class MainWindow : Window
{
    private bool _allowClose;

    private MainWindowViewModel? _contextGroupModel;

    /// <summary>Application.Restart abandonne les brouillons de la fenêtre courante.</summary>
    internal void PrepareForRestart() => _allowClose = true;

    private bool _prompting;

    private MainWindowViewModel Model =>
        (MainWindowViewModel)DataContext!;

    /// <summary>
    /// Initialise la fenêtre et ses événements de fermeture.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        Closing += CheckClosing;
        Closed += (_, _) =>
            (DataContext as IDisposable)?.Dispose();
        DataContextChanged += (_, _) =>
        {
            if (_contextGroupModel is not null)
            {
                _contextGroupModel.GroupCreationRequested -=
                    RequestGroupCreation;
                _contextGroupModel.SagaEditingRequested -=
                    RequestSagaEditing;
            }
            _contextGroupModel = DataContext as MainWindowViewModel;
            if (_contextGroupModel is not null)
            {
                _contextGroupModel.GroupCreationRequested +=
                    RequestGroupCreation;
                _contextGroupModel.SagaEditingRequested +=
                    RequestSagaEditing;
            }
        };
    }

    private async void EditorTabChanged(
        object? sender,
        SelectionChangedEventArgs eventArgs
    )
    {
        if (DataContext is not MainWindowViewModel model
            || !ReferenceEquals(eventArgs.Source, sender)
            || sender is not TabControl tabs)
        {
            return;
        }
        string? name = (tabs.SelectedItem as TabItem)?.Name;
        if (name is "PosterTab" or "FanartTab")
        {
            await model.LoadArtworkChoicesAsync(
                name == "PosterTab"
                    ? ArtworkKind.Poster
                    : ArtworkKind.Fanart
            );
        }
    }

    private void PrepareGenres(
        object? sender,
        EventArgs eventArgs
    ) => Model.PrepareGenreChoices();

    private void ApplyGenres(
        object? sender,
        EventArgs eventArgs
    ) => Model.ApplyGenreChoices();

    private async void ShowConfiguration(
        object? sender,
        RoutedEventArgs eventArgs
    ) => await OpenConfigurationAsync();

    private async void ShowDuneSettings(
        object? sender,
        RoutedEventArgs eventArgs
    ) => await OpenConfigurationAsync(2);

    private Task OpenConfigurationAsync(int tabIndex = 0) =>
        Model.RunAsync(async () =>
        {
            using ConfigurationViewModel draft =
                Model.CreateConfiguration();
            ConfigurationWindow dialog = new()
            {
                DataContext = draft,
            };
            dialog
                .FindControl<TabControl>("ConfigurationTabs")!
                .SelectedIndex = tabIndex;
            await dialog.ShowDialog(this);
        });

    private async void RequestGroupCreation(string kind)
    {
        string id = kind == "saga" ? "186" : "167";
        Window dialog = new()
        {
            Title = Model.Texts.GetString(id),
            Width = 360,
            Height = 145,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        TextBox input = new()
        {
            PlaceholderText = Model.Texts.GetString("169"),
        };
        Button create = new()
        {
            Content = Model.Texts.GetString("86"),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        create.Click += (_, _) => dialog.Close(input.Text);
        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 12,
            Children = { input, create },
        };
        string? name = await dialog.ShowDialog<string?>(this);
        if (!string.IsNullOrWhiteSpace(name))
        {
            await Model.CreateContextGroupAsync(kind, name);
        }
    }

    private async void RequestSagaEditing(string name)
    {
        SagaContextDraft original = Model.ReadSaga(name);
        SagaEditorWindow editor = new(
            original,
            Model.CurrentWorkspaceDirectory
        );
        SagaContextDraft? edited =
            await editor.ShowDialog<SagaContextDraft?>(this);
        if (edited is not null)
        {
            await Model.EditContextSagaAsync(name, edited);
        }
    }

    private async void ChangeContextTitle(
        object? sender,
        RoutedEventArgs eventArgs
    )
    {
        if (Model.SelectedCard is null)
        {
            return;
        }

        if (Model.IsSeries)
        {
            await ChangeSeriesContextTitleAsync();
        }
        else
        {
            await ChangeMovieContextTitleAsync();
        }
    }

    private async Task ChangeMovieContextTitleAsync()
    {
        Window dialog = CreateTitleDialog();
        TextBox query = new() { Text = Model.SelectedCard?.Title ?? "" };
        Button search = new() { Content = Model.Texts.GetString("114") };
        ListBox results = new() { Height = 290 };
        Button apply = new()
        {
            Content = Model.Texts.GetString("115"),
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        async Task FindAsync()
        {
            apply.IsEnabled = false;
            results.ItemsSource = null;
            try
            {
                results.ItemsSource = await Model.SearchMovieTitlesAsync(query.Text ?? "");
            }
            catch (Exception exception)
            {
                Model.Status = exception.Message;
            }
        }
        search.Click += async (_, _) => await FindAsync();
        query.KeyDown += async (_, args) =>
        {
            if (args.Key == Avalonia.Input.Key.Enter)
            {
                await FindAsync();
            }
        };
        results.SelectionChanged += (_, _) =>
            apply.IsEnabled = results.SelectedItem is MovieTitleMatch;
        results.DoubleTapped += (_, _) =>
        {
            if (results.SelectedItem is MovieTitleMatch match)
            {
                dialog.Close(match);
            }
        };
        apply.Click += (_, _) =>
        {
            if (results.SelectedItem is MovieTitleMatch match)
            {
                dialog.Close(match);
            }
        };
        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 10,
            Children = { query, search, results, apply },
        };
        dialog.Opened += async (_, _) => await FindAsync();
        MovieTitleMatch? selected =
            await dialog.ShowDialog<MovieTitleMatch?>(this);
        if (selected is not null)
        {
            await Model.ChangeMovieTitleAsync(selected);
        }
    }

    private async Task ChangeSeriesContextTitleAsync()
    {
        Window dialog = CreateTitleDialog();
        TextBox query = new() { Text = Model.SelectedCard?.Title ?? "" };
        Button search = new() { Content = Model.Texts.GetString("114") };
        ListBox results = new() { Height = 250 };
        ComboBox orders = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
        Button apply = new()
        {
            Content = Model.Texts.GetString("115"),
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        async Task FindAsync()
        {
            apply.IsEnabled = false;
            results.ItemsSource = null;
            orders.ItemsSource = null;
            Model.Query = query.Text ?? "";
            try
            {
                await Model.SearchAsync();
                results.ItemsSource = Model.SearchResults;
            }
            catch (Exception exception)
            {
                Model.Status = exception.Message;
            }
        }
        search.Click += async (_, _) => await FindAsync();
        query.KeyDown += async (_, args) =>
        {
            if (args.Key == Avalonia.Input.Key.Enter)
            {
                await FindAsync();
            }
        };
        results.SelectionChanged += async (_, _) =>
        {
            apply.IsEnabled = false;
            orders.ItemsSource = null;
            if (results.SelectedItem is SeriesMatch match)
            {
                Model.SelectedMatch = match;
                try
                {
                    await Model.LoadOrdersAsync();
                    if (results.SelectedItem == match)
                    {
                        orders.ItemsSource = Model.Orders;
                        orders.SelectedItem = Model.SelectedOrder;
                        apply.IsEnabled = orders.SelectedItem is EpisodeOrder;
                    }
                }
                catch (Exception exception)
                {
                    Model.Status = exception.Message;
                }
            }
        };
        orders.SelectionChanged += (_, _) =>
            apply.IsEnabled = results.SelectedItem is SeriesMatch
                && orders.SelectedItem is EpisodeOrder;
        apply.Click += (_, _) =>
        {
            if (results.SelectedItem is SeriesMatch match
                && orders.SelectedItem is EpisodeOrder order)
            {
                dialog.Close((match.Id, order.Code));
            }
        };
        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 10,
            Children = { query, search, results, orders, apply },
        };
        dialog.Opened += async (_, _) => await FindAsync();
        (long, string)? selected =
            await dialog.ShowDialog<(long, string)?>(this);
        if (selected is { } choice)
        {
            await Model.ChangeSeriesTitleAsync(choice.Item1, choice.Item2);
        }
    }

    private Window CreateTitleDialog() => new()
    {
        Title = Model.Texts.GetString("62"),
        Width = 620,
        Height = 470,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
    };

    private void BeginManualMovieEdit(
        object? sender,
        RoutedEventArgs eventArgs
    )
    {
        Model.BeginManualMovieEdit();
        this.FindControl<TextBox>("MovieTitleEditor")?.Focus();
    }

    private void OpenDataFolder(object? sender, RoutedEventArgs eventArgs)
    {
        if (Model.SelectedCard is { } card)
        {
            OpenExternal(card.DataDirectory);
        }
    }

    private void OpenMediaFolder(object? sender, RoutedEventArgs eventArgs)
    {
        if (Model.SelectedCard is { } card)
        {
            OpenExternal(card.MediaFolderPath);
        }
    }

    private void OpenInformationPage(object? sender, RoutedEventArgs eventArgs)
    {
        if (Model.SelectedCard is { } card
            && !string.IsNullOrWhiteSpace(card.InformationUrl))
        {
            OpenExternal(card.InformationUrl);
        }
    }

    private void OpenExternal(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            Model.Status = exception.Message;
        }
    }

    private async void ShowTvdbSearch(
        object? sender,
        RoutedEventArgs eventArgs
    )
    {
        if (Model.IsSeries)
        {
            await ShowUtilityDialog(
                "TvdbSearchTab",
                Model.Texts.GetString("62")
            );
        }
    }

    private async Task ShowUtilityDialog(
        string tabName,
        string title
    )
    {
        TabItem tab = this.FindControl<TabItem>(tabName)!;
        object? content = tab.Content;
        tab.Content = null;
        Window dialog = new()
        {
            Title = title,
            Width = 850,
            Height = 650,
            MinWidth = 650,
            MinHeight = 500,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner,
            DataContext = Model,
            Content = content,
        };
        try
        {
            await dialog.ShowDialog(this);
        }
        finally
        {
            dialog.Content = null;
            tab.Content = content;
        }
    }

    private async void RefreshWorkspace(
        object? sender,
        RoutedEventArgs eventArgs
    )
    {
        if (!Model.HasWorkspace || !await ConfirmSwitch())
        {
            return;
        }

        await Model.RunAsync(() =>
            Model.RefreshConfiguredWorkspaceAsync(
                Model.OperationToken
            )
        );
    }

    private async void FlushImageCache(
        object? sender,
        RoutedEventArgs eventArgs
    )
    {
        if (!Model.HasWorkspace)
        {
            return;
        }

        await Model.RunAsync(Model.FlushImageCacheAsync);
        await ShowToolResultAsync("76", false);
    }

    private async void ResetDuneInterface(
        object? sender,
        RoutedEventArgs eventArgs
    )
    {
        if (!Model.HasWorkspace)
        {
            return;
        }

        await Model.RunAsync(Model.ResetDuneInterfaceAsync);
        await ShowToolResultAsync("75", true);
    }

    private async Task ShowToolResultAsync(
        string titleId,
        bool requiresReprocessing
    )
    {
        string message = Model.Texts.GetString("77") + ".";
        if (requiresReprocessing)
        {
            message += Environment.NewLine
                + Environment.NewLine
                + Model.Texts.GetString("78") + ".";
        }

        Window dialog = new()
        {
            Title = Model.Texts.GetString(titleId),
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        Button close = new()
        {
            Content = Model.Texts.GetString("34"),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        close.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                },
                close,
            },
        };
        await dialog.ShowDialog(this);
    }

    private async void ShowAbout(
        object? sender,
        RoutedEventArgs eventArgs
    )
    {
        var dialog = new Window
        {
            Title = Model
                .Texts.GetString("138")
                .Replace(
                    "yaDIS",
                    "yaDIS3",
                    StringComparison.Ordinal
                ),
            Width = 460,
            Height = 240,
            CanResize = false,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner,
        };
        var close = new Button
        {
            Content = Model.Texts.GetString("34"),
            HorizontalAlignment = Avalonia
                .Layout
                .HorizontalAlignment
                .Right,
        };
        close.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = "yaDIS3", FontSize = 24 },
                new TextBlock
                {
                    Text = Model.Texts.GetString("355"),
                },
                new TextBlock
                {
                    Text =
                        "Frédéric L’hote 2009–2013 — MarvinS 2026",
                    TextWrapping = Avalonia
                        .Media
                        .TextWrapping
                        .Wrap,
                },
                new TextBlock
                {
                    Text = Model.Texts.GetString("356"),
                },
                close,
            },
        };
        await dialog.ShowDialog(this);
    }

    private async Task<string?> Folder(string title)
    {
        IReadOnlyList<IStorageFolder> selected =
            await StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false,
                }
            );
        return selected.Count == 0
            ? null
            : selected[0].TryGetLocalPath();
    }

    private async Task<bool> ConfirmSwitch()
    {
        if (!Model.HasChanges)
        {
            return true;
        }

        string? choice = await UnsavedDialog();
        if (choice == "discard")
        {
            return true;
        }

        if (choice != "save")
        {
            return false;
        }

        await Model.RunAsync(Model.SaveAllAsync);
        return !Model.HasChanges;
    }

    private async void CreateWorkspace(
        object? sender,
        RoutedEventArgs eventArgs
    )
    {
        if (!await ConfirmSwitch())
        {
            return;
        }

        string? path = await Folder(
            Model.Texts.GetString("358")
        );
        if (path is not null)
        {
            await Model.RunAsync(() =>
                Model.OpenAsync(path, true, Model.OperationToken)
            );
        }
    }

    private async void AddManualSeries(
        object? sender,
        RoutedEventArgs eventArgs
    )
    {
        string? path = await Folder(
            Model.Texts.GetString("359")
        );
        if (path is not null)
        {
            await Model.RunAsync(() =>
                Model.AddManualSeriesAsync(path)
            );
        }
    }

    private async void ScanEpisodes(
        object? sender,
        RoutedEventArgs eventArgs
    )
    {
        string? path = await Folder(
            Model.Texts.GetString("360")
        );
        if (path is not null)
        {
            await Model.RunAsync(() => Model.ScanAsync(path));
        }
    }

    private async void ChooseTemplate(
        object? sender,
        RoutedEventArgs eventArgs
    )
    {
        string? directory = await Folder(
            Model.Texts.GetString("361")
        );
        if (directory is not null)
        {
            await Model.RunAsync(() =>
            {
                Model.LoadTemplate(directory);
                return Task.CompletedTask;
            });
        }
    }

    private async void ChooseExportDestination(
        object? sender,
        RoutedEventArgs eventArgs
    )
    {
        string? directory = await Folder(
            Model.Texts.GetString("362")
        );
        if (directory is not null)
        {
            Model.ExportParentPath = directory;
        }
    }

    private async void ConfigureTvdb(
        object? sender,
        RoutedEventArgs eventArgs
    )
    {
        IReadOnlyList<IStorageFile> files =
            await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = Model.Texts.GetString("363"),
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType(
                            Model.Texts.GetString("354")
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
            await Model.RunAsync(() =>
            {
                Model.ConfigureTvdb(path);
                return Task.CompletedTask;
            });
        }
    }

    private async void SearchSelectionChanged(
        object? sender,
        SelectionChangedEventArgs eventArgs
    )
    {
        if (
            eventArgs
                .AddedItems.OfType<SeriesMatch>()
                .FirstOrDefault() is
            { } match
        )
        {
            Model.SelectedMatch = match;
        }

        if (Model.SelectedMatch is not null && !Model.IsBusy)
        {
            await Model.RunAsync(Model.LoadOrdersAsync);
        }
    }

    private async Task Import(ArtworkKind kind)
    {
        IReadOnlyList<IStorageFile> files =
            await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = Model.Texts.GetString("364"),
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType(
                            Model.Texts.GetString("341")
                        )
                        {
                            Patterns =
                            [
                                "*.jpg",
                                "*.jpeg",
                                "*.png",
                                "*.bmp",
                                "*.gif",
                                "*.webp",
                            ],
                        },
                    ],
                }
            );
        if (
            files.Count > 0
            && files[0].TryGetLocalPath() is { } path
        )
        {
            await Model.RunAsync(() =>
            {
                Model.ImportImage(kind, path);
                return Task.CompletedTask;
            });
        }
    }

    private async void ImportFanart(
        object? sender,
        RoutedEventArgs eventArgs
    )
    {
        await Import(ArtworkKind.Fanart);
    }

    private async void ImportBanner(
        object? sender,
        RoutedEventArgs eventArgs
    )
    {
        await Import(ArtworkKind.Banner);
    }

    private async void ImportPoster(
        object? sender,
        RoutedEventArgs eventArgs
    )
    {
        await Import(ArtworkKind.Poster);
    }

    private async void ImportEpisodeImage(
        object? sender,
        RoutedEventArgs eventArgs
    )
    {
        await Import(ArtworkKind.Episode);
    }

    private Task<string?> UnsavedDialog()
    {
        var dialog = new Window
        {
            Title = Model.Texts.GetString("365"),

            Width = 540,

            Height = 200,

            CanResize = false,

            WindowStartupLocation =
                WindowStartupLocation.CenterOwner,
        };
        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),

            Spacing = 20,
        };
        panel.Children.Add(
            new TextBlock
            {
                Text = Model.Texts.GetString("366"),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            }
        );
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,

            Spacing = 10,
        };
        foreach (
            var (text, value) in new[]
            {
                (Model.Texts.GetString("367"), "save"),
                (Model.Texts.GetString("368"), "discard"),
                (Model.Texts.GetString("90"), "cancel"),
            }
        )
        {
            var button = new Button { Content = text };
            button.Click += (_, _) => dialog.Close(value);
            buttons.Children.Add(button);
        }

        panel.Children.Add(buttons);
        dialog.Content = panel;
        return dialog.ShowDialog<string?>(this);
    }

    private async void CheckClosing(
        object? sender,
        WindowClosingEventArgs eventArgs
    )
    {
        if (
            _allowClose
            || DataContext is not MainWindowViewModel model
        )
        {
            return;
        }

        if (model.IsBusy)
        {
            eventArgs.Cancel = true;
            model.Status = Model.Texts.GetString("369");
            return;
        }

        if (!model.HasChanges)
        {
            return;
        }

        eventArgs.Cancel = true;
        if (_prompting)
        {
            return;
        }

        _prompting = true;
        try
        {
            if (await ConfirmSwitch())
            {
                _allowClose = true;
                Close();
            }
        }
        finally
        {
            _prompting = false;
        }
    }
}
