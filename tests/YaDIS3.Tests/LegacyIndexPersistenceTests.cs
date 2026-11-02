using System.Text;
using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyIndexPersistenceTests
{
    // bgwExport ne rappelle pas AddToIndex pour les genres, personnes et critères techniques.
    [Fact]
    public void Export_SansTraitement_NeReconstruitPasLesIndex()
    {
        using ExportFixture fixture = new();
        ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        Assert.False(
            Directory.Exists(
                Index(fixture.Destination, "04", "Drame")
            )
        );
        Assert.False(
            Directory.Exists(
                Index(
                    fixture.Root,
                    "04",
                    "Drame",
                    workspace: true
                )
            )
        );
        Assert.True(
            File.Exists(
                Path.Combine(
                    fixture.Destination,
                    "00",
                    "03",
                    "É",
                    "dune_folder.txt"
                )
            )
        );
    }

    // SyncInterface copie les anciens fichiers, y compris les index devenus incomplets.
    [Fact]
    public void Export_IndexImporte_PreserveLesOctetsEtIgnoreLesMetadonneesModifiees()
    {
        using ExportFixture fixture = new();
        string source = Index(
            fixture.Root,
            "04",
            "Drame",
            workspace: true
        );
        Directory.CreateDirectory(source);
        byte[] original = LegacyDuneEncoding.Encode(
            "background_path=../../00/background.aai|item.7.caption=Ancien titre|item.7.media_action=browse|item.7.media_url=../../99/Ancienne version (1984).mkv",
            true
        );
        File.WriteAllBytes(
            Path.Combine(source, "dune_folder.txt"),
            original
        );
        byte[] icon = [0, 1, 2, 3];
        File.WriteAllBytes(
            Path.Combine(source, "icon.aai"),
            icon
        );
        string excluded = Path.Combine(source, "_archive");
        Directory.CreateDirectory(excluded);
        File.WriteAllText(
            Path.Combine(excluded, "dune_folder.txt"),
            "ne pas exporter",
            Encoding.UTF8
        );
        DuneExporter exporter = ExportFixture.Exporter();
        exporter.Export(fixture.Cards(), fixture.Destination);
        ChangeMovie(fixture, "Comédie", "Titre modifié");
        exporter.Export(fixture.Cards(), fixture.Destination);
        string output = Index(
            fixture.Destination,
            "04",
            "Drame"
        );
        Assert.Equal(
            original,
            File.ReadAllBytes(
                Path.Combine(output, "dune_folder.txt")
            )
        );
        Assert.Equal(
            icon,
            File.ReadAllBytes(Path.Combine(output, "icon.aai"))
        );
        Assert.Equal(
            original,
            File.ReadAllBytes(
                Path.Combine(source, "dune_folder.txt")
            )
        );
        Assert.False(
            Directory.Exists(
                Index(fixture.Destination, "04", "Comédie")
            )
        );
        Assert.False(
            Directory.Exists(Path.Combine(output, "_archive"))
        );
    }

    // CreateIndexItem conserve la première version ajoutée, indépendamment de l'ordre d'export.
    [Fact]
    public void Update_VersionsDeMemeTitre_ConserveLaPremiereApresReouverture()
    {
        using ExportFixture fixture = new();
        MediaContext original = fixture
            .Cards()
            .Single(card => !card.IsSeries);
        XDocument version = XDocument.Load(
            Path.Combine(original.DataDirectory, "data.xml")
        );
        const string FirstVersion =
            "L’été d’Adèle [version longue].mkv";
        version
            .Root!.Element("item")!
            .Element("filename")!
            .Value = FirstVersion;
        string directory = Path.Combine(
            fixture.Root,
            "data",
            FirstVersion
        );
        Directory.CreateDirectory(directory);
        version.Save(Path.Combine(directory, "data.xml"));
        MediaContext first = fixture
            .Cards()
            .Single(card =>
                card.Header.Element("filename")!.Value
                == FirstVersion
            );
        new LegacyMovieIndexStore(
            ExportFixture.Template(),
            new RenderOptions()
        ).Update(first, fixture.Root);
        new LegacyMovieIndexStore(
            ExportFixture.Template(),
            new RenderOptions()
        ).Update(original, fixture.Root);
        string index = Path.Combine(
            Index(fixture.Root, "04", "Drame", workspace: true),
            "dune_folder.txt"
        );
        string content = File.ReadAllText(index, Encoding.UTF8);
        Assert.Contains(
            "item.0.media_url=../../99/" + FirstVersion,
            content,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "item.1.caption=",
            content,
            StringComparison.Ordinal
        );
        ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        Assert.Equal(
            File.ReadAllBytes(index),
            File.ReadAllBytes(
                Path.Combine(
                    Index(fixture.Destination, "04", "Drame"),
                    "dune_folder.txt"
                )
            )
        );
    }

    // AddToIndex ajoute les nouveaux critères sans purger les autres dossiers.
    [Fact]
    public void Update_TitreEtGenreModifies_PreserveLesIndexPreexistants()
    {
        using ExportFixture fixture = new();
        fixture.ProcessIndexes();
        string old = Path.Combine(
            Index(fixture.Root, "04", "Drame", workspace: true),
            "dune_folder.txt"
        );
        byte[] original = File.ReadAllBytes(old);
        ChangeMovie(fixture, "Comédie", "Nouveau titre");
        fixture.ProcessIndexes();
        Assert.Equal(original, File.ReadAllBytes(old));
        string added = Path.Combine(
            Index(
                fixture.Root,
                "04",
                "Comédie",
                workspace: true
            ),
            "dune_folder.txt"
        );
        Assert.Contains(
            "item.0.caption=Nouveau titre",
            File.ReadAllText(added, Encoding.UTF8),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Process_EchecDuRendu_PreserveLesIndexExistants()
    {
        using ExportFixture fixture = new();
        fixture.ProcessIndexes();
        string index = Path.Combine(
            Index(fixture.Root, "04", "Drame", workspace: true),
            "dune_folder.txt"
        );
        byte[] original = File.ReadAllBytes(index);
        ChangeMovie(fixture, "Comédie", "Nouveau titre");
        MediaContext movie = fixture
            .Cards()
            .Single(card => !card.IsSeries);
        File.WriteAllBytes(
            Path.Combine(movie.DataDirectory, "fanart.jpg"),
            [1, 2, 3]
        );
        Assert.Throws<InvalidDataException>(() =>
            DuneExporter.Process(
                movie,
                ExportFixture.Template(),
                new RenderOptions(),
                fixture.Root
            )
        );
        Assert.Equal(original, File.ReadAllBytes(index));
        Assert.False(
            Directory.Exists(
                Index(
                    fixture.Root,
                    "04",
                    "Comédie",
                    workspace: true
                )
            )
        );
    }

    [Fact]
    public void Update_SerieOuAnnulation_NeCreePasDIndex()
    {
        using ExportFixture fixture = new();
        LegacyMovieIndexStore store = new(
            ExportFixture.Template(),
            new RenderOptions()
        );
        store.Update(
            fixture.Cards().Single(card => card.IsSeries),
            fixture.Root
        );
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            store.Update(
                fixture.Cards().Single(card => !card.IsSeries),
                fixture.Root,
                cancellation.Token
            )
        );
        Assert.False(
            Directory.Exists(
                Path.Combine(fixture.Root, "interface")
            )
        );
    }

    [Theory]
    [InlineData(false, "background.jpg")]
    [InlineData(true, "background.aai")]
    public void Update_FondSelonOption_ConserveLaReferenceHistorique(
        bool aai,
        string background
    )
    {
        using ExportFixture fixture = new();
        LegacyMovieIndexStore store = new(
            ExportFixture.Template(),
            new RenderOptions(AaiBackgrounds: aai)
        );
        store.Update(
            fixture.Cards().Single(card => !card.IsSeries),
            fixture.Root
        );
        string path = Path.Combine(
            Index(fixture.Root, "04", "Drame", workspace: true),
            "dune_folder.txt"
        );
        Assert.Contains(
            "background_path=../../00/" + background,
            File.ReadAllText(path, Encoding.UTF8),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Update_IndexImporte_ConserveLaVersionEtContinueLaNumerotation()
    {
        using ExportFixture fixture = new();
        string directory = Index(
            fixture.Root,
            "04",
            "Drame",
            workspace: true
        );
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "dune_folder.txt");
        File.WriteAllBytes(
            path,
            LegacyDuneEncoding.Encode(
                "item.7.caption=Été d’Adèle|item.7.media_action=browse|item.7.media_url=../../99/Ancienne version [1080p].mkv|item.7.icon_scale_factor=0.5",
                true
            )
        );
        fixture.ProcessIndexes();
        string content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains(
            "item.7.media_url=../../99/Ancienne version [1080p].mkv",
            content,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "item.7.icon_scale_factor=0.85",
            content,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "item.7.icon_sel_scale_factor=1",
            content,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "item.8.caption=",
            content,
            StringComparison.Ordinal
        );
        ChangeMovie(fixture, "Drame", "Nouveau titre");
        fixture.ProcessIndexes();
        Assert.Contains(
            "item.8.caption=Nouveau titre",
            File.ReadAllText(path, Encoding.UTF8),
            StringComparison.Ordinal
        );
    }

    private static string Index(
        string root,
        string category,
        string name,
        bool workspace = false
    )
    {
        return Path.Combine(
            workspace ? Path.Combine(root, "interface") : root,
            "00",
            category,
            name
        );
    }

    private static void ChangeMovie(
        ExportFixture fixture,
        string genre,
        string title
    )
    {
        string path = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "data.xml"
        );
        XDocument data = XDocument.Load(path);
        XElement info = data.Root!.Element("info")!;
        info.Element("genre")!.Value = genre;
        info.Element("sort_title")!.Value = title;
        data.Save(path);
    }
}
