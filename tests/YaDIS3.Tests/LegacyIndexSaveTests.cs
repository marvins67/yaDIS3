using System.Text;
using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyIndexSaveTests
{
    [Fact]
    public void ChangeTitle_AncienneIndexation_RetireLesCriteresAvantLaRecherche()
    {
        using ExportFixture fixture = new();
        fixture.ProcessIndexes();
        using WorkspaceRepository repository = new();
        MediaCard card = repository.Open(fixture.Root).Cards
            .Single(media => !media.IsSeries);
        string indexes = Path.Combine(fixture.Root, "interface", "00");
        Assert.NotEmpty(Directory.EnumerateFiles(
            indexes, "dune_folder.txt", SearchOption.AllDirectories
        ));

        new LegacyMovieTitleChangeCleanup().Remove(card);
        card.PrepareMovieTitleChange(235);

        Assert.Empty(Directory.EnumerateFiles(
            indexes, "dune_folder.txt", SearchOption.AllDirectories
        ));
        Assert.True(File.Exists(card.FilePath));
        Assert.Equal(
            "235",
            card.Snapshot().Document.Root!.Element("info")!
                .Element("id")!.Value
        );
    }

    [Fact]
    public async Task SaveAsync_FicheModifiee_RetireLesAnciensIndexAvantTraitement()
    {
        using ExportFixture fixture = new();
        fixture.ProcessIndexes();
        using WorkspaceRepository repository = new();
        MediaCard movie = repository
            .Open(fixture.Root)
            .Cards.Single(card => !card.IsSeries);
        movie.Year = "2030";
        await new LegacyIndexedCardSaver(repository).SaveAsync(
            movie
        );
        string indexRoot = Path.Combine(
            fixture.Root,
            "interface",
            "00"
        );
        Assert.Empty(
            Directory.EnumerateFiles(
                indexRoot,
                "dune_folder.txt",
                SearchOption.AllDirectories
            )
        );
        Assert.False(movie.IsDirty);
        ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        Assert.False(
            Directory.Exists(
                Path.Combine(
                    fixture.Destination,
                    "00",
                    "09",
                    "2030"
                )
            )
        );
        DuneExporter.Process(
            new MediaContext(movie),
            ExportFixture.Template(),
            new RenderOptions(),
            fixture.Root
        );
        Assert.True(
            File.Exists(
                Path.Combine(
                    indexRoot,
                    "09",
                    "2030",
                    "dune_folder.txt"
                )
            )
        );
        Assert.False(
            Directory.Exists(
                Path.Combine(indexRoot, "09", "2020")
            )
        );
    }

    [Fact]
    public async Task SaveAsync_IndexAvecDeuxFilms_RetireSeulementLeLibelleEtRenumerote()
    {
        using ExportFixture fixture = new();
        string moviePath = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "data.xml"
        );
        XDocument version = XDocument.Load(moviePath);
        const string OtherName = "Les Choristes (2004) [FR].mkv";
        version
            .Root!.Element("item")!
            .Element("filename")!
            .Value = OtherName;
        version
            .Root.Element("info")!
            .Element("sort_title")!
            .Value = "Les Choristes";
        string directory = Path.Combine(
            fixture.Root,
            "data",
            OtherName
        );
        Directory.CreateDirectory(directory);
        version.Save(Path.Combine(directory, "data.xml"));
        fixture.ProcessIndexes();
        using WorkspaceRepository repository = new();
        MediaCard movie = repository
            .Open(fixture.Root)
            .Cards.Single(card =>
                card.FolderName == ExportFixture.MovieName
            );
        movie.Plot = "Résumé modifié";
        await new LegacyIndexedCardSaver(repository).SaveAsync(
            movie
        );
        string content = File.ReadAllText(
            DramaIndex(fixture),
            Encoding.UTF8
        );
        Assert.Contains(
            "item.0.caption=Les Choristes",
            content,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "item.0.media_url=../../99/" + OtherName,
            content,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "item.1.",
            content,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "Été d’Adèle",
            content,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task SaveAsync_SansModification_PreserveLesIndex()
    {
        using ExportFixture fixture = new();
        fixture.ProcessIndexes();
        byte[] original = File.ReadAllBytes(DramaIndex(fixture));
        using WorkspaceRepository repository = new();
        MediaCard movie = repository
            .Open(fixture.Root)
            .Cards.Single(card => !card.IsSeries);
        await new LegacyIndexedCardSaver(repository).SaveAsync(
            movie
        );
        Assert.Equal(
            original,
            File.ReadAllBytes(DramaIndex(fixture))
        );
    }

    [Fact]
    public async Task SaveAsync_ConflitSurLeXml_PreserveLesIndex()
    {
        using ExportFixture fixture = new();
        fixture.ProcessIndexes();
        byte[] original = File.ReadAllBytes(DramaIndex(fixture));
        using WorkspaceRepository repository = new();
        MediaCard movie = repository
            .Open(fixture.Root)
            .Cards.Single(card => !card.IsSeries);
        movie.Title = "Titre modifié";
        await File.AppendAllTextAsync(
            movie.FilePath,
            "\n",
            Encoding.UTF8
        );
        await Assert.ThrowsAsync<IOException>(() =>
            new LegacyIndexedCardSaver(repository).SaveAsync(
                movie
            )
        );
        Assert.Equal(
            original,
            File.ReadAllBytes(DramaIndex(fixture))
        );
    }

    private static string DramaIndex(ExportFixture fixture) =>
        Path.Combine(
            fixture.Root,
            "interface",
            "00",
            "04",
            "Drame",
            "dune_folder.txt"
        );
}
