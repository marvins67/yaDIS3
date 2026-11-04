using System.Text.Json.Nodes;
using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class MovieMetadataTests
{
    private static string FixturePath(string name) =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Metadata",
            name
        );

    [Theory]
    [InlineData(false, true, "Film")]
    [InlineData(true, true, "Film")]
    [InlineData(false, false, "Le Film")]
    [InlineData(true, false, "Le Film")]
    public async Task PrepareAsync_IdentificationOuRechargement_ReproduitLesChampsYaDIS2(
        bool reload,
        bool ignoreArticles,
        string sortTitle
    )
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(
            fixture, repository, reload
        );
        using MovieMetadataHandler handler = new(
            File.ReadAllText(FixturePath("movie.json"))
        );
        using HttpClient http = new(handler);
        OnlineTreatmentMetadata metadata = Metadata(http, "fr");
        TreatmentPreparation preparation = new(
            new ExportFileSystem(),
            metadata,
            new TreatmentInputs()
        );
        Assert.True(
            await preparation.PrepareAsync(
                card,
                reload
                    ? TreatmentMode.ReloadInformation
                    : TreatmentMode.Normal,
                CancellationToken.None
            )
        );
        card.EnsureMovieSortTitle(
            new DuneConfiguration
            {
                IgnoreArticles = ignoreArticles,
            }
        );
        await repository.SaveAsync(card);
        XElement actual = XDocument
            .Load(card.FilePath)
            .Root!.Element("info")!;
        actual.Element("images")!.Remove();
        actual.Element("mediainfo")!.Remove();
        XElement expected = XElement.Load(
            FixturePath("yadis2-movie.xml")
        );
        expected.SetElementValue("sort_title", sortTitle);
        Assert.True(
            XNode.DeepEquals(expected, actual),
            actual.ToString()
        );
        Assert.Contains(
            handler.Requests,
            request => request.Contains(
                "append_to_response=credits,release_dates",
                StringComparison.Ordinal
            )
        );
    }

    [Theory]
    [InlineData("fr", "TOUS PUBLICS")]
    [InlineData("FR", "TOUS PUBLICS")]
    [InlineData("us", "R")]
    [InlineData("en", "")]
    public async Task FindMovieAsync_Classification_RespecteLePaysHistoriqueSansRepli(
        string language,
        string expected
    )
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(
            fixture, repository, true
        );
        using MovieMetadataHandler handler = new(
            File.ReadAllText(FixturePath("movie.json"))
        );
        using HttpClient http = new(handler);
        XElement? info = await Metadata(http, language)
            .FindMovieAsync(card, CancellationToken.None);
        Assert.Equal(
            expected, info!.Element("certification")!.Value
        );
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task PrepareAsync_TraitementNormal_ConserveLesRetouchesSansRequete()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(
            fixture, repository, true
        );
        card.Directors = "Réalisateur corrigé";
        card.Countries = "CA";
        card.Studios = "Studio corrigé";
        using MovieMetadataHandler handler = new("{}");
        using HttpClient http = new(handler);
        TreatmentPreparation preparation = new(
            new ExportFileSystem(),
            Metadata(http, "fr"),
            new TreatmentInputs()
        );
        await preparation.PrepareAsync(
            card, TreatmentMode.Normal, CancellationToken.None
        );
        card.EnsureMovieSortTitle(
            new DuneConfiguration { IgnoreArticles = true }
        );
        Assert.Empty(handler.Requests);
        Assert.Equal("Ancien tri manuel", card.SortTitle);
        Assert.Equal(
            "Ancienne classification", card.Certification
        );
        Assert.Equal("Réalisateur corrigé", card.Directors);
        Assert.Equal("CA", card.Countries);
        Assert.Equal("Studio corrigé", card.Studios);
    }

    [Fact]
    public async Task FindMovieAsync_ChampsOptionnelsVides_ConserveLesElementsVides()
    {
        JsonNode movie = JsonNode.Parse(
            File.ReadAllText(FixturePath("movie.json"))
        )!;
        movie["homepage"] = null;
        movie["release_date"] = "";
        movie["production_companies"] = new JsonArray();
        movie["production_countries"] = new JsonArray();
        movie["credits"]!["cast"] = new JsonArray();
        movie["credits"]!["crew"] = new JsonArray();
        movie["release_dates"]!["results"] = new JsonArray();
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(
            fixture, repository, true
        );
        using MovieMetadataHandler handler = new(
            movie.ToJsonString()
        );
        using HttpClient http = new(handler);
        XElement? info = await Metadata(http, "fr")
            .FindMovieAsync(card, CancellationToken.None);
        foreach (string field in new[]
        {
            "url", "trailer", "year", "certification", "studios",
            "countries", "actors", "directors",
        })
        {
            Assert.Equal("", info!.Element(field)!.Value);
        }
    }

    private static OnlineTreatmentMetadata Metadata(
        HttpClient http,
        string language
    ) => new(
        http,
        null,
        "test-only-token",
        language,
        new ImageImporter(new UnusedJpegEncoder())
    );

    private static MediaCard PrepareCard(
        ExportFixture fixture,
        WorkspaceRepository repository,
        bool reload
    )
    {
        string file = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "data.xml"
        );
        XDocument xml = XDocument.Load(file);
        XElement info = xml.Root!.Element("info")!;
        if (reload)
        {
            info.SetElementValue("id", "42");
            info.SetElementValue("source", "0");
            info.SetElementValue(
                "sort_title", "Ancien tri manuel"
            );
            info.SetElementValue(
                "certification", "Ancienne classification"
            );
        }
        else
        {
            info.Remove();
        }
        XElement item = xml.Root.Element("item")!;
        item.SetElementValue("path", fixture.Root);
        File.WriteAllBytes(
            Path.Combine(fixture.Root, ExportFixture.MovieName),
            []
        );
        xml.Save(file);
        return repository.Open(fixture.Root)
            .Cards.Single(card => !card.IsSeries);
    }
}
