using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class BasicDuneConfigurationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Process_FilmBasique_EcritLesCheminsLocauxEtLeFondParent(
        bool multipart
    )
    {
        using ExportFixture fixture = new();
        MediaContext movie = Prepare(fixture, false);
        string source = Source(movie);
        File.WriteAllBytes(
            Path.Combine(source, "Le Film CD1.mkv"),
            []
        );
        if (multipart)
        {
            File.WriteAllBytes(
                Path.Combine(source, "Le Film CD2.mkv"),
                []
            );
        }
        DuneExporter.Process(
            movie,
            ExportFixture.Template(),
            new(BasicMode: true),
            fixture.Root
        );
        string definition = File.ReadAllText(
            Path.Combine(source, "dune_folder.txt")
        );
        Assert.Contains(
            "system_files=*\r\n",
            definition,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "item.0.media_url="
                + (
                    multipart
                        ? "playlist.m3u"
                        : "Le Film CD1.mkv"
                ),
            definition,
            StringComparison.Ordinal
        );
        Assert.True(
            File.Exists(Path.Combine(source, "background.jpg"))
        );
        Assert.True(
            File.Exists(Path.Combine(source, "icon.aai"))
        );
        Assert.False(
            File.Exists(Path.Combine(source, "preview.jpg"))
        );
        Assert.True(
            File.Exists(
                Path.Combine(
                    fixture.Root,
                    "interface",
                    "00",
                    "04",
                    "Drame",
                    "dune_folder.txt"
                )
            )
        );
        Assert.Contains(
            "background_path=background.jpg",
            File.ReadAllText(
                Path.Combine(
                    Path.GetDirectoryName(source)!,
                    "dune_folder.txt"
                )
            ),
            StringComparison.Ordinal
        );
        if (multipart)
        {
            Assert.Equal(
                "Le Film CD1.mkv\r\nLe Film CD2.mkv\r\n",
                File.ReadAllText(
                    Path.Combine(source, "playlist.m3u")
                )
            );
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Process_SerieBasique_ReproduitRedirectionEtCheminEpisodeDeYadis2(
        bool aai
    )
    {
        using ExportFixture fixture = new();
        MediaContext series = Prepare(fixture, true);
        string source = Source(series);
        DuneExporter.Process(
            series,
            ExportFixture.Template(),
            new(AaiBackgrounds: aai, BasicMode: true),
            fixture.Root
        );
        Assert.Contains(
            "media_url=_interface",
            File.ReadAllText(
                Path.Combine(source, "dune_folder.txt")
            ),
            StringComparison.Ordinal
        );
        string episode = File.ReadAllText(
            Path.Combine(
                source,
                "_interface",
                "01",
                "01_000_001",
                "dune_folder.txt"
            )
        );
        Assert.Equal(
            LegacyDuneEncoding.Header
                + "\r\nicon_path=icon.aai\r\nicon_valign=center\r\nmedia_url=../../../Saison 0/Bonus [FR].mkv\r\n",
            episode
        );
        string parent = Path.GetDirectoryName(source)!;
        Assert.Contains(
            "background_path=background.jpg",
            File.ReadAllText(
                Path.Combine(parent, "dune_folder.txt")
            ),
            StringComparison.Ordinal
        );
        Assert.True(
            File.Exists(
                Path.Combine(
                    parent,
                    aai ? "background.aai" : "background.jpg"
                )
            )
        );
        Assert.False(
            File.Exists(
                Path.Combine(source, "_interface", "preview.jpg")
            )
        );
    }

    [Fact]
    public void Render_QualiteJpeg_AffecteLesFondsEtLesApercus()
    {
        using ExportFixture fixture = new();
        MediaContext movie = fixture
            .Cards()
            .Single(card => !card.IsSeries);
        string low = Path.Combine(fixture.Root, "qualité basse");
        string high = Path.Combine(
            fixture.Root,
            "qualité haute"
        );
        using (
            TemplateRenderer renderer = new(
                ExportFixture.Template(),
                new(JpegQuality: 20)
            )
        )
        {
            renderer.Render(movie, low);
        }
        using (
            TemplateRenderer renderer = new(
                ExportFixture.Template(),
                new(JpegQuality: 95)
            )
        )
        {
            renderer.Render(movie, high);
        }
        Assert.True(
            new FileInfo(
                Path.Combine(high, "background.jpg")
            ).Length
                > new FileInfo(
                    Path.Combine(low, "background.jpg")
                ).Length
        );
        Assert.True(
            new FileInfo(
                Path.Combine(high, "preview.jpg")
            ).Length
                > new FileInfo(
                    Path.Combine(low, "preview.jpg")
                ).Length
        );
    }

    [Theory]
    [InlineData("3", true)]
    [InlineData("1", false)]
    public async Task ProcessAsync_SelectionAutomatique_AjouteSeulementLesNouveauxFilmsReussis(
        string status,
        bool added
    )
    {
        using ExportFixture fixture = new();
        string path = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "data.xml"
        );
        XDocument document = XDocument.Load(path);
        document
            .Root!.Element("item")!
            .SetElementValue("status", status);
        document
            .Root.Element("info")!
            .SetElementValue("sort_title", "");
        document
            .Root.Element("info")!
            .SetElementValue("title", "Le Château");
        document.Save(path);
        fixture
            .ConfigurationStore()
            .Save(
                fixture.Root,
                new(
                    new(),
                    [],
                    XDocument.Parse(
                        "<groups><selections><selection enabled='yes'><name>Famille</name></selection></selections></groups>"
                    ),
                    []
                )
            );
        using WorkspaceRepository repository = new();
        MediaCard card = repository
            .Open(fixture.Root)
            .Cards.Single(value => !value.IsSeries);
        await new MediaProcessingService(
            repository,
            TimeProvider.System
        ).ProcessAsync(
            card,
            ExportFixture.Template(),
            new(),
            fixture.Root,
            configuration: new()
            {
                AutoSelect = true,
                AutoSelectName = "Famille",
                IgnoreArticles = true,
            }
        );
        Assert.Equal(
            added ? 1 : 0,
            fixture
                .ConfigurationStore()
                .Load(fixture.Root)
                .Groups.Descendants("movie")
                .Count()
        );
        Assert.Equal(
            "Château",
            XDocument
                .Load(path)
                .Root!.Element("info")!
                .Element("sort_title")!
                .Value
        );
        Assert.Equal(
            "1",
            XDocument
                .Load(path)
                .Root!.Element("item")!
                .Element("status")!
                .Value
        );
    }

    [Fact]
    public void EnsureMovieSortTitle_TriExistant_NeLeReecritPas()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        MediaCard card = repository
            .Open(fixture.Root)
            .Cards.Single(value => !value.IsSeries);
        string expected = card.Snapshot()
            .Document.Root!.Element("info")!
            .Element("sort_title")!
            .Value;
        card.EnsureMovieSortTitle(
            new() { IgnoreArticles = true }
        );
        Assert.Equal(
            expected,
            card.Snapshot()
                .Document.Root!.Element("info")!
                .Element("sort_title")!
                .Value
        );
        Assert.False(card.IsDirty);
    }

    private static string Source(MediaContext media) =>
        Path.Combine(
            media.Header.Element("path")!.Value,
            media.Header.Element("filename")!.Value
        );

    private static MediaContext Prepare(
        ExportFixture fixture,
        bool series
    )
    {
        MediaContext media = fixture
            .Cards()
            .Single(value => value.IsSeries == series);
        string source = Path.Combine(
            fixture.Root,
            "Vidéos",
            series ? "SPY×FAMILY" : "Le Film (2023)"
        );
        Directory.CreateDirectory(source);
        string data = Path.Combine(
            media.DataDirectory,
            "data.xml"
        );
        XDocument document = XDocument.Load(data);
        XElement item = document.Root!.Element("item")!;
        item.SetElementValue(
            "filename",
            Path.GetFileName(source)
        );
        item.SetElementValue(
            "path",
            Path.GetDirectoryName(source)
        );
        item.SetElementValue("folder", "yes");
        if (series)
        {
            document
                .Descendants("episode")
                .Single()
                .SetElementValue(
                    "file",
                    Path.Combine(
                        source,
                        "Saison 0",
                        "Bonus [FR].mkv"
                    )
                );
        }
        document.Save(data);
        return fixture
            .Cards()
            .Single(value => value.IsSeries == series);
    }
}
