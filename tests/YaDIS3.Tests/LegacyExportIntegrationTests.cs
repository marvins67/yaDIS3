using System.Text;
using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyExportIntegrationTests
{
    [Theory]
    [InlineData("Origins")]
    [InlineData("GLASS Revisited")]
    [InlineData("GLASS Revisited (TV Posters)")]
    public void Export_RealisticMediaNames_WritesLegacyTree(
        string template
    )
    {
        using ExportFixture fixture = new();
        fixture.ProcessIndexes(template);
        ExportResult result = ExportFixture
            .Exporter(template)
            .Export(fixture.Cards(), fixture.Destination);
        Assert.Equal(fixture.Destination, result.Directory);
        Assert.False(
            File.Exists(
                Path.Combine(
                    result.Directory,
                    "yadis3-export.json"
                )
            )
        );
        string movie = Path.Combine(
            result.Directory,
            "00",
            "99",
            ExportFixture.MovieName
        );
        string series = Path.Combine(
            result.Directory,
            "01",
            ExportFixture.SeriesName
        );
        Assert.True(
            File.Exists(Path.Combine(movie, "dune_folder.txt"))
        );
        Assert.True(
            File.Exists(
                Path.Combine(
                    series,
                    "01",
                    "01_000_001",
                    "dune_folder.txt"
                )
            )
        );
        Assert.False(
            Directory.Exists(Path.Combine(series, "00"))
        );
        Assert.Empty(
            Directory.GetFiles(
                result.Directory,
                "preview.jpg",
                SearchOption.AllDirectories
            )
        );
        Assert.Empty(
            Directory.GetFiles(
                result.Directory,
                "nav-*.aai",
                SearchOption.AllDirectories
            )
        );
        Assert.True(
            File.Exists(
                Path.Combine(
                    result.Directory,
                    "00",
                    "09",
                    "2020",
                    "dune_folder.txt"
                )
            )
        );
        Assert.True(
            File.Exists(
                Path.Combine(
                    result.Directory,
                    "00",
                    "11",
                    "M2TS",
                    "icon_sel.aai"
                )
            )
        );
        string season = File.ReadAllText(
            Path.Combine(series, "01", "dune_folder.txt")
        );
        Assert.DoesNotContain(
            "item.0.",
            season,
            StringComparison.Ordinal
        );
        string episode = File.ReadAllText(
            Path.Combine(
                series,
                "01",
                "01_000_001",
                "dune_folder.txt"
            )
        );
        Assert.Equal(
            LegacyDuneEncoding.Header
                + "\r\nicon_path=icon.aai\r\nicon_valign=center\r\nmedia_url=smb://nas/Series/SPY×FAMILY/Bonus [FR].mkv\r\n",
            episode
        );
        Assert.Equal(1, result.EpisodeCount);
    }

    [Fact]
    public void Export_MovieDefinition_MatchesLegacyBytes()
    {
        using ExportFixture fixture = new();
        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        byte[] expected = File.ReadAllBytes(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "Movie.txt"
            )
        );
        byte[] actual = File.ReadAllBytes(
            Path.Combine(
                result.Directory,
                "00",
                "99",
                ExportFixture.MovieName,
                "dune_folder.txt"
            )
        );
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Export_GroupsAndShortcuts_PreservesLegacyNavigation()
    {
        using ExportFixture fixture = new();
        File.WriteAllText(
            Path.Combine(fixture.Root, "groups.xml"),
            "<groups><selections><selection enabled='yes'><name>Enfants</name><movie>"
                + ExportFixture.MovieName
                + "</movie></selection></selections></groups>",
            Encoding.UTF8
        );
        File.WriteAllText(
            Path.Combine(fixture.Root, "shortcuts.xml"),
            "<shortcuts><shortcut type='0'>Enfants</shortcut></shortcuts>",
            Encoding.UTF8
        );
        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        string selection = File.ReadAllText(
            Path.Combine(
                result.Directory,
                "00",
                "01_Enfants",
                "dune_folder.txt"
            )
        );
        Assert.Contains(
            ".media_url=../99/" + ExportFixture.MovieName,
            selection,
            StringComparison.Ordinal
        );
        string shortcut = File.ReadAllText(
            Path.Combine(
                result.Directory,
                "05_00_Enfants",
                "dune_folder.txt"
            )
        );
        Assert.Contains(
            "media_url=../00/01_Enfants",
            shortcut,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "system_files=*.aai,*.jpg,98,99,01_Enfants",
            File.ReadAllText(
                Path.Combine(
                    result.Directory,
                    "00",
                    "dune_folder.txt"
                )
            ),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Export_NonemptyCorruptFanart_StopsAndKeepsPreviousCatalog()
    {
        using ExportFixture fixture = new();
        DuneExporter exporter = ExportFixture.Exporter();
        ExportResult original = exporter.Export(
            fixture.Cards(),
            fixture.Destination
        );
        string previous = Path.Combine(
            original.Directory,
            "dune_folder.txt"
        );
        byte[] before = File.ReadAllBytes(previous);
        string fanart = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "fanart.jpg"
        );
        File.WriteAllText(fanart, "corrupt", Encoding.UTF8);
        InvalidDataException exception =
            Assert.Throws<InvalidDataException>(() =>
                exporter.Export(
                    fixture.Cards(),
                    fixture.Destination
                )
            );
        Assert.Contains(
            "L’été d’Adèle",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            fanart,
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Equal(before, File.ReadAllBytes(previous));
        Assert.Empty(
            Directory.GetDirectories(
                fixture.Root,
                ".export.yadis3-stage-*"
            )
        );
    }

    [Fact]
    public void Export_EmptyFanart_UsesAbsentFanartBehavior()
    {
        using ExportFixture fixture = new();
        string path = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "fanart.jpg"
        );
        File.WriteAllBytes(path, []);
        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        Assert.Equal(2, result.MediaCount);
        Assert.Equal(0, new FileInfo(path).Length);
    }

    [Fact]
    public void Export_TwentyNineSagas_CreatesEveryLegacyFolder()
    {
        using ExportFixture fixture = new();
        XElement[] sagas = Enumerable.Range(0, 29)
            .Select(index => Saga(
                $"Saga {index:00}",
                index == 0 ? [ExportFixture.MovieName] : []
            ))
            .ToArray();
        SaveSagas(fixture, sagas);

        ExportResult result = ExportFixture.Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        string root = Path.Combine(result.Directory, "00", "98");
        Assert.Equal(29, Directory.GetDirectories(root).Length);
        foreach (XElement saga in sagas)
        {
            string directory = Path.Combine(root, saga.Element("name")!.Value);
            Assert.True(File.Exists(Path.Combine(directory, "dune_folder.txt")));
            Assert.True(File.Exists(Path.Combine(directory, "background.jpg")));
        }

        string listing = File.ReadAllText(
            Path.Combine(result.Directory, "00", "00", "dune_folder.txt")
        );
        Assert.Contains("98/Saga 00", listing, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "Saga 00", "icon.aai")));
    }

    [Fact]
    public void Export_SagaMissingFilm_PreservesXmlMemberOrderAndLinks()
    {
        using ExportFixture fixture = new();
        SaveSagas(fixture, Saga("Fantômes",
            ["Missing.mkv", ExportFixture.MovieName]));

        ExportResult result = ExportFixture.Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        string definition = File.ReadAllText(
            Path.Combine(result.Directory, "00", "98", "Fantômes",
                "dune_folder.txt")
        );
        Assert.Contains(
            "item.0.media_url=../../99/Missing.mkv",
            definition, StringComparison.Ordinal
        );
        Assert.Contains(
            "item.1.media_url=../../99/" + ExportFixture.MovieName,
            definition, StringComparison.Ordinal
        );
        Assert.Contains("item.0.caption=00", definition, StringComparison.Ordinal);
        Assert.Contains("item.1.caption=01", definition, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Alien", new[] { "Alien 1.mkv", "Alien 2.mkv", "Alien 3.mkv", "Alien 4.mkv" })]
    [InlineData("Batman", new[] { "Batman 3.mkv", "Batman 1.mkv", "Batman 2.mkv" })]
    [InlineData("Star Wars", new[] { "Episode 4.mkv", "Episode 5.mkv", "Episode 1.mkv" })]
    public void Export_SagaCustomOrder_UsesXmlOrderNotTitleOrChronology(
        string name,
        string[] members
    )
    {
        using ExportFixture fixture = new();
        SaveSagas(fixture, Saga(name, members));
        ExportFixture.Exporter().Export(fixture.Cards(), fixture.Destination);
        AssertSagaOrder(fixture, name, members);
    }

    [Fact]
    public void Export_SagaOrderEditedAndExportedAgain_PreservesUpdatedOrder()
    {
        using ExportFixture fixture = new();
        DuneExporter exporter = ExportFixture.Exporter();
        string[] initial = ["Alien 1.mkv", "Alien 3.mkv",
            "Alien 4.mkv", "Alien 2.mkv"];
        string[] reordered = ["Alien 1.mkv", "Alien 2.mkv",
            "Alien 3.mkv", "Alien 4.mkv"];
        SaveSagas(fixture,
            Saga("Alien", initial),
            Saga("X-Men", ["X3.mkv", "X1.mkv", "X2.mkv"]));
        exporter.Export(fixture.Cards(), fixture.Destination);
        AssertSagaOrder(fixture, "Alien", initial);
        AssertSagaOrder(fixture, "X-Men",
            ["X3.mkv", "X1.mkv", "X2.mkv"]);

        SaveSagas(fixture,
            Saga("Alien", reordered),
            Saga("X-Men", ["X3.mkv", "X1.mkv", "X2.mkv"]));
        exporter.Export(fixture.Cards(), fixture.Destination);
        AssertSagaOrder(fixture, "Alien", reordered);
        AssertSagaOrder(fixture, "X-Men",
            ["X3.mkv", "X1.mkv", "X2.mkv"]);
        exporter.Export(fixture.Cards(), fixture.Destination);
        AssertSagaOrder(fixture, "Alien", reordered);
    }

    private static void AssertSagaOrder(
        ExportFixture fixture,
        string name,
        string[] members
    )
    {
        string definition = File.ReadAllText(
            Path.Combine(fixture.Destination, "00", "98", name,
                "dune_folder.txt"));
        for (int index = 0; index < members.Length; index++)
        {
            Assert.Contains(
                $"item.{index}.media_url=../../99/{members[index]}",
                definition, StringComparison.Ordinal);
            Assert.Contains(
                $"item.{index}.caption={index:00}",
                definition, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Export_InvalidSaga_DoesNotPreventFollowingSaga()
    {
        using ExportFixture fixture = new();
        SaveSagas(fixture,
            Saga("Invalid/Name", []),
            Saga("Valid", [ExportFixture.MovieName]));

        ExportResult result = ExportFixture.Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        Assert.True(File.Exists(
            Path.Combine(result.Directory, "00", "98",
                "Valid", "dune_folder.txt")
        ));
        Assert.False(Directory.Exists(
            Path.Combine(result.Directory, "00", "98", "Invalid")
        ));
    }

    [Fact]
    public void Export_RenamedAndRemovedSagas_DeletesPreviousFolders()
    {
        using ExportFixture fixture = new();
        DuneExporter exporter = ExportFixture.Exporter();
        SaveSagas(fixture,
            Saga("Ancienne", [ExportFixture.MovieName]),
            Saga("Supprimée", []));
        exporter.Export(fixture.Cards(), fixture.Destination);

        SaveSagas(fixture,
            Saga("Nouvelle", [ExportFixture.MovieName]));
        exporter.Export(fixture.Cards(), fixture.Destination);

        string root = Path.Combine(fixture.Destination, "00", "98");
        string directory = Assert.Single(Directory.GetDirectories(root));
        Assert.Equal("Nouvelle", Path.GetFileName(directory));
        Assert.True(File.Exists(
            Path.Combine(root, "Nouvelle", "dune_folder.txt")
        ));
    }

    private static XElement Saga(string name, string[] members)
    {
        return new XElement("saga",
            new XElement("name", name),
            new XElement("sort_name", name),
            new XElement("poster",
                members.Contains(ExportFixture.MovieName,
                    StringComparer.Ordinal) ? ExportFixture.MovieName : ""),
            new XElement("fanart", ""),
            members.Select(member => new XElement("movie", member))
        );
    }

    private static void SaveSagas(
        ExportFixture fixture,
        params XElement[] sagas
    )
    {
        new XDocument(new XElement("groups",
            new XElement("sagas", sagas)))
            .Save(Path.Combine(fixture.Root, "groups.xml"));
    }
}
