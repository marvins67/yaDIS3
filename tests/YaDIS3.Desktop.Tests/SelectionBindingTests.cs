using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Logging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Desktop.Localization;
using YaDIS3.Desktop.ViewModels;
using YaDIS3.Desktop.Views;

namespace YaDIS3.Desktop.Tests;

[Collection("Avalonia UI")]
public sealed class SelectionBindingTests
{
    private static readonly string[] s_movieTabs =
    [
        "Film",
        "Média",
        "Jaquette",
        "Fanart",
    ];
    private static readonly string[] s_seriesTabs =
    [
        "Série TV",
        "Bannière",
        "Fanart",
        "Épisodes",
    ];

    [Fact]
    public void MainWindow_SelectionChanges_ClearsEditorsWithoutBindingErrors()
    {
        AppBuilder
            .Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .SetupWithoutStarting();
        ILogSink? previous = Logger.Sink;
        BindingLog log = new();
        Logger.Sink = log;
        string directory = Path.Combine(
            Path.GetTempPath(),
            "yadis3-bindings-" + Guid.NewGuid().ToString("N")
        );
        try
        {
            VerifySelections(directory, log);
        }
        finally
        {
            Logger.Sink = previous;
            Directory.Delete(directory, true);
        }
    }

    private static void VerifySelections(
        string directory,
        BindingLog log
    )
    {
        AddCard(directory, "Série", true);
        AddCard(directory, "(120) Coco (2017).mkv", false, "Film");
        using WorkspaceRepository repository = new();
        Workspace workspace = repository.Open(directory);
        using MainWindowViewModel model = new(
            Path.Combine(directory, "user.config")
        );
        MainWindow window = new() { DataContext = model };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            VerifyMissingXmlResource();
            VerifyXmlResources(window);
            VerifyFirstLaunch(window, model);
            VerifyMainLayout(window);
            VerifyStateMenu(window, model, workspace.Cards[0]);
            VerifySettingsDialog(window, model);
            VerifySagaArtworkGallery(directory);
            TextBox title = window.FindControl<TextBox>(
                "TitleEditor"
            )!;
            Assert.True(string.IsNullOrEmpty(title.Text));
            MediaCard series = workspace.Cards.Single(card =>
                card.IsSeries
            );
            model.SelectedCard = series;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(
                s_seriesTabs,
                window
                    .FindControl<TabControl>("EditorTabs")!
                    .Items.OfType<TabItem>()
                    .Where(tab => tab.IsVisible)
                    .Select(tab => tab.Header?.ToString())
                    .ToArray()
            );
            VisitTabs(window);
            Assert.Equal("Série", title.Text);
            VerifyEpisodeEditors(window, model);
            title.Text = "Série modifiée";
            Assert.Equal("Série modifiée", series.Title);
            VerifyEmptySelection(window, model);
            MediaCard movie = workspace.Cards.Single(card => !card.IsSeries);
            model.SelectedCard = movie;
            Dispatcher.UIThread.RunJobs();
            VerifyLibraryFileName(window, movie);
            Assert.Equal(
                "Film",
                window
                    .FindControl<TextBox>("MovieTitleEditor")!
                    .Text
            );
            Assert.Null(model.SelectedEpisode);
            model.SelectedCard = series;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Série modifiée", title.Text);
            Assert.True(
                log.Errors.Count == 0,
                string.Join(Environment.NewLine, log.Errors)
            );
        }
        finally
        {
            window.Close();
        }
    }

    private static void VerifySagaArtworkGallery(string workspace)
    {
        string member = "SagaTest.mkv";
        string data = Path.Combine(workspace, "data", member);
        Directory.CreateDirectory(data);
        byte[] image = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwC" +
            "AAAAC0lEQVR42mP8/x8AAwMCAO+/j1sAAAAASUVORK5CYII="
        );
        File.WriteAllBytes(Path.Combine(data, "poster.jpg"), image);
        File.WriteAllBytes(Path.Combine(data, "fanart.jpg"), image);
        SagaContextDraft draft = new(
            "Saga de test",
            "Saga de test",
            member,
            member,
            [member]
        );
        SagaEditorWindow editor = new(draft, workspace);
        editor.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            Grid root = Assert.IsType<Grid>(editor.Content);
            Grid gallery = Assert.Single(root.Children.OfType<Grid>());
            ListBox members = Assert.Single(
                gallery.Children.OfType<ListBox>()
            );
            Assert.Equal(
                member,
                Assert.Single(members.ItemsSource!.OfType<string>())
            );
            Button[] buttons = gallery.Children.OfType<Button>()
                .ToArray();
            Assert.Equal(2, buttons.Length);
            Assert.All(buttons, button =>
            {
                Image picture = Assert.IsType<Image>(button.Content);
                Assert.NotNull(picture.Source);
            });
        }
        finally
        {
            editor.Close();
        }
    }

    private static void VerifyMissingXmlResource()
    {
        string folder = Path.Combine(
            Path.GetTempPath(),
            "yadis3-missing-text-" + Guid.NewGuid()
        );
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(
                Path.Combine(folder, "fr.xml"),
                "<strings><string id='0'>fr</string><string id='1'>Français</string></strings>",
                Encoding.UTF8
            );
            LegacyLanguageCatalog catalog =
                new LegacyLanguageLoader(
                    new ExportFileSystem()
                ).Load(folder, "fr", new CultureInfo("fr-FR"));
            ResourceDictionary resources = new();
            LanguageResources.Apply(resources, catalog);
            Assert.Equal(
                "<label not found>",
                resources["Text.10"]
            );
            Assert.Equal(
                "<label not found>:",
                resources["Text.22.Colon"]
            );
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    private static void VerifyXmlResources(MainWindow window)
    {
        LegacyLanguageCatalog english =
            LegacyLanguageCatalog.Load("en");
        LegacyLanguageCatalog french =
            LegacyLanguageCatalog.Load("fr");
        try
        {
            LanguageResources.Apply(
                Application.Current!.Resources,
                english
            );
            Dispatcher.UIThread.RunJobs();
            Button configure = window.FindControl<Button>(
                "ConfigureButton"
            )!;
            Assert.Equal(
                english["10"],
                configure
                    .GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Single(item => item.Text == english["10"])
                    .Text
            );
            Assert.Equal(
                english["31"],
                Application.Current.Resources["Text.31"]
            );
        }
        finally
        {
            LanguageResources.Apply(
                Application.Current!.Resources,
                french
            );
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void VerifyFirstLaunch(
        MainWindow window,
        MainWindowViewModel model
    )
    {
        Assert.Equal("Films", model.MediaFilterLabel);
        Assert.Equal("Tous", model.StateFilterLabel);
        Assert.Equal("", model.VisibleCountLabel);
        Assert.Equal("", model.Status);
        Assert.False(model.CanExport);
        Assert.False(
            window.FindControl<Button>("ExportButton")!.IsEnabled
        );
        Assert.False(
            window
                .FindControl<TabControl>("EditorTabs")!
                .IsEnabled
        );
        Assert.True(
            window.FindControl<Image>("EmptyPreview")!.IsVisible
        );
        Assert.Equal(
            s_movieTabs,
            window
                .FindControl<TabControl>("EditorTabs")!
                .Items.OfType<TabItem>()
                .Where(tab => tab.IsVisible)
                .Select(tab => tab.Header?.ToString())
        );
        model.ToggleMediaFilterCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Séries TV", model.MediaFilterLabel);
        Assert.False(
            window.FindControl<TabItem>("MediaTab")!.IsVisible
        );
        model.SelectMoviesCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
    }

    private static void VerifyLibraryFileName(
        MainWindow window,
        MediaCard movie
    )
    {
        ListBox library = window.FindControl<ListBox>("LibraryList")!;
        Grid row = Assert.IsType<Grid>(library.ItemTemplate!.Build(movie));
        row.DataContext = movie;
        Dispatcher.UIThread.RunJobs();

        const string FileName = "(120) Coco (2017).mkv";
        Assert.Equal(FileName, Assert.Single(row.Children.OfType<TextBlock>()).Text);
        Assert.Equal(FileName, ToolTip.GetTip(row));
    }

    private static void VerifyMainLayout(MainWindow window)
    {
        ListBox library = window.FindControl<ListBox>(
            "LibraryList"
        )!;
        Border preview = window.FindControl<Border>(
            "PreviewPanel"
        )!;
        Assert.Equal(254, library.Bounds.Width);
        Assert.Equal(
            49,
            library.TranslatePoint(default, window)!.Value.Y
        );
        Assert.Equal(
            270,
            preview.TranslatePoint(default, window)!.Value.X
        );
        Assert.Equal(
            library.Bounds.Height,
            preview.Bounds.Height
        );
        Assert.Equal(
            242,
            window
                .FindControl<TabControl>("EditorTabs")!
                .Bounds.Height
        );
        Grid information = window.FindControl<Grid>(
            "InformationPanel"
        )!;
        Assert.Equal(
            406,
            information.ColumnDefinitions[0].ActualWidth
        );
        window.Width = 943;
        window.Height = 693;
        Dispatcher.UIThread.RunJobs();
        Assert.True(preview.Bounds.Width > 600);
        Assert.True(library.Bounds.Height > 300);
        window.Width = 1075;
        window.Height = 797;
        Dispatcher.UIThread.RunJobs();
    }

    private static void VerifyStateMenu(
        MainWindow window,
        MainWindowViewModel model,
        MediaCard card
    )
    {
        model.Cards.Add(card);
        model.MediaFilterIndex = 0;
        SplitButton button = window.FindControl<SplitButton>(
            "StateFilterButton"
        )!;
        MenuFlyout menu = Assert.IsType<MenuFlyout>(
            button.Flyout
        );
        menu.ShowAt(button);
        Dispatcher.UIThread.RunJobs();
        MenuItem[] entries = menu
            .Items.OfType<MenuItem>()
            .Take(5)
            .ToArray();
        Assert.Equal(
            model.StateFilters.Select(item => item.Label),
            entries.Select(item => item.Header)
        );
        Assert.All(
            entries,
            item => Assert.NotNull(item.Command)
        );
        Assert.All(
            entries.Skip(1),
            item => Assert.IsType<Image>(item.Icon)
        );
        MenuItem genres = Assert.IsType<MenuItem>(menu.Items[8]);
        Assert.True(genres.IsVisible);
        genres.IsSubMenuOpen = true;
        Dispatcher.UIThread.RunJobs();
        MenuItem genre = Assert.IsType<MenuItem>(
            genres.ContainerFromIndex(0)
        );
        Assert.Equal("Drame", genre.Header);
        Assert.NotNull(genre.Command);
        genres.IsSubMenuOpen = false;
        menu.Hide();
        model.Cards.Clear();
        model.StateFilters[0].SelectCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
    }

    private static void VerifySettingsDialog(
        MainWindow window,
        MainWindowViewModel model
    )
    {
        Button configure = window.FindControl<Button>(
            "ConfigureButton"
        )!;
        configure.RaiseEvent(
            new RoutedEventArgs(Button.ClickEvent)
        );
        Dispatcher.UIThread.RunJobs();
        ConfigurationWindow dialog =
            Assert.IsType<ConfigurationWindow>(
                Assert.Single(window.OwnedWindows)
            );
        ConfigurationViewModel draft =
            Assert.IsType<ConfigurationViewModel>(
                dialog.DataContext
            );
        Assert.Equal(574, dialog.Width);
        Assert.Equal(542, dialog.Height);
        Assert.False(dialog.CanResize);
        TabControl tabs = dialog.FindControl<TabControl>(
            "ConfigurationTabs"
        )!;
        string[] names = ["Général", "Dossiers", "Interface"];
        Assert.Equal(
            names,
            tabs.Items.OfType<TabItem>()
                .Select(tab => tab.Header?.ToString())
        );
        VerifyGeneralControls(dialog, draft);
        foreach (TabItem tab in tabs.Items.OfType<TabItem>())
        {
            tabs.SelectedItem = tab;
            Dispatcher.UIThread.RunJobs();
        }
        draft.BasicMode = true;
        Assert.False(model.CanExport);
        dialog.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.False(model.CanExport);
        Assert.False(model.IsBusy);
        configure.RaiseEvent(
            new RoutedEventArgs(Button.ClickEvent)
        );
        Dispatcher.UIThread.RunJobs();
        dialog = Assert.IsType<ConfigurationWindow>(
            Assert.Single(window.OwnedWindows)
        );
        Assert.False(
            (
                (ConfigurationViewModel)dialog.DataContext!
            ).BasicMode
        );
        dialog.Close();
        Dispatcher.UIThread.RunJobs();
    }

    private static void VerifyGeneralControls(
        ConfigurationWindow dialog,
        ConfigurationViewModel draft
    )
    {
        ComboBox database = dialog.FindControl<ComboBox>(
            "MovieDatabaseChoice"
        )!;
        Assert.Equal(
            "TMDb",
            Assert
                .IsType<ComboBoxItem>(
                    Assert.Single(database.Items)
                )
                .Content
        );
        Assert.Equal(0, database.SelectedIndex);
        CheckBox auto = dialog.FindControl<CheckBox>(
            "AutoSelectChoice"
        )!;
        ComboBox selection = dialog.FindControl<ComboBox>(
            "AutoSelectionChoice"
        )!;
        CheckBox ignore = dialog.FindControl<CheckBox>(
            "IgnoreArticlesChoice"
        )!;
        TextBox articles = dialog.FindControl<TextBox>(
            "ArticlesChoice"
        )!;
        auto.IsChecked = true;
        ignore.IsChecked = true;
        articles.Text = "le, la, les";
        Assert.True(draft.AutoSelect);
        Assert.True(draft.IgnoreArticles);
        Assert.True(selection.IsVisible);
        Assert.True(articles.IsVisible);
        Assert.Equal(
            "le,la,les",
            draft.Snapshot().Settings.Articles
        );
        draft.BasicMode = true;
        Dispatcher.UIThread.RunJobs();
        Assert.False(
            dialog
                .FindControl<Canvas>("NormalGeneralOptions")!
                .IsVisible
        );
        Assert.Equal(118, draft.DatabaseTop);
        Assert.True(database.IsVisible);
        Assert.True(draft.AutoSelect);
        Assert.True(draft.IgnoreArticles);
        draft.BasicMode = false;
        auto.IsChecked = false;
        ignore.IsChecked = false;
        Assert.False(selection.IsVisible);
        Assert.False(articles.IsVisible);
        Assert.Equal(231, draft.DatabaseTop);
    }

    private static void VerifyEpisodeEditors(
        MainWindow window,
        MainWindowViewModel model
    )
    {
        TextBox title = window.FindControl<TextBox>(
            "EpisodeTitleEditor"
        )!;
        Assert.Equal("Épisode initial", title.Text);
        title.Text = "Épisode modifié";
        Assert.Equal(
            "Épisode modifié",
            model.SelectedEpisode!.Title
        );
        model.SelectedEpisode = null;
        Dispatcher.UIThread.RunJobs();
        Assert.True(string.IsNullOrEmpty(title.Text));
        model.SelectedEpisode = model.SelectedCard!.Episodes[0];
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Épisode modifié", title.Text);
    }

    private static void VerifyEmptySelection(
        MainWindow window,
        MainWindowViewModel model
    )
    {
        model.SelectedCard = null;
        Dispatcher.UIThread.RunJobs();
        Assert.True(
            string.IsNullOrEmpty(
                window.FindControl<TextBox>("TitleEditor")!.Text
            )
        );
        Assert.True(
            string.IsNullOrEmpty(
                window
                    .FindControl<TextBox>("EpisodeTitleEditor")!
                    .Text
            )
        );
        Assert.Null(
            window
                .FindControl<ComboBox>("EpisodeChoice")!
                .ItemsSource
        );
        Assert.False(model.HasCard);
        Assert.False(model.HasEpisode);
    }

    private static void VisitTabs(MainWindow window)
    {
        TabControl tabs = window.FindControl<TabControl>(
            "EditorTabs"
        )!;
        foreach (TabItem tab in tabs.Items.OfType<TabItem>())
        {
            tabs.SelectedItem = tab;
            Dispatcher.UIThread.RunJobs();
        }
        tabs.SelectedIndex = 0;
    }

    private static void AddCard(
        string directory,
        string name,
        bool series,
        string? title = null
    )
    {
        string path = Path.Combine(directory, "data", name);
        Directory.CreateDirectory(path);
        XElement info = new(
            "info",
            new XElement("title", title ?? name),
            new XElement("genre", "Drame")
        );
        if (series)
        {
            info.Add(
                new XElement(
                    "episode",
                    new XAttribute("season", "1"),
                    new XAttribute("episode", "1"),
                    new XAttribute("key", "01_001_001"),
                    new XElement("title", "Épisode initial")
                )
            );
        }
        new XDocument(
            new XElement(
                "data",
                new XElement(
                    "item",
                    new XElement("filename", name),
                    new XElement("type", series ? "1" : "0"),
                    new XElement("status", "1")
                ),
                info
            )
        ).Save(Path.Combine(path, "data.xml"));
    }
}
