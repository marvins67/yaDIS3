using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class SeriesMetadataTests
{
    private static readonly string[] s_genres = ["Drame", "Comédie"];
    private static readonly string[] s_actors = ["Actrice", "Invité"];
    [Theory]
    [InlineData("default")]
    [InlineData("dvd")]
    [InlineData("absolute")]
    public async Task SeriesAsync_Metadata_CompletEtOrdreConserve(string order)
    {
        using SeriesMetadataHandler handler = new();
        using HttpClient http = new(handler);
        using TvdbClient client = new(http, new TvdbSettings("test", ""));
        SeriesDetails series = await client.SeriesAsync(42, order);
        Assert.Equal(s_genres, series.Genres);
        Assert.Equal(s_actors, series.Actors);
        Assert.Equal("", series.Rating);
        Assert.Equal("", Assert.Single(series.Episodes).Rating);
        Assert.Equal("Réalisateur", series.Episodes[0].Director);
        Assert.Contains(handler.Requests, path => path.Contains(
            "/episodes/" + order + "/fra", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplyTvdb_EpisodeManuel_ConserveLesRetouches()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = repository.Open(fixture.Root).Cards.Single(item => item.IsSeries);
        Episode episode = card.Episodes[0];
        episode.Title = "Bonus manuel";
        SeriesDetails series = new(42, "Série", "Résumé", "2020-01-01", "Chaîne",
            [new(0, 1, "Titre fournisseur", "Résumé", "2020-01-02", "")
                { Rating = "8", Director = "Réalisateur" }]);
        card.ApplyTvdb(series, "dvd");
        Assert.Equal("Bonus manuel", episode.Title);
        Assert.True(episode.IsManual);
    }

    [Fact]
    public async Task SeriesAsync_EpisodesLocaux_ChargeUniquementLeursDetails()
    {
        using SeriesMetadataHandler handler = new();
        using HttpClient http = new(handler);
        using TvdbClient client = new(http, new TvdbSettings("test", ""));
        await client.SeriesAsync(42, "default", "fra", requestedEpisodes:
            new HashSet<(int, int)> { (2, 1) });
        Assert.DoesNotContain(handler.Requests, path => path.Contains(
            "/episodes/8/extended", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplyTvdb_Metadata_EcritLesChampsHistoriques()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = repository.Open(fixture.Root).Cards.Single(item => item.IsSeries);
        SeriesDetails series = new(42, "Série", "Résumé", "2020-01-01", "Chaîne",
            [new(0, 1, "Bonus", "Résumé", "2020-01-02", "") { Rating = "8", Director = "Réalisateur" }])
        {
            Rating = "9",
            Genres = ["Drame"],
            Actors = ["Actrice", "Invité"],
        };
        card.ApplyTvdb(series, "dvd");
        Assert.Equal("dvd", card.Order);
        Assert.Equal("9", card.Snapshot().Document.Root!.Element("info")!.Element("rating")!.Value);
        Assert.Equal("Drame", card.Genres);
        Assert.Equal("Actrice, Invité", card.Actors);
        var episode = card.Snapshot().Document.Root!.Element("info")!.Element("episode")!;
        Assert.Equal("8", episode.Element("rating")!.Value);
        Assert.Equal("Réalisateur", episode.Element("director")!.Value);
    }
}
