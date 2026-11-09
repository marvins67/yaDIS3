using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
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
        ListBox results = CreateTitleResults();
        Button apply = CreateTitleApplyButton();
        Dictionary<ListBoxItem, MovieTitleMatch> candidates = [];

        async Task FindAsync()
        {
            apply.IsEnabled = false;
            candidates.Clear();
            results.ItemsSource = null;
            string searchText = query.Text?.Trim() ?? "";
            if (searchText.Length == 0)
            {
                return;
            }
            try
            {
                // Un identifiant IMDb applique directement la fiche, comme ChangeForm.
                if (searchText.StartsWith("tt", StringComparison.OrdinalIgnoreCase))
                {
                    MovieTitleMatch? direct = await Model.FindMovieByImdbAsync(searchText);
                    if (direct is not null)
                    {
                        dialog.Close(direct);
                    }
                    else
                    {
                        Model.Status = Model.Texts.GetString("142");
                    }
                    return;
                }

                IReadOnlyList<MovieTitleMatch> matches =
                    await Model.SearchMovieTitlesAsync(searchText);
                List<ListBoxItem> items = [];
                foreach (MovieTitleMatch match in matches)
                {
                    ListBoxItem item = CreateTitleResultItem(
                        match.Title,
                        match.OriginalTitle,
                        match.ReleaseDate,
                        "",
                        match.PosterPath
                    );
                    candidates.Add(item, match);
                    items.Add(item);
                }
                results.ItemsSource = items;
            }
            catch (Exception exception)
            {
                Model.Status = exception.Message;
            }
        }

        results.SelectionChanged += (_, _) =>
            apply.IsEnabled = results.SelectedItem is ListBoxItem item
                && candidates.ContainsKey(item);
        void Accept()
        {
            if (results.SelectedItem is ListBoxItem item
                && candidates.TryGetValue(item, out MovieTitleMatch? match))
            {
                dialog.Close(match);
            }
        }
        results.DoubleTapped += (_, _) => Accept();
        apply.Click += (_, _) => Accept();
        dialog.Content = CreateTitleContent(dialog, results, query, apply, FindAsync);
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
        ListBox results = CreateTitleResults();
        ComboBox orders = new()
        {
            Width = 282,
            IsEnabled = false,
            ItemTemplate = new FuncDataTemplate<EpisodeOrder>(
                (order, _) => new TextBlock { Text = order?.Name }
            ),
        };
        void ShowOrderMessage(string message)
        {
            orders.IsEnabled = false;
            orders.ItemsSource = new[] { message };
            orders.SelectedIndex = 0;
        }
        ShowOrderMessage("Sélectionnez une série dans les résultats");
        Button apply = CreateTitleApplyButton();
        Dictionary<ListBoxItem, SeriesMatch> candidates = [];

        async Task FindAsync()
        {
            apply.IsEnabled = false;
            candidates.Clear();
            results.ItemsSource = null;
            ShowOrderMessage("Sélectionnez une série dans les résultats");
            Model.Query = query.Text ?? "";
            if (string.IsNullOrWhiteSpace(Model.Query))
            {
                return;
            }
            try
            {
                await Model.SearchAsync();
                List<ListBoxItem> items = [];
                foreach (SeriesMatch match in Model.SearchResults)
                {
                    ListBoxItem item = CreateTitleResultItem(
                        match.Name, "", match.Year, match.Overview, match.PosterUrl
                    );
                    candidates.Add(item, match);
                    items.Add(item);
                }
                results.ItemsSource = items;
            }
            catch (Exception exception)
            {
                Model.Status = exception.Message;
            }
        }

        results.SelectionChanged += async (_, _) =>
        {
            apply.IsEnabled = false;
            ShowOrderMessage("Sélectionnez une série dans les résultats");
            if (results.SelectedItem is ListBoxItem item
                && candidates.TryGetValue(item, out SeriesMatch? match))
            {
                ShowOrderMessage("Chargement des ordres TVDB…");
                Model.SelectedMatch = match;
                try
                {
                    await Model.LoadOrdersAsync();
                    if (ReferenceEquals(results.SelectedItem, item))
                    {
                        if (Model.Orders.Count == 0)
                        {
                            ShowOrderMessage("TVDB n’a indiqué aucun ordre disponible pour cette série.");
                            return;
                        }
                        orders.ItemsSource = Model.Orders;
                        orders.SelectedItem = Model.SelectedOrder;
                        orders.IsEnabled = true;
                        apply.IsEnabled = orders.SelectedItem is EpisodeOrder;
                    }
                }
                catch (Exception exception)
                {
                    if (ReferenceEquals(results.SelectedItem, item))
                    {
                        ShowOrderMessage("Ordres TVDB indisponibles : " + exception.Message);
                    }
                    Model.Status = exception.Message;
                }
            }
        };
        orders.SelectionChanged += (_, _) =>
            apply.IsEnabled = results.SelectedItem is ListBoxItem
                && orders.IsEnabled
                && orders.SelectedItem is EpisodeOrder;

        void Accept()
        {
            if (results.SelectedItem is ListBoxItem item
                && candidates.TryGetValue(item, out SeriesMatch? match)
                && orders.IsEnabled
                && orders.SelectedItem is EpisodeOrder order)
            {
                dialog.Close((match.Id, order.Code));
            }
        }
        results.DoubleTapped += (_, _) => Accept();
        apply.Click += (_, _) => Accept();
        dialog.Content = CreateTitleContent(dialog, results, query, apply, FindAsync, orders);
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
        Width = 714,
        Height = 510,
        MinWidth = 714,
        MinHeight = 510,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
    };

    private static ListBox CreateTitleResults() => new()
    {
        Background = Avalonia.Media.Brushes.White,
        BorderBrush = Avalonia.Media.Brushes.DarkGray,
        BorderThickness = new Avalonia.Thickness(1),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
    };

    private Button CreateTitleApplyButton() => new()
    {
        Content = Model.Texts.GetString("115"),
        IsEnabled = false,
        MinWidth = 80,
    };

    private Grid CreateTitleContent(
        Window dialog,
        ListBox results,
        TextBox query,
        Button apply,
        Func<Task> search,
        ComboBox? orders = null
    )
    {
        Grid layout = new()
        {
            Margin = new Avalonia.Thickness(12),
            RowDefinitions = new RowDefinitions("*,Auto"),
            RowSpacing = 12,
        };
        layout.Children.Add(results);

        Grid footer = new()
        {
            ColumnDefinitions = new ColumnDefinitions("230,*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            RowSpacing = 3,
            ColumnSpacing = 8,
        };
        TextBlock label = new()
        {
            Text = Model.Texts.GetString("114") + " :",
            FontSize = 12,
        };
        footer.Children.Add(label);
        Grid searchRow = new()
        {
            ColumnDefinitions = new ColumnDefinitions("192,26,Auto"),
            ColumnSpacing = 4,
        };
        searchRow.Children.Add(query);
        Button searchButton = new()
        {
            Width = 26,
            Height = 26,
            Padding = new Avalonia.Thickness(2),
            Content = new Image
            {
                Source = new Avalonia.Media.Imaging.Bitmap(
                    Avalonia.Platform.AssetLoader.Open(
                        new Uri("avares://yaDIS3.Desktop/Assets/Icons/search.png")
                    )
                ),
                Width = 18,
                Height = 18,
            },
        };
        Grid.SetColumn(searchButton, 1);
        searchRow.Children.Add(searchButton);
        if (orders is not null)
        {
            Grid.SetColumn(orders, 2);
            searchRow.Children.Add(orders);
            TextBlock orderLabel = new()
            {
                Text = "Ordre des épisodes (TVDB) :",
                FontSize = 12,
                Margin = new Avalonia.Thickness(8, 0, 0, 0),
            };
            Grid.SetColumn(orderLabel, 1);
            footer.Children.Add(orderLabel);
        }
        Grid.SetRow(searchRow, 1);
        Grid.SetColumnSpan(searchRow, 2);
        footer.Children.Add(searchRow);

        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        actions.Children.Add(apply);
        Button cancel = new()
        {
            Content = Model.Texts.GetString("90"),
            MinWidth = 80,
        };
        cancel.Click += (_, _) => dialog.Close();
        actions.Children.Add(cancel);
        Grid.SetRow(actions, 1);
        Grid.SetColumn(actions, 2);
        footer.Children.Add(actions);
        Grid.SetRow(footer, 1);
        layout.Children.Add(footer);

        searchButton.Click += async (_, _) => await search();
        query.KeyDown += async (_, args) =>
        {
            if (args.Key == Avalonia.Input.Key.Enter)
            {
                args.Handled = true;
                await search();
            }
        };
        return layout;
    }

    private ListBoxItem CreateTitleResultItem(
        string title,
        string originalTitle,
        string date,
        string overview,
        string posterUrl
    )
    {
        Grid row = new()
        {
            ColumnDefinitions = new ColumnDefinitions("90,*"),
            ColumnSpacing = 8,
            Height = 120,
        };
        Image poster = new()
        {
            Width = 90,
            Height = 120,
            Stretch = Avalonia.Media.Stretch.Uniform,
        };
        row.Children.Add(poster);
        StackPanel details = new()
        {
            Spacing = 3,
            VerticalAlignment = VerticalAlignment.Center,
        };
        details.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
        });
        if (!string.IsNullOrWhiteSpace(originalTitle))
        {
            details.Children.Add(new TextBlock
            {
                Text = "(" + originalTitle + ")",
                FontSize = 12,
                FontStyle = Avalonia.Media.FontStyle.Italic,
            });
        }
        if (date.Length >= 4)
        {
            details.Children.Add(new TextBlock
            {
                Text = date[..4],
                FontSize = 12,
                Foreground = Avalonia.Media.Brushes.Gray,
            });
        }
        if (!string.IsNullOrWhiteSpace(overview))
        {
            details.Children.Add(new TextBlock
            {
                Text = overview,
                FontSize = 12,
                Foreground = Avalonia.Media.Brushes.Gray,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxHeight = 55,
            });
        }
        Grid.SetColumn(details, 1);
        row.Children.Add(details);
        ListBoxItem item = new()
        {
            Content = row,
            Padding = new Avalonia.Thickness(0),
            Margin = new Avalonia.Thickness(0, 0, 0, 10),
        };
        _ = LoadPosterAsync(poster, posterUrl);
        return item;
    }

    private async Task LoadPosterAsync(Image image, string posterUrl)
    {
        try
        {
            image.Source = await Model.LoadTitlePosterAsync(posterUrl);
        }
        catch (Exception exception)
        {
            Model.Status = exception.Message;
        }
    }

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
