using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyMediaDeletionTests
{
    [Fact]
    public void Delete_Film_NettoieGroupesRenduEtConserveSource()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        MediaCard movie = repository.Open(fixture.Root).Cards
            .Single(card => !card.IsSeries);
        string source = Path.Combine(fixture.Root, "videos", "original.mkv");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, "video");
        string saga = "Collection";
        XDocument groups = new(
            new XElement("groups",
                new XElement("sagas",
                    new XElement("saga",
                        new XElement("name", saga),
                        new XElement("poster", movie.FolderName),
                        new XElement("fanart", movie.FolderName),
                        new XElement("movie", movie.FolderName)
                    )
                ),
                new XElement("selections",
                    new XElement("selection",
                        new XAttribute("enabled", "yes"),
                        new XElement("name", "Favoris"),
                        new XElement("movie", movie.FolderName)
                    )
                )
            )
        );
        DuneConfigurationStore store = fixture.ConfigurationStore();
        store.SaveGroups(fixture.Root, groups);
        string movieRender = Path.Combine(
            fixture.Root, "interface", "00", "99", movie.FolderName
        );
        string sagaRender = Path.Combine(
            fixture.Root, "interface", "00", "98", saga
        );
        Directory.CreateDirectory(movieRender);
        Directory.CreateDirectory(sagaRender);

        LegacyMediaDeletionResult result = new LegacyMediaDeletion(
            store
        ).Delete(fixture.Root, [movie]);

        Assert.Empty(result.Errors);
        Assert.Equal(movie.FilePath, Assert.Single(result.DeletedDataPaths));
        Assert.False(Directory.Exists(movie.DataDirectory));
        Assert.False(Directory.Exists(movieRender));
        Assert.False(Directory.Exists(sagaRender));
        Assert.True(File.Exists(source));
        XDocument remaining = XDocument.Load(
            Path.Combine(fixture.Root, "groups.xml")
        );
        Assert.Empty(remaining.Descendants("saga"));
        Assert.Empty(remaining.Descendants("selection"));
    }

    [Fact]
    public void Delete_SelectionMixte_NettoieChaqueType()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        MediaCard[] cards = repository.Open(fixture.Root).Cards.ToArray();
        MediaCard movie = cards.Single(item => !item.IsSeries);
        MediaCard series = cards.Single(item => item.IsSeries);
        string seriesRender = Path.Combine(
            fixture.Root, "interface", "01", series.FolderName
        );
        Directory.CreateDirectory(seriesRender);
        DuneConfigurationStore store = fixture.ConfigurationStore();
        LegacyMediaDeletionResult result = new LegacyMediaDeletion(
            store
        ).Delete(fixture.Root, cards);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.DeletedDataPaths.Count);
        Assert.False(File.Exists(movie.FilePath));
        Assert.False(File.Exists(series.FilePath));
        Assert.False(Directory.Exists(seriesRender));
    }

    [Fact]
    public void Delete_SagaAvecAutreFilm_ActualiseLesReferences()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        MediaCard movie = repository.Open(fixture.Root).Cards
            .Single(item => !item.IsSeries);
        string remaining = "Autre.mkv";
        XDocument groups = new(
            new XElement("groups",
                new XElement("sagas",
                    new XElement("saga",
                        new XElement("name", "Saga"),
                        new XElement("poster", movie.FolderName),
                        new XElement("fanart", movie.FolderName),
                        new XElement("movie", movie.FolderName),
                        new XElement("movie", remaining)
                    )
                )
            )
        );
        DuneConfigurationStore store = fixture.ConfigurationStore();
        store.SaveGroups(fixture.Root, groups);

        LegacyMediaDeletionResult result = new LegacyMediaDeletion(
            store
        ).Delete(fixture.Root, [movie]);

        Assert.Empty(result.Errors);
        XElement saga = Assert.Single(
            XDocument.Load(Path.Combine(fixture.Root, "groups.xml"))
                .Descendants("saga")
        );
        Assert.Equal(remaining, saga.Element("poster")?.Value);
        Assert.Equal(remaining, saga.Element("fanart")?.Value);
        Assert.Equal(remaining, saga.Element("movie")?.Value);
    }

    [Fact]
    public void Delete_FicheExterieure_RefuseLaSuppression()
    {
        using ExportFixture first = new();
        using ExportFixture second = new();
        using WorkspaceRepository repository = new();
        MediaCard movie = repository.Open(first.Root).Cards
            .Single(item => !item.IsSeries);

        Assert.Throws<InvalidDataException>(() =>
            new LegacyMediaDeletion(second.ConfigurationStore())
                .Delete(second.Root, [movie])
        );
        Assert.True(File.Exists(movie.FilePath));
    }
    [Fact]
    public void Delete_FilmIndexe_RetireLesCriteresDeLInterface()
    {
        using ExportFixture fixture = new();
        fixture.ProcessIndexes();
        using WorkspaceRepository repository = new();
        MediaCard movie = repository.Open(fixture.Root).Cards
            .Single(card => !card.IsSeries);
        string indexRoot = Path.Combine(fixture.Root, "interface", "00");
        Assert.NotEmpty(Directory.EnumerateFiles(
            indexRoot, "dune_folder.txt", SearchOption.AllDirectories
        ));

        LegacyMediaDeletionResult result = new LegacyMediaDeletion(
            fixture.ConfigurationStore()
        ).Delete(fixture.Root, [movie]);

        Assert.Empty(result.Errors);
        Assert.Empty(Directory.EnumerateFiles(
            indexRoot, "dune_folder.txt", SearchOption.AllDirectories
        ));
    }

}
