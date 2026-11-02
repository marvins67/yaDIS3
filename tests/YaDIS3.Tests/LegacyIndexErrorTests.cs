using System.Xml.Linq;
using Xunit;

namespace YaDIS3.Tests;

public sealed class LegacyIndexErrorTests
{
    [Fact]
    public void Export_InvalidIndexName_IdentifiesCardAndMetadataFile()
    {
        using ExportFixture fixture = new();
        string path = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "data.xml"
        );
        XDocument data = XDocument.Load(path);
        data.Root!.Element("info")!.Element("genre")!.Value =
            "Science/Fiction";
        data.Save(path);
        InvalidDataException exception =
            Assert.Throws<InvalidDataException>(() =>
                ExportFixture
                    .Exporter()
                    .Export(fixture.Cards(), fixture.Destination)
            );
        Assert.Contains(
            "L’été d’Adèle",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            path,
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "Science/Fiction",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.False(Directory.Exists(fixture.Destination));
    }

    [Fact]
    public void Export_PersonWithTrailingSpace_CompletesLikeYadis2()
    {
        using ExportFixture fixture = new();
        string path = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "data.xml"
        );
        XDocument data = XDocument.Load(path);
        data.Root!.Element("info")!.Element("actor")!.Value =
            "Burt Lancaster ";
        data.Root.Element("info")!.Element("director")!.Value =
            "Stanley Kubrick ";
        data.Save(path);
        var result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        Assert.Equal(2, result.MediaCount);
        Assert.False(
            Directory.Exists(
                Path.Combine(
                    result.Directory,
                    "00",
                    "05",
                    "L",
                    "Burt Lancaster "
                )
            )
        );
        Assert.Equal(
            "Burt Lancaster ",
            XDocument
                .Load(path)
                .Root!.Element("info")!
                .Element("actor")!
                .Value
        );
    }

    [Fact]
    public void Export_BlankIndexMetadata_ExportsCardWithoutBlankIndexes()
    {
        using ExportFixture fixture = new();
        string path = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "data.xml"
        );
        XDocument data = XDocument.Load(path);
        XElement info = data.Root!.Element("info")!;
        info.Element("title")!.Value =
            "Les Raisins du Désespoir";
        info.Element("genre")!.Value = " ";
        info.Element("country")!.Value = " ";
        info.Element("rating")!.Value = " ";
        data
            .Root.Element("mediainfo")!
            .Element("format")!
            .Value = " ";
        data.Save(path);
        byte[] original = File.ReadAllBytes(path);
        var result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        Assert.Equal(2, result.MediaCount);
        Assert.True(
            File.Exists(
                Path.Combine(
                    result.Directory,
                    "00",
                    "99",
                    ExportFixture.MovieName,
                    "dune_folder.txt"
                )
            )
        );
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.DoesNotContain(
            Directory.EnumerateDirectories(
                result.Directory,
                "*",
                SearchOption.AllDirectories
            ),
            directory =>
                string.IsNullOrWhiteSpace(
                    Path.GetFileName(directory)
                )
        );
    }
}
