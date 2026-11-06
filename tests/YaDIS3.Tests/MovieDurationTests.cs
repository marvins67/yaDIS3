using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class MovieDurationTests
{
    [Fact]
    public async Task PrepareAsync_BlocMediaInfoVide_RelanceAnalyseEtDuree()
    {
        using MovieArtworkFixture fixture = new();
        XDocument xml = XDocument.Load(fixture.Card.FilePath);
        xml.Root!.Element("info")!.Add(LegacyMediaInfo.Empty());
        xml.Save(fixture.Card.FilePath);

        using WorkspaceRepository repository = new();
        using MediaCard card = repository.Open(fixture.Root)
            .Cards.Single(item => !item.IsSeries);
        DurationReader reader = new();
        TreatmentPreparation preparation = new(
            new ExportFileSystem(), new TreatmentInputs(), reader
        );

        await preparation.PrepareAsync(
            card, TreatmentMode.Normal, CancellationToken.None
        );

        Assert.Equal(1, reader.Reads);
        Assert.Equal("120", card.Runtime);
    }

    [Theory]
    [InlineData(TreatmentMode.Normal, false, false, "120", 1)]
    [InlineData(TreatmentMode.Normal, true, false, "151", 0)]
    [InlineData(TreatmentMode.ReloadTechnicalInformation, true, false, "120", 1)]
    [InlineData(TreatmentMode.Normal, false, true, "151", 1)]
    public async Task PrepareAsync_Duree_RespecteAnalyseEtRetouches(
        TreatmentMode mode, bool existing, bool multiple, string expected, int reads
    )
    {
        using MovieArtworkFixture fixture = new();
        XDocument xml = XDocument.Load(fixture.Card.FilePath);
        if (existing)
        {
            xml.Root!.Element("info")!.Add(new XElement(
                "mediainfo",
                new XElement("duration", "1"),
                new XElement("format", "Matroska"),
                new XElement("video", new XElement("codec", "AVC"))
            ));
        }
        xml.Save(fixture.Card.FilePath);
        if (multiple)
        {
            string source = Path.Combine(fixture.Root, ExportFixture.MovieName);
            File.Delete(source);
            Directory.CreateDirectory(source);
            File.WriteAllBytes(Path.Combine(source, "a.mkv"), []);
            File.WriteAllBytes(Path.Combine(source, "b.mkv"), []);
        }
        using WorkspaceRepository repository = new();
        using MediaCard card = repository.Open(fixture.Root).Cards.Single(item => !item.IsSeries);
        DurationReader reader = new();
        TreatmentPreparation preparation = new(new ExportFileSystem(), new TreatmentInputs(), reader);
        await preparation.PrepareAsync(card, mode, CancellationToken.None);
        Assert.Equal(expected, card.Runtime);
        Assert.Equal(reads, reader.Reads);
    }
}
