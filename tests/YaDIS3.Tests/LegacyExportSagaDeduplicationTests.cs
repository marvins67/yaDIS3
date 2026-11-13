using System.Xml.Linq;
using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyExportSagaDeduplicationTests
{
    private const int PropertiesPerItem = 8;

    [Theory]
    [InlineData("Une quête", false, false)]
    [InlineData("Une quête", false, true)]
    [InlineData("Une Quête", true, true)]
    [InlineData("Une quête parallèle", true, false)]
    public void Export_SagaNameInCaptionSuffix_PreservesLegacyMembershipAndIndices(
        string firstTitle,
        bool includeSaga,
        bool byDate
    )
    {
        using ExportFixture fixture = new();
        MediaContext[] movies =
        [
            Movie(fixture, "Seul.mkv", firstTitle),
            Movie(fixture, "Membre 1.mkv", "Premier membre"),
            Movie(fixture, "Membre 2.mkv", "Second membre"),
            Movie(fixture, "Après.mkv", "Après"),
        ];
        SaveGroups(fixture, movies, Saga("quête", "Tri saga", "Membre 1.mkv", "Membre 2.mkv"));

        Export(fixture, movies, byDate, aai: true);

        AssertCaptionSuffixListings(fixture, firstTitle, includeSaga, byDate);
    }

    [Fact]
    public void Export_SagaNameInAnotherSagaUrl_UsesLiteralDefinitionMembership()
    {
        using ExportFixture fixture = new();
        MediaContext[] movies =
        [
            Movie(fixture, "Long.mkv", "Premier membre"),
            Movie(fixture, "Court.mkv", "Second membre"),
            Movie(fixture, "Après.mkv", "Après"),
        ];
        SaveGroups(fixture, movies,
            Saga("Long Quest", "Partie longue", "Long.mkv"),
            Saga("Quest", "Partie courte", "Court.mkv"));

        Export(fixture, movies, byDate: false, aai: true);

        AssertItems(fixture, "00/01_Choix",
            (0, "Partie longue", "../98/Long Quest"),
            (2, "Après", "../99/Après.mkv"));
        AssertItems(fixture, "00/02",
            (0, "Partie longue", "../98/Long Quest"),
            (1, "Après", "../99/Après.mkv"));
    }

    [Theory]
    [InlineData("jpg", true, false)]
    [InlineData("background.jpg", true, false)]
    [InlineData("aai", false, false)]
    [InlineData("background.aai", false, false)]
    [InlineData("jpg", true, true)]
    [InlineData("background.jpg", true, true)]
    [InlineData("aai", false, true)]
    [InlineData("background.aai", false, true)]
    public void Export_SagaNameInHeader_DeduplicatesBeforeBackgroundConversion(
        string name,
        bool includeSaga,
        bool aai
    )
    {
        using ExportFixture fixture = new();
        MediaContext[] movies =
        [
            Movie(fixture, "Membre.mkv", "Le membre"),
            Movie(fixture, "Après.mkv", "Après"),
        ];
        SaveGroups(fixture, movies, Saga(name, "Tri saga", "Membre.mkv"));

        Export(fixture, movies, byDate: false, aai);

        AssertHeaderListings(fixture, name, includeSaga);
        string background = aai ? "background.aai" : "background.jpg";
        Assert.Contains("background_path=../00/" + background, Lines(fixture, "00/01_Choix"));
        Assert.Contains("background_path=../00/" + background, Lines(fixture, "00/02"));
    }

    private static void AssertCaptionSuffixListings(
        ExportFixture fixture,
        string firstTitle,
        bool includeSaga,
        bool byDate
    )
    {
        string prefix = byDate ? "000_" : "";
        if (includeSaga)
        {
            AssertItems(fixture, "00/01_Choix",
                (0, firstTitle, "../99/Seul.mkv"),
                (1, "Tri saga", "../98/quête"),
                (3, "Après", "../99/Après.mkv"));
            AssertItems(fixture, "00/02",
                (0, prefix + firstTitle, "../99/Seul.mkv"),
                (1, prefix + "Tri saga", "../98/quête"),
                (2, prefix + "Après", "../99/Après.mkv"));
            return;
        }
        AssertItems(fixture, "00/01_Choix",
            (0, firstTitle, "../99/Seul.mkv"),
            (3, "Après", "../99/Après.mkv"));
        AssertItems(fixture, "00/02",
            (0, prefix + firstTitle, "../99/Seul.mkv"),
            (1, prefix + "Après", "../99/Après.mkv"));
    }

    private static void AssertHeaderListings(ExportFixture fixture, string name, bool includeSaga)
    {
        if (includeSaga)
        {
            AssertItems(fixture, "00/01_Choix",
                (0, "Tri saga", "../98/" + name),
                (1, "Après", "../99/Après.mkv"));
            AssertItems(fixture, "00/02",
                (0, "Tri saga", "../98/" + name),
                (1, "Après", "../99/Après.mkv"));
            return;
        }
        AssertItems(fixture, "00/01_Choix", (1, "Après", "../99/Après.mkv"));
        AssertItems(fixture, "00/02", (0, "Après", "../99/Après.mkv"));
    }

    private static void AssertItems(
        ExportFixture fixture,
        string relative,
        params (int Index, string Caption, string Target)[] expected
    )
    {
        string[] lines = Lines(fixture, relative);
        Assert.Equal(
            expected.SelectMany(entry => new[]
            {
                $"item.{entry.Index}.caption={entry.Caption}",
                $"item.{entry.Index}.media_url={entry.Target}",
            }),
            lines.Where(line => line.StartsWith("item.", StringComparison.Ordinal)
                && (line.Contains(".caption=", StringComparison.Ordinal)
                    || line.Contains(".media_url=", StringComparison.Ordinal))));
        Assert.Equal(expected.Length * PropertiesPerItem, lines.Count(line => line.StartsWith("item.", StringComparison.Ordinal)));
    }

    private static string[] Lines(ExportFixture fixture, string relative) =>
        File.ReadAllLines(Path.Combine(fixture.Destination,
            relative.Replace('/', Path.DirectorySeparatorChar), "dune_folder.txt"));

    private static void Export(ExportFixture fixture, MediaContext[] movies, bool byDate, bool aai)
    {
        Directory.CreateDirectory(fixture.Destination);
        new DuneExporter(ExportFixture.Template(), new RenderOptions { AaiBackgrounds = aai }, [],
            catalogOptions: new LegacyCatalogOptions
            {
                Categories = "10000000000",
                SortRecentByDate = byDate,
            }, timeProvider: new FixedTimeProvider())
            .ExportLegacy(movies, fixture.Destination);
    }

    private static MediaContext Movie(ExportFixture fixture, string name, string title)
    {
        MediaContext movie = fixture.Cards().Single(card => !card.IsSeries);
        movie.Header.SetElementValue("filename", name);
        movie.Header.SetElementValue("date", "2026-11-02");
        movie.Info.SetElementValue("sort_title", title);
        return movie;
    }

    private static XElement Saga(string name, string sortName, params string[] members) =>
        new("saga", new XElement("name", name), new XElement("sort_name", sortName),
            members.Select(member => new XElement("movie", member)));

    private static void SaveGroups(ExportFixture fixture, MediaContext[] movies, params XElement[] sagas) =>
        new XElement("groups", new XElement("sagas", sagas),
            new XElement("selection", new XAttribute("enabled", "yes"),
                new XElement("name", "Choix"),
                movies.Select(movie => new XElement("movie", movie.Header.Element("filename")!.Value))))
            .Save(Path.Combine(fixture.Root, "groups.xml"));
}
