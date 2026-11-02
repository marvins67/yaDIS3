using System.Text;
using System.Xml.Linq;
using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyCatalogOptionTests
{
    [Fact]
    public void Export_DisabledCategories_OmitsTheirFolders()
    {
        using ExportFixture fixture = new();
        DuneExporter exporter = new(
            ExportFixture.Template(),
            new RenderOptions(),
            [],
            catalogOptions: new LegacyCatalogOptions
            {
                Categories = "0000000000",
            }
        );
        ExportResult result = exporter.Export(
            fixture.Cards(),
            fixture.Destination
        );
        Assert.True(
            Directory.Exists(
                Path.Combine(result.Directory, "00", "00")
            )
        );
        Assert.False(
            Directory.Exists(
                Path.Combine(result.Directory, "00", "02")
            )
        );
        Assert.False(
            Directory.Exists(
                Path.Combine(result.Directory, "00", "03")
            )
        );
        Assert.False(
            Directory.Exists(
                Path.Combine(result.Directory, "00", "04")
            )
        );
    }

    [Fact]
    public void Export_RecentSorting_PreservesLegacyDayPrefix()
    {
        using ExportFixture fixture = new();
        DuneExporter exporter = new(
            ExportFixture.Template(),
            new RenderOptions(),
            [],
            catalogOptions: new LegacyCatalogOptions
            {
                SortRecentByDate = true,
            },
            timeProvider: new FixedTimeProvider()
        );
        ExportResult result = exporter.Export(
            fixture.Cards(),
            fixture.Destination
        );
        string recent = File.ReadAllText(
            Path.Combine(
                result.Directory,
                "00",
                "02",
                "dune_folder.txt"
            )
        );
        Assert.Contains(
            "item.0.caption=001_Été d’Adèle",
            recent,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Export_Saga_ReplacesMoviesInMainAndAlphabetListings()
    {
        using ExportFixture fixture = new();
        new XElement(
            "groups",
            new XElement(
                "sagas",
                new XElement(
                    "saga",
                    new XElement("name", "Saga d’Adèle"),
                    new XElement("sort_name", "Saga d’Adèle"),
                    new XElement(
                        "poster",
                        ExportFixture.MovieName
                    ),
                    new XElement(
                        "fanart",
                        ExportFixture.MovieName
                    ),
                    new XElement(
                        "movie",
                        ExportFixture.MovieName
                    )
                )
            )
        ).Save(Path.Combine(fixture.Root, "groups.xml"));
        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        string main = File.ReadAllText(
            Path.Combine(
                result.Directory,
                "00",
                "00",
                "dune_folder.txt"
            )
        );
        Assert.Contains(
            "item.0.media_url=../98/Saga d’Adèle",
            main,
            StringComparison.Ordinal
        );
        string alphabet = File.ReadAllText(
            Path.Combine(
                result.Directory,
                "00",
                "03",
                "S",
                "dune_folder.txt"
            )
        );
        Assert.Contains(
            "item.0.media_url=../../98/Saga d’Adèle",
            alphabet,
            StringComparison.Ordinal
        );
        string saga = File.ReadAllText(
            Path.Combine(
                result.Directory,
                "00",
                "98",
                "Saga d’Adèle",
                "dune_folder.txt"
            )
        );
        Assert.Contains(
            "item.0.media_url=../../99/"
                + ExportFixture.MovieName,
            saga,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "\r\nicon_path=",
            saga,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Export_MultipartMovie_PreservesSourcePlaylistAndPlaybackUrl()
    {
        using ExportFixture fixture = new();
        const string FolderName = "Le Retour Du Roi (2003)";
        string source = Path.Combine(
            fixture.Root,
            "Films",
            FolderName
        );
        Directory.CreateDirectory(source);
        File.WriteAllBytes(
            Path.Combine(source, "Le Retour Du Roi CD1.mkv"),
            []
        );
        File.WriteAllBytes(
            Path.Combine(source, "Le Retour Du Roi CD2.mkv"),
            []
        );
        string dataPath = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "data.xml"
        );
        XDocument data = XDocument.Load(dataPath);
        XElement header = data.Root!.Element("item")!;
        header.Element("filename")!.Value = FolderName;
        header.Element("folder")!.Value = "yes";
        header.Element("path")!.Value = Path.Combine(
            fixture.Root,
            "Films"
        );
        data.Save(dataPath);
        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        string movie = File.ReadAllText(
            Path.Combine(
                result.Directory,
                "00",
                "99",
                FolderName,
                "dune_folder.txt"
            )
        );
        Assert.Contains(
            "item.0.media_url=smb://nas/Films/"
                + FolderName
                + "/playlist.m3u",
            movie,
            StringComparison.Ordinal
        );
        Assert.Equal(
            "Le Retour Du Roi CD1.mkv\r\nLe Retour Du Roi CD2.mkv\r\n",
            File.ReadAllText(
                Path.Combine(source, "playlist.m3u"),
                Encoding.UTF8
            )
        );
    }
}
