using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Logging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;
using YaDIS3.Desktop.Views;

namespace YaDIS3.Desktop.Tests;

[Collection("Avalonia UI")]
public sealed class ArtworkCatalogTests
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(
            new AvaloniaHeadlessPlatformOptions()
        );

    [Fact]
    public async Task LoadArtworkChoicesAsync_UneVignetteEnErreur_ChargeLesAutresEtSelectionneOriginal()
    {
        using HeadlessUnitTestSession session =
            HeadlessUnitTestSession.StartNew(typeof(ArtworkCatalogTests));
        await session.Dispatch(async () =>
        {
            await VerifyDownloadsAsync();
            return 0;
        }, CancellationToken.None);
    }

    private static async Task VerifyDownloadsAsync()
    {
        using ArtworkHandler handler = new() { FailFirstThumbnail = true };
        using ArtworkModelFixture fixture = new(handler);
        MainWindowViewModel model = fixture.Model;
        Assert.Single(model.PosterChoices);
        Assert.Equal(2, model.FanartChoices.Count);
        Assert.Empty(handler.Requests);
        await model.LoadArtworkChoicesAsync(ArtworkKind.Fanart);
        Assert.Null(model.FanartChoices[0].Thumbnail);
        Assert.NotNull(model.FanartChoices[1].Thumbnail);
        Assert.Contains("404", model.FanartChoices[0].Hint,
            StringComparison.Ordinal);
        byte[] banner = [1, 2, 3];
        File.WriteAllBytes(fixture.Card.BannerPath, banner);
        await model.SelectArtworkAsync(model.FanartChoices[1].Reference);
        Assert.True(model.FanartChoices[1].IsSelected);
        Assert.False(model.FanartChoices[0].IsSelected);
        Assert.Contains(handler.Requests, uri =>
            uri.AbsolutePath == "/t/p/original/second.jpg");
        Assert.Equal(ArtworkHandler.Image, File.ReadAllBytes(fixture.Card.FanartPath));
        Assert.Equal(banner, File.ReadAllBytes(fixture.Card.BannerPath));
        Assert.True(fixture.Card.IsDirty);
        model.ImportImage(ArtworkKind.Fanart, fixture.Card.FanartPath);
        Assert.All(model.FanartChoices, choice => Assert.False(choice.IsSelected));
        model.SelectedCard = null;
        Assert.Empty(model.PosterChoices);
        Assert.Empty(model.FanartChoices);
    }

    [Fact]
    public async Task MainWindow_Catalogue_LieLesVignettesEtCommandesSansErreur()
    {
        using HeadlessUnitTestSession session =
            HeadlessUnitTestSession.StartNew(typeof(ArtworkCatalogTests));
        await session.Dispatch(VerifyBindings, CancellationToken.None);
    }

    private static void VerifyBindings()
    {
        ILogSink? previous = Logger.Sink;
        BindingLog log = new();
        Logger.Sink = log;
        using ArtworkHandler handler = new();
        using ArtworkModelFixture fixture = new(handler);
        MainWindow window = new() { DataContext = fixture.Model };
        window.Show();
        try
        {
            TabControl tabs = window.FindControl<TabControl>("EditorTabs")!;
            tabs.SelectedItem = window.FindControl<TabItem>("PosterTab");
            Dispatcher.UIThread.RunJobs();
            ItemsControl posters = window.FindControl<ItemsControl>("PosterCatalog")!;
            Assert.Same(fixture.Model.PosterChoices, posters.ItemsSource);
            Button poster = posters.GetVisualDescendants().OfType<Button>().Single();
            Assert.Same(fixture.Model.PosterChoices[0].SelectCommand, poster.Command);
            ScrollViewer viewport = posters.GetVisualAncestors()
                .OfType<ScrollViewer>().First();
            Assert.InRange(poster.Bounds.Height, 168, viewport.Bounds.Height);
            tabs.SelectedItem = window.FindControl<TabItem>("FanartTab");
            Dispatcher.UIThread.RunJobs();
            ItemsControl fanarts = window.FindControl<ItemsControl>("FanartCatalog")!;
            Assert.Same(fixture.Model.FanartChoices, fanarts.ItemsSource);
            Assert.Equal(2, fanarts.GetVisualDescendants().OfType<Button>().Count());
            Assert.True(log.Errors.Count == 0, string.Join("\n", log.Errors));
        }
        finally
        {
            Logger.Sink = previous;
            window.DataContext = null;
            window.Close();
        }
    }
}
