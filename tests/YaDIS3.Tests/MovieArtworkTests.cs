using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class MovieArtworkTests
{
    [Fact]
    public async Task FindMovieAsync_CatalogueComplet_EnregistreLeXmlYaDIS2SansFiltreLangue()
    {
        using MovieArtworkFixture fixture = new();
        using ArtworkHttpHandler handler = new();
        using HttpClient http = new(handler);
        TreatmentPreparation preparation = new(
            new ExportFileSystem(), fixture.Metadata(http), new TreatmentInputs()
        );
        await preparation.PrepareAsync(
            fixture.Card, TreatmentMode.ReloadInformation, CancellationToken.None
        );
        await fixture.SaveAsync();
        XElement actual = XDocument.Load(fixture.Card.FilePath)
            .Root!.Element("info")!.Element("images")!;
        Assert.True(XNode.DeepEquals(
            MovieArtworkFixture.ExpectedImages(), actual
        ), actual.ToString());
        Assert.Equal(5, fixture.Card.MovieArtwork.Count);
        Assert.Contains(handler.Requests, uri =>
            uri.AbsolutePath.EndsWith("/images", StringComparison.Ordinal)
                && uri.Query.Length == 0);
        Assert.DoesNotContain(handler.Requests, uri => uri.Host == "image.tmdb.org");
    }

    [Fact]
    public async Task PrepareAsync_ImagesParDefaut_UtiliseLesChoixDuFilmEtNonLePremierCatalogue()
    {
        using MovieArtworkFixture fixture = new();
        using ArtworkHttpHandler handler = new() { PreferredArtwork = true };
        using HttpClient http = new(handler);
        TreatmentPreparation preparation = new(
            new ExportFileSystem(), fixture.Metadata(http), new TreatmentInputs()
        );
        await preparation.PrepareAsync(
            fixture.Card, TreatmentMode.ReloadInformation, CancellationToken.None
        );
        Assert.Contains(handler.Requests, uri =>
            uri.AbsolutePath == "/t/p/original/preferred-poster.jpg");
        Assert.Contains(handler.Requests, uri =>
            uri.AbsolutePath == "/t/p/original/preferred-fanart.jpg");
        Assert.Equal(5, fixture.Card.MovieArtwork.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TreatAsync_ChoixOuImport_ActualiseApercuSansRemplacerLesImages(
        bool manual
    )
    {
        using MovieArtworkFixture fixture = new();
        using WorkspaceRepository repository = new();
        using ArtworkHttpHandler handler = new();
        using HttpClient http = new(handler);
        MediaCard card = fixture.Card;
        if (manual)
        {
            string source = Path.Combine(fixture.Root, "manuel.png");
            File.WriteAllBytes(source, ArtworkHttpHandler.Image);
            fixture.Importer.Import(card, ArtworkKind.Fanart, source);
        }
        else
        {
            await new MovieArtworkService(http, fixture.Importer).SelectAsync(
                card, card.MovieArtwork[^1], CancellationToken.None);
        }
        TreatmentPreparation preparation = new(
            new ExportFileSystem(), fixture.Metadata(http), new TreatmentInputs()
        );
        MediaTreatment treatment = new(repository, preparation, new FixedTimeProvider());
        Assert.True(await treatment.TreatAsync(
            card, ExportFixture.Template(), new RenderOptions(), fixture.Root,
            TreatmentMode.ReloadInformation, new DuneConfiguration(), null,
            CancellationToken.None));
        Assert.Equal(ArtworkHttpHandler.Image, File.ReadAllBytes(card.FanartPath));
        Assert.True(File.Exists(Path.Combine(card.DataDirectory, "preview.jpg")));
        Assert.True(XNode.DeepEquals(MovieArtworkFixture.ExpectedImages(),
            XDocument.Load(card.FilePath).Root!.Element("info")!.Element("images")));
    }

    [Theory]
    [InlineData(ArtworkKind.Poster, "w154", "poster-poster-fr.jpg")]
    [InlineData(ArtworkKind.Fanart, "w300", "fanart-fanart-en.jpg")]
    public async Task ThumbnailAsync_CacheVide_RechargePuisReutiliseLaVignette(
        ArtworkKind kind, string size, string filename
    )
    {
        using MovieArtworkFixture fixture = new();
        using ArtworkHttpHandler handler = new();
        using HttpClient http = new(handler);
        MovieArtworkService service = new(http, fixture.Importer);
        ArtworkReference reference = fixture.Card.MovieArtwork.First(image => image.Kind == kind);
        Directory.CreateDirectory(Path.Combine(fixture.Root, "cache"));
        string cache = Path.Combine(fixture.Root, "cache", filename);
        File.WriteAllBytes(cache, []);
        Assert.Equal(cache, await service.ThumbnailAsync(
            reference, fixture.Root, CancellationToken.None));
        Assert.Equal(cache, await service.ThumbnailAsync(
            reference, fixture.Root, CancellationToken.None));
        Assert.Contains("/" + size + "/", Assert.Single(handler.Requests).AbsoluteUri,
            StringComparison.Ordinal);
        Assert.Equal(ArtworkHttpHandler.Image, File.ReadAllBytes(cache));
    }

    [Theory]
    [InlineData(ArtworkKind.Poster, false)]
    [InlineData(ArtworkKind.Fanart, false)]
    [InlineData(ArtworkKind.Poster, true)]
    [InlineData(ArtworkKind.Fanart, true)]
    public async Task PrepareAsync_ChoixOuImportManuel_ConserveLesImagesEtLaBanniere(
        ArtworkKind kind, bool manual
    )
    {
        using MovieArtworkFixture fixture = new();
        using ArtworkHttpHandler handler = new();
        using HttpClient http = new(handler);
        MediaCard card = fixture.Card;
        byte[] banner = [11, 22, 33];
        File.WriteAllBytes(card.BannerPath, banner);
        File.WriteAllBytes(card.PosterPath, ArtworkHttpHandler.Image);
        File.WriteAllBytes(card.FanartPath, ArtworkHttpHandler.Image);
        string other = kind == ArtworkKind.Poster
            ? card.FanartPath : card.PosterPath;
        byte[] untouched = [44, 55, 66];
        File.WriteAllBytes(other, untouched);
        if (manual)
        {
            string source = Path.Combine(fixture.Root, "manuel.png");
            File.WriteAllBytes(source, ArtworkHttpHandler.Image);
            fixture.Importer.Import(card, kind, source);
        }
        else
        {
            ArtworkReference reference = card.MovieArtwork.Last(image => image.Kind == kind);
            await new MovieArtworkService(http, fixture.Importer).SelectAsync(
                card, reference, CancellationToken.None);
        }
        string path = kind == ArtworkKind.Poster ? card.PosterPath : card.FanartPath;
        Assert.Equal(ArtworkHttpHandler.Image, File.ReadAllBytes(path));
        handler.Requests.Clear();
        TreatmentPreparation preparation = new(
            new ExportFileSystem(), fixture.Metadata(http), new TreatmentInputs()
        );
        await preparation.PrepareAsync(card, TreatmentMode.ReloadInformation, CancellationToken.None);
        await fixture.SaveAsync();
        Assert.DoesNotContain(handler.Requests, uri => uri.Host == "image.tmdb.org");
        Assert.Equal(banner, File.ReadAllBytes(card.BannerPath));
        Assert.Equal(untouched, File.ReadAllBytes(other));
        string saved = Path.Combine(card.DataDirectory,
            kind == ArtworkKind.Poster ? "poster.jpg" : "fanart.jpg");
        Assert.Equal(ArtworkHttpHandler.Image, File.ReadAllBytes(saved));
        handler.Requests.Clear();
        await preparation.PrepareAsync(card, TreatmentMode.Normal, CancellationToken.None);
        Assert.Empty(handler.Requests);
        Assert.Equal(ArtworkHttpHandler.Image, File.ReadAllBytes(saved));
    }

    [Fact]
    public async Task SelectAsync_TelechargementEnEchec_ConserveLeChoixPrecedent()
    {
        using MovieArtworkFixture fixture = new();
        using ArtworkHttpHandler handler = new();
        using HttpClient http = new(handler);
        MovieArtworkService service = new(http, fixture.Importer);
        ArtworkReference reference = fixture.Card.MovieArtwork[^1];
        await service.SelectAsync(fixture.Card, reference, CancellationToken.None);
        string previous = fixture.Card.FanartPath;
        handler.FailDownloads = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => service.SelectAsync(
            fixture.Card, reference, CancellationToken.None));
        Assert.Equal(previous, fixture.Card.FanartPath);
        Assert.Equal(ArtworkHttpHandler.Image, File.ReadAllBytes(previous));
        Assert.Equal(5, fixture.Card.MovieArtwork.Count);
    }

    [Theory]
    [InlineData("<poster>https://image.tmdb.org/t/p/original/a.jpg</poster>", ArtworkKind.Poster)]
    [InlineData("<fanart>https://image.tmdb.org/t/p/original/a.jpg</fanart>", ArtworkKind.Fanart)]
    public async Task MovieArtwork_AncienXmlDuPortage_ResteLisible(
        string legacy, ArtworkKind kind
    )
    {
        using MovieArtworkFixture fixture = new();
        string file = fixture.Card.FilePath;
        XDocument xml = XDocument.Load(file);
        xml.Root!.Element("info")!.Element("images")!.ReplaceNodes(XElement.Parse(legacy));
        xml.Save(file);
        using WorkspaceRepository repository = new();
        using MediaCard card = repository.Open(fixture.Root).Cards.Single(item => !item.IsSeries);
        Assert.Equal(kind, Assert.Single(card.MovieArtwork).Kind);
        await repository.SaveAsync(card);
        Assert.Equal(XElement.Parse(legacy).Value,
            Assert.Single(card.MovieArtwork).Url);
    }
}
