using System.Text;
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
}
