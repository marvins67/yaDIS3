using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyCatalogParityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Export_LegacyTechnicalMetadata_CreatesAllFormatIndexes(
        bool itemRoot
    )
    {
        using ExportFixture fixture = new();
        string[] formats =
        [
            "AVI",
            "DVD",
            "BDAV",
            "MP4",
            "MPEG-TS",
            "Matroska",
        ];
        foreach (string format in formats)
        {
            XDocument data = ReadMovie(fixture);
            XElement root = data.Root!;
            XElement technical = root.Element("mediainfo")!;
            technical.Remove();
            technical.Element("format")!.Value = format;
            root.Element("info")!.Add(technical);
            if (itemRoot)
            {
                XElement header = root.Element("item")!;
                header.Add(root.Element("info"));
                data = new XDocument(new XElement(header));
            }
            SaveMovie(fixture, data, format + ".mkv");
        }
        fixture.ProcessIndexes();
        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        foreach (string format in formats)
        {
            string name = format == "BDAV" ? "M2TS" : format;
            Assert.True(
                File.Exists(
                    Path.Combine(
                        result.Directory,
                        "00",
                        "11",
                        name,
                        "dune_folder.txt"
                    )
                )
            );
        }
    }

    [Theory]
    [InlineData("Robert Downey Jr.", "00/05/J/Robert Downey Jr")]
    [InlineData(
        "Pedro Armendáriz Jr.",
        "00/05/J/Pedro Armendáriz Jr"
    )]
    public void Create_TrailingPeriod_UsesWindowsDirectoryAndOriginalCaption(
        string name,
        string directory
    )
    {
        XElement info = new("info", new XElement("actor", name));
        LegacyIndexEntry entry = Assert.Single(
            LegacyMovieIndexes.Create(info, null)
        );
        Assert.Equal(directory, entry.Directory);
        Assert.Equal(name, entry.Caption);
        Assert.Equal(name, info.Element("actor")!.Value);
    }

    [Theory]
    [InlineData(0, "00", false)]
    [InlineData(0, "01", false)]
    [InlineData(2, "01", true)]
    public void Export_SeasonIcon_RequiresPositiveEpisodeSeason(
        int season,
        string viewingSeason,
        bool expected
    )
    {
        using ExportFixture fixture = new();
        string path = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.SeriesName,
            "data.xml"
        );
        XDocument data = XDocument.Load(path);
        XElement episode = data.Descendants("episode").Single();
        episode.SetAttributeValue("season", season);
        episode.SetAttributeValue(
            "key",
            viewingSeason + "_999_001"
        );
        data.Save(path);
        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        string directory = Path.Combine(
            result.Directory,
            "01",
            ExportFixture.SeriesName,
            viewingSeason
        );
        Assert.Equal(
            expected,
            File.Exists(Path.Combine(directory, "icon.aai"))
        );
        Assert.True(
            File.Exists(
                Path.Combine(directory, "dune_folder.txt")
            )
        );
        Assert.True(
            File.Exists(
                Path.Combine(
                    directory,
                    viewingSeason + "_999_001",
                    "icon.aai"
                )
            )
        );
    }

    [Fact]
    public void Export_DisneyIsoCards_PreservesAllFourLegacyDirectories()
    {
        using ExportFixture fixture = new();
        string[] names =
        [
            "Disney Animation Collection 4.iso",
            "Disney Animation Collection 5.iso",
            "Disney Animation Collection 6.iso",
            "Disney Animation Collection 7 Mickey's Christmas Carol.iso",
        ];
        foreach (string name in names)
        {
            SaveMovie(fixture, ReadMovie(fixture), name);
        }
        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        Assert.Equal(6, result.MediaCount);
        foreach (string name in names)
        {
            Assert.True(
                File.Exists(
                    Path.Combine(
                        result.Directory,
                        "00",
                        "99",
                        name,
                        "dune_folder.txt"
                    )
                )
            );
        }
    }

    [Fact]
    public void Export_AccentedSortTitle_PreservesLegacyAlphabetLetter()
    {
        using ExportFixture fixture = new();
        XDocument data = ReadMovie(fixture);
        data.Root!.Element("info")!
            .Element("sort_title")!
            .Value = "À bout de souffle";
        SaveMovie(fixture, data, ExportFixture.MovieName);
        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        Assert.True(
            File.Exists(
                Path.Combine(
                    result.Directory,
                    "00",
                    "03",
                    "À",
                    "dune_folder.txt"
                )
            )
        );
    }

    [Fact]
    public void Export_MixedViewingSeason_UsesLastPositiveSeasonNumberForIcon()
    {
        using ExportFixture fixture = new();
        string path = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.SeriesName,
            "data.xml"
        );
        XDocument data = XDocument.Load(path);
        XElement info = data.Root!.Element("info")!;
        XElement original = info.Element("episode")!;
        foreach (
            string key in new[]
            {
                "01_002_001",
                "02_002_001",
                "01_000_002",
                "01_999_002",
            }
        )
        {
            XElement episode = new(original);
            episode.SetAttributeValue("key", key);
            episode.SetAttributeValue(
                "season",
                key == "01_999_002" ? 0
                    : key == "01_002_001" ? 1
                    : 2
            );
            info.Add(episode);
        }
        data.Save(path);
        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        string directory = Path.Combine(
            result.Directory,
            "01",
            ExportFixture.SeriesName
        );
        Assert.Equal(
            File.ReadAllBytes(
                Path.Combine(directory, "02", "icon.aai")
            ),
            File.ReadAllBytes(
                Path.Combine(directory, "01", "icon.aai")
            )
        );
    }

    private static XDocument ReadMovie(ExportFixture fixture)
    {
        return XDocument.Load(
            Path.Combine(
                fixture.Root,
                "data",
                ExportFixture.MovieName,
                "data.xml"
            )
        );
    }

    private static void SaveMovie(
        ExportFixture fixture,
        XDocument data,
        string name
    )
    {
        XElement header =
            data.Root!.Name == "item"
                ? data.Root
                : data.Root.Element("item")!;
        header.Element("filename")!.Value = name;
        string directory = Path.Combine(
            fixture.Root,
            "data",
            name
        );
        Directory.CreateDirectory(directory);
        data.Save(Path.Combine(directory, "data.xml"));
    }
}
