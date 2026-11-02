using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Logging;
using Avalonia.Threading;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;
using YaDIS3.Desktop.Views;

namespace YaDIS3.Desktop.Tests;

public sealed class SelectionBindingTests
{
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
        AddCard(directory, "Film", false);
        using WorkspaceRepository repository = new();
        Workspace workspace = repository.Open(directory);
        using MainWindowViewModel model = new();
        MainWindow window = new() { DataContext = model };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            VerifyMainLayout(window);
            VerifySettingsDialog(window, model);
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
            model.SelectedCard = workspace.Cards.Single(card =>
                !card.IsSeries
            );
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Film", title.Text);
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
            51,
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

    private static void VerifySettingsDialog(
        MainWindow window,
        MainWindowViewModel model
    )
    {
        Button configure = window.FindControl<Button>(
            "ConfigureButton"
        )!;
        Flyout flyout = (Flyout)configure.Flyout!;
        Button settings = ((StackPanel)flyout.Content!)
            .Children.OfType<Button>()
            .Single(button =>
                button.Content?.ToString()
                == "Templates et export Dune…"
            );
        TabItem tab = window.FindControl<TabItem>(
            "DuneSettingsTab"
        )!;
        object? original = tab.Content;
        settings.RaiseEvent(
            new RoutedEventArgs(Button.ClickEvent)
        );
        Dispatcher.UIThread.RunJobs();
        Window dialog = Assert.Single(window.OwnedWindows);
        Assert.Same(model, dialog.DataContext);
        Assert.Same(original, dialog.Content);
        Assert.False(tab.IsVisible);
        dialog.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.Same(original, tab.Content);
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
        bool series
    )
    {
        string path = Path.Combine(directory, "data", name);
        Directory.CreateDirectory(path);
        XElement info = new("info", new XElement("title", name));
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
