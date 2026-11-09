using System.Xml.Linq;
using Avalonia.Headless;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;
using YaDIS3.Rendering;

namespace YaDIS3.Desktop.Tests;

[Collection("Avalonia UI")]
public sealed class WorkspaceScanTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "yadis-scan-" + Guid.NewGuid()
    );

    [Fact]
    public async Task OpenAsync_DossiersYaDis2_DecouvreLesNouvellesVideos()
    {
        string source = CreateWorkspace();
        File.WriteAllBytes(
            Path.Combine(source, "Nouveau.mkv"),
            []
        );
        using MainWindowViewModel model = new(
            Path.Combine(_root, "user.config")
        );
        await model.OpenAsync(Path.Combine(_root, "data"));
        MediaCard card = Assert.Single(model.Cards);
        Assert.Equal("Nouveau.mkv", card.FolderName);
        Assert.Equal(
            MediaProcessingState.Unprocessed,
            card.ProcessingState
        );
    }

    [Fact]
    public async Task RefreshConfiguredWorkspaceAsync_DisqueReconnecte_RetrouveLeStatutTraite()
    {
        string source = CreateWorkspace();
        string video = Path.Combine(source, "Film.mkv");
        File.WriteAllBytes(video, []);
        using MainWindowViewModel model = new(
            Path.Combine(_root, "user.config")
        );
        await model.OpenAsync(_root);
        string xml = Assert.Single(model.Cards).FilePath;
        XDocument document = XDocument.Load(xml);
        document
            .Root!.Element("item")!
            .SetElementValue("status", "0");
        document.Save(xml);
        File.Delete(video);
        await model.RefreshConfiguredWorkspaceAsync();
        Assert.Equal(
            MediaProcessingState.Missing,
            Assert.Single(model.Cards).ProcessingState
        );
        Assert.Equal(
            "0",
            XDocument
                .Load(xml)
                .Root!.Element("item")!
                .Element("status")!
                .Value
        );
        File.WriteAllBytes(video, []);
        await model.RefreshConfiguredWorkspaceAsync();
        Assert.Equal(
            MediaProcessingState.Checked,
            Assert.Single(model.Cards).ProcessingState
        );
    }

    [Fact]
    public async Task RestoreWorkspaceAsync_Redemarrage_DecouvreLesAjouts()
    {
        string source = CreateWorkspace();
        using (
            MainWindowViewModel previous = new(
                Path.Combine(_root, "user.config")
            )
        )
        {
            await previous.OpenAsync(_root);
            Assert.Empty(previous.Cards);
        }
        File.WriteAllBytes(
            Path.Combine(source, "Ajout depuis le Dune.mkv"),
            []
        );
        using MainWindowViewModel current = new(
            Path.Combine(_root, "user.config")
        );
        await current.RestoreWorkspaceAsync();
        Assert.Equal(
            "Ajout depuis le Dune.mkv",
            Assert.Single(current.Cards).FolderName
        );
    }

    [Fact]
    public async Task ProcessSelectedAsync_FilmRetraite_LePlaceEnFinDeCollectionDExport()
    {
        using HeadlessUnitTestSession session =
            HeadlessUnitTestSession.StartNew(typeof(ArtworkCatalogTests));
        await session.Dispatch(async () =>
        {
            await VerifyMovieOrderAfterTreatmentAsync();
            return 0;
        }, CancellationToken.None);
    }

    private async Task VerifyMovieOrderAfterTreatmentAsync()
    {
        string source = CreateWorkspace();
        WriteProcessedMovie(source, "Z.mkv");
        WriteProcessedMovie(source, "A.mkv");
        using MainWindowViewModel model = new(
            Path.Combine(_root, "user.config")
        );
        await model.OpenAsync(_root);
        string assets = Path.Combine(AppContext.BaseDirectory, "Assets");
        model.SelectedTemplate = DuneTemplate.Load(
            Path.Combine(assets, "Templates", "Origins"),
            Path.Combine(assets, "Images")
        );
        MediaCard[] exportOrder = model.Cards.ToArray();
        MediaCard[] visibleOrder = model.VisibleCards.ToArray();
        model.SelectedCard = exportOrder[0];

        await model.ProcessSelectedAsync();

        Assert.Equal(exportOrder.Reverse(), model.Cards);
        Assert.Equal(visibleOrder, model.VisibleCards);
    }

    private void WriteProcessedMovie(string source, string filename)
    {
        File.WriteAllBytes(Path.Combine(source, filename), []);
        string directory = Path.Combine(_root, "data", filename);
        Directory.CreateDirectory(directory);
        new XElement(
            "data",
            new XElement(
                "item",
                new XElement("filename", filename),
                new XElement("path", source),
                new XElement("dunepath", "smb://dune/Films"),
                new XElement("folder", "no"),
                new XElement("type", "0"),
                new XElement("status", "1")
            ),
            new XElement(
                "info",
                new XElement("title", filename),
                new XElement("sort_title", filename),
                new XElement("mediainfo", new XElement("format", "Matroska"))
            )
        ).Save(Path.Combine(directory, "data.xml"));
    }

    private string CreateWorkspace()
    {
        string source = Path.Combine(_root, "Films");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(Path.Combine(_root, "data"));
        new XDocument(
            new XElement(
                "folders",
                new XElement(
                    "folder",
                    new XElement("path", source),
                    new XElement("dunepath", "smb://dune/Films"),
                    new XElement("type", 0)
                )
            )
        ).Save(Path.Combine(_root, "folders.xml"));
        return source;
    }

    public void Dispose() =>
        Directory.Delete(_root, recursive: true);
}
