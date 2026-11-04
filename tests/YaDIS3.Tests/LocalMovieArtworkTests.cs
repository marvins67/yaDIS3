using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class LocalMovieArtworkTests
{
    [Fact]
    public async Task PrepareAsync_PremiereIdentification_UtilisePosterPrefixeSansCover()
    {
        using MovieArtworkFixture fixture = new();
        XDocument xml = XDocument.Load(fixture.Card.FilePath);
        xml.Root!.Element("info")!.Remove();
        xml.Save(fixture.Card.FilePath);
        using WorkspaceRepository repository = new();
        using MediaCard card = repository.Open(fixture.Root)
            .Cards.Single(item => !item.IsSeries);
        string prefix = Path.GetFileNameWithoutExtension(card.FolderName);
        byte[] poster = [10, 20, 30];
        File.WriteAllBytes(Path.Combine(fixture.Root, prefix + ".poster.jpg"), poster);
        File.WriteAllBytes(Path.Combine(fixture.Root, "Autre.cover.jpg"), [99]);
        using MovieMetadataHandler handler = new(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Metadata", "movie.json")));
        using HttpClient http = new(handler);
        TreatmentPreparation preparation = new(
            new ExportFileSystem(), fixture.Metadata(http), new TreatmentInputs()
        );
        await preparation.PrepareAsync(card, TreatmentMode.Normal, CancellationToken.None);
        Assert.Equal(poster, File.ReadAllBytes(card.PosterPath));
        Assert.DoesNotContain(handler.Requests, uri => uri.Contains(
            "image.tmdb.org", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, TreatmentMode.Normal, false)]
    [InlineData(false, TreatmentMode.ReloadInformation, true)]
    [InlineData(true, TreatmentMode.Normal, false)]
    [InlineData(true, TreatmentMode.ReloadInformation, true)]
    public async Task PrepareAsync_ImagesLocales_RespectePrioriteEtMode(
        bool folder, TreatmentMode mode, bool replaced
    )
    {
        using MovieArtworkFixture fixture = new();
        using ArtworkHttpHandler handler = new();
        using HttpClient http = new(handler);
        MediaCard card = fixture.Card;
        string source = Path.Combine(fixture.Root, ExportFixture.MovieName);
        if (folder)
        {
            File.Delete(source);
            Directory.CreateDirectory(source);
            File.WriteAllBytes(Path.Combine(source, "film.mkv"), []);
        }
        string directory = folder ? source : fixture.Root;
        string prefix = folder ? "" : Path.GetFileNameWithoutExtension(source);
        byte[] cover = [1, 2, 3];
        byte[] fanart = [4, 5, 6];
        byte[] previous = [7, 8, 9];
        File.WriteAllBytes(Path.Combine(directory, prefix + ".poster.jpg"), [99]);
        File.WriteAllBytes(Path.Combine(directory, prefix + ".cover.jpg"), cover);
        File.WriteAllBytes(Path.Combine(directory, prefix + ".fanart.jpg"), fanart);
        File.WriteAllBytes(card.PosterPath, previous);
        File.WriteAllBytes(card.FanartPath, previous);
        TreatmentPreparation preparation = new(
            new ExportFileSystem(), fixture.Metadata(http), new TreatmentInputs()
        );
        await preparation.PrepareAsync(card, mode, CancellationToken.None);
        Assert.Equal(replaced ? cover : previous, File.ReadAllBytes(card.PosterPath));
        Assert.Equal(replaced ? fanart : previous, File.ReadAllBytes(card.FanartPath));
        Assert.DoesNotContain(handler.Requests, uri => uri.Host == "image.tmdb.org");
    }
}
