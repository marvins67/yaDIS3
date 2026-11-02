using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Logging;
using Avalonia.Threading;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;
using YaDIS3.Desktop.Views;

namespace YaDIS3.Desktop.Tests;

public sealed class SelectionBindingTests
{
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
            TextBox title = window.FindControl<TextBox>(
                "TitleEditor"
            )!;
            Assert.True(string.IsNullOrEmpty(title.Text));
            MediaCard series = workspace.Cards.Single(card =>
                card.IsSeries
            );
            model.SelectedCard = series;
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
            Assert.Empty(log.Errors);
        }
        finally
        {
            window.Close();
        }
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
