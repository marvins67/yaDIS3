using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class SeriesTreatmentMetadataTests
{
    [Theory]
    [InlineData(TreatmentMode.Normal, "Titre corrigé", "Note corrigée", "Épisode corrigé")]
    [InlineData(TreatmentMode.ReloadInformation, "Titre fournisseur", "7", "Épisode fournisseur")]
    public async Task PrepareAsync_Mode_ConserveOuReconstruitLesChamps(
        TreatmentMode mode, string title, string rating, string episodeTitle
    )
    {
        using ExportFixture fixture = new();
        string directory = Path.Combine(fixture.Root, ExportFixture.SeriesName);
        Directory.CreateDirectory(directory);
        string video = Path.Combine(directory, "S01E01.mkv");
        File.WriteAllBytes(video, []);
        File.WriteAllBytes(Path.Combine(directory, "S01E02.mkv"), []);
        string file = Path.Combine(fixture.Root, "data", ExportFixture.SeriesName, "data.xml");
        XDocument xml = XDocument.Load(file);
        xml.Root!.SetAttributeValue("tvdb_order", "dvd");
        xml.Root.Element("item")!.SetElementValue("path", fixture.Root);
        XElement info = xml.Root.Element("info")!;
        info.SetElementValue("id", "42");
        info.SetElementValue("title", "Titre corrigé");
        info.SetElementValue("rating", "Note corrigée");
        info.Elements("episode").Remove();
        info.Add(new XElement("episode", new XAttribute("season", 1), new XAttribute("episode", 1),
            new XElement("title", "Épisode corrigé"), new XElement("aired", "2020-01-02"),
            new XElement("rating", "Note épisode corrigée"), new XElement("director", "Réalisateur corrigé"),
            new XElement("file", video), new XElement("mediainfo", new XElement("format", "Retouche technique"))));
        xml.Save(file);
        using WorkspaceRepository repository = new();
        using MediaCard card = repository.Open(fixture.Root).Cards.Single(item => item.IsSeries);
        byte[] chosenImage = [1, 2, 3];
        File.WriteAllBytes(card.BannerPath, chosenImage);
        TreatmentPreparation preparation = new(new ExportFileSystem(), new SeriesTreatmentMetadata(), new TreatmentInputs());
        await preparation.PrepareAsync(card, mode, CancellationToken.None);
        Assert.Equal(title, card.Title);
        Assert.Equal("dvd", card.Order);
        XElement result = card.Snapshot().Document.Root!.Element("info")!;
        Assert.Equal(rating, result.Element("rating")!.Value);
        XElement first = result.Elements("episode").Single(item => (string?)item.Attribute("episode") == "1");
        Assert.Equal(episodeTitle, first.Element("title")!.Value);
        Assert.Equal(mode == TreatmentMode.Normal ? "Réalisateur corrigé" : "Réalisateur", first.Element("director")!.Value);
        XElement second = result.Elements("episode").Single(item => (string?)item.Attribute("episode") == "2");
        Assert.Equal("9", second.Element("rating")!.Value);
        Assert.Equal("Autre réalisateur", second.Element("director")!.Value);
        Assert.Equal(chosenImage, File.ReadAllBytes(card.BannerPath));
    }
}
