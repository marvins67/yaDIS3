using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class DuneConfigurationTests
{
    private static ConfigurationSnapshot Snapshot() =>
        new(
            new(),
            [
                new("/Volumes/Films", 0, "smb://nas/Films"),
                new("/Volumes/Séries", 1, "smb://nas/Series"),
            ],
            XDocument.Parse(
                "<groups extension='conserver'><selections><selection enabled='yes'><name>Famille</name><movie>Wall·E (2008).iso</movie></selection></selections><sagas><saga><name>Le Hobbit</name></saga></sagas></groups>"
            ),
            [
                new("Famille", 0),
                new("Science-Fiction", 1),
                new(
                    "NAS [Salon]",
                    2,
                    "smb://nas/Films & Séries"
                ),
            ]
        );

    [Fact]
    public void Load_AnciennesPreferences_GardeLesValeursEtNecritRien()
    {
        using ExportFixture fixture = new();
        const string Original =
            "{\"Template\":\"Origins\",\"Language\":\"en\",\"Aai\":false,\"Cases\":true,\"Mappings\":\"/Films => smb://nas/Films\",\"Destination\":\"/Volumes/Dune/yaDIS\"}";
        string path = Path.Combine(
            fixture.Root,
            DuneConfigurationStore.FileName
        );
        File.WriteAllText(path, Original, Encoding.UTF8);
        ConfigurationSnapshot snapshot =
            new DuneConfigurationStore().Load(fixture.Root);
        Assert.Equal(Original, File.ReadAllText(path));
        Assert.True(snapshot.Settings.Cases);
        Assert.Equal("en", snapshot.Settings.Language);
        Assert.Equal(95, snapshot.Settings.JpegQuality);
        Assert.Equal(60, snapshot.Settings.LastDays);
        Assert.False(snapshot.Settings.BasicMode);
        Assert.Equal("1111111111", snapshot.Settings.Categories);
        Assert.Empty(snapshot.Folders);
        Assert.False(
            File.Exists(
                Path.Combine(fixture.Root, "folders.xml")
            )
        );
    }

    [Fact]
    public void Save_Documents_ReproduitLesSchemasEtPreserveSelectionsEtSagas()
    {
        using ExportFixture fixture = new();
        DuneConfigurationStore store = new();
        ConfigurationSnapshot expected = Snapshot() with
        {
            Settings = new()
            {
                BasicMode = true,
                JpegQuality = 78,
                Categories = "1101010101",
                SortRecentByDate = true,
            },
        };
        store.Save(fixture.Root, expected);
        ConfigurationSnapshot actual = store.Load(fixture.Root);
        Assert.Equal(
            JsonSerializer.Serialize(expected.Settings),
            JsonSerializer.Serialize(actual.Settings)
        );
        Assert.Equal(expected.Shortcuts, actual.Shortcuts);
        Assert.True(
            XNode.DeepEquals(
                expected.Groups,
                XDocument.Parse(actual.Groups.ToString())
            )
        );
        XDocument folders = XDocument.Load(
            Path.Combine(fixture.Root, "folders.xml")
        );
        Assert.Equal(
            "0",
            folders
                .Descendants("folder")
                .First()
                .Element("type")!
                .Value
        );
        XDocument shortcuts = XDocument.Load(
            Path.Combine(fixture.Root, "shortcuts.xml")
        );
        Assert.Null(
            shortcuts
                .Descendants("shortcut")
                .First()
                .Attribute("path")
        );
        Assert.Equal(
            "smb://nas/Films & Séries",
            shortcuts
                .Descendants("shortcut")
                .Last()
                .Attribute("path")!
                .Value
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Save_EchecPartiel_RestaureExactementLesDocumentsOriginaux(
        int failedWrite
    )
    {
        using ExportFixture fixture = new();
        new DuneConfigurationStore().Save(
            fixture.Root,
            Snapshot()
        );
        string[] names =
        [
            DuneConfigurationStore.FileName,
            "folders.xml",
            "groups.xml",
            "shortcuts.xml",
        ];
        Dictionary<string, byte[]> bytes = names.ToDictionary(
            name => name,
            name =>
                File.ReadAllBytes(
                    Path.Combine(fixture.Root, name)
                )
        );
        DuneConfigurationStore store = new(
            new ConfigurationFileSystem(failedWrite)
        );
        Assert.Throws<IOException>(() =>
            store.Save(
                fixture.Root,
                Snapshot() with
                {
                    Settings = new() { JpegQuality = 12 },
                }
            )
        );
        foreach (string name in names)
        {
            Assert.Equal(
                bytes[name],
                File.ReadAllBytes(
                    Path.Combine(fixture.Root, name)
                )
            );
        }
    }

    [Fact]
    public void Save_ExtensionsInconnues_ConserveJsonEtXml()
    {
        using ExportFixture fixture = new();
        File.WriteAllText(
            Path.Combine(
                fixture.Root,
                DuneConfigurationStore.FileName
            ),
            "{\"UseRemote\":true,\"FutureOption\":{\"value\":42}}",
            Encoding.UTF8
        );
        File.WriteAllText(
            Path.Combine(fixture.Root, "folders.xml"),
            "<folders><folder extra='oui'><path>/Films</path><type>0</type><dunepath>smb://nas/Films</dunepath><inconnu>conserver</inconnu></folder></folders>",
            Encoding.UTF8
        );
        DuneConfigurationStore store = new();
        ConfigurationSnapshot snapshot = store.Load(
            fixture.Root
        );
        store.Save(
            fixture.Root,
            snapshot with
            {
                Folders =
                [
                    snapshot.Folders[0] with
                    {
                        DunePath = "smb://nas/Nouveau",
                    },
                ],
            }
        );
        ConfigurationSnapshot actual = store.Load(fixture.Root);
        Assert.True(
            actual
                .Settings.AdditionalSettings!["UseRemote"]
                .GetBoolean()
        );
        Assert.Equal(
            42,
            actual
                .Settings.AdditionalSettings["FutureOption"]
                .GetProperty("value")
                .GetInt32()
        );
        Assert.Equal(
            "oui",
            actual.Folders[0].Original!.Attribute("extra")!.Value
        );
        Assert.Equal(
            "conserver",
            actual.Folders[0].Original!.Element("inconnu")!.Value
        );
        Assert.Equal(
            "smb://nas/Nouveau",
            actual.Folders[0].DunePath
        );
    }

    [Fact]
    public void SaveSettings_Echec_RestaureLesOctetsOriginaux()
    {
        using ExportFixture fixture = new();
        new DuneConfigurationStore().SaveSettings(
            fixture.Root,
            new()
        );
        string path = Path.Combine(
            fixture.Root,
            DuneConfigurationStore.FileName
        );
        byte[] expected = File.ReadAllBytes(path);
        Assert.Throws<IOException>(() =>
            new DuneConfigurationStore(
                new ConfigurationFileSystem(1)
            ).SaveSettings(
                fixture.Root,
                new() { JpegQuality = 15 }
            )
        );
        Assert.Equal(expected, File.ReadAllBytes(path));
    }

    [Fact]
    public void Save_QualiteInvalide_RefuseAvantTouteEcriture()
    {
        using ExportFixture fixture = new();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DuneConfigurationStore().Save(
                fixture.Root,
                Snapshot() with
                {
                    Settings = new() { JpegQuality = 0 },
                }
            )
        );
        Assert.False(
            File.Exists(
                Path.Combine(
                    fixture.Root,
                    DuneConfigurationStore.FileName
                )
            )
        );
    }

    [Fact]
    public void Apply_FicheInvalide_RestaureLesPreferencesEtIdentifieLeFichier()
    {
        using ExportFixture fixture = new();
        DuneConfigurationStore store = new();
        ConfigurationSnapshot initial = store.Load(fixture.Root);
        store.Save(fixture.Root, initial);
        string data = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "data.xml"
        );
        File.WriteAllText(data, "<data />", Encoding.UTF8);
        InvalidDataException error =
            Assert.Throws<InvalidDataException>(() =>
                new DuneConfigurationService().Apply(
                    fixture.Root,
                    fixture.Root,
                    initial with
                    {
                        Settings = new() { JpegQuality = 15 },
                    }
                )
            );
        Assert.Contains(
            data,
            error.Message,
            StringComparison.Ordinal
        );
        Assert.Equal(
            95,
            store.Load(fixture.Root).Settings.JpegQuality
        );
    }

    [Theory]
    [InlineData("Le Château", "Château")]
    [InlineData("Les Misérables", "Misérables")]
    [InlineData("L'été", "Été")]
    [InlineData("The Lord of the Rings", "Lord of the Rings")]
    [InlineData("L'", "L'")]
    [InlineData("La", "La")]
    public void Create_Articles_ReproduitRemoveArticleDeYadis2(
        string title,
        string expected
    )
    {
        Assert.Equal(
            expected,
            LegacySortTitle.Create(
                title,
                new() { IgnoreArticles = true }
            )
        );
        Assert.Equal(
            title,
            LegacySortTitle.Create(title, new())
        );
    }

    [Fact]
    public void Add_SelectionAutomatique_ConserveOrdreEtEviteLesDoublons()
    {
        using ExportFixture fixture = new();
        new DuneConfigurationStore().Save(
            fixture.Root,
            Snapshot()
        );
        LegacyAutoSelection service = new();
        service.Add(
            fixture.Root,
            "Famille",
            "L’été d’Adèle [1080p].mkv"
        );
        service.Add(
            fixture.Root,
            "Famille",
            "L’été d’Adèle [1080p].mkv"
        );
        string[] expected =
        [
            "Wall·E (2008).iso",
            "L’été d’Adèle [1080p].mkv",
        ];
        Assert.Equal(
            expected,
            new DuneConfigurationStore()
                .Load(fixture.Root)
                .Groups.Descendants("movie")
                .Select(node => node.Value)
        );
    }

    [Fact]
    public void Copy_Workspace_ConserveSourceEtSauvegardeAncienneDestination()
    {
        using ExportFixture fixture = new();
        string source = Path.Combine(fixture.Root, "source");
        string target = Path.Combine(fixture.Root, "cible");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);
        File.WriteAllText(
            Path.Combine(source, "Wall·E (2008).txt"),
            "source",
            Encoding.UTF8
        );
        File.WriteAllText(
            Path.Combine(target, "ancien.txt"),
            "ancien",
            Encoding.UTF8
        );
        new WorkspaceRelocator().Copy(source, target);
        Assert.True(
            File.Exists(
                Path.Combine(source, "Wall·E (2008).txt")
            )
        );
        Assert.True(
            File.Exists(
                Path.Combine(target, "Wall·E (2008).txt")
            )
        );
        Assert.False(
            File.Exists(Path.Combine(target, "ancien.txt"))
        );
        string backup = Assert.Single(
            Directory.EnumerateDirectories(
                fixture.Root,
                "cible.previous-*"
            )
        );
        Assert.Equal(
            "ancien",
            File.ReadAllText(Path.Combine(backup, "ancien.txt"))
        );
    }

    [Fact]
    public void Copy_DossierImbrique_RefuseSansSupprimerLaSource()
    {
        using ExportFixture fixture = new();
        Assert.Throws<IOException>(() =>
            new WorkspaceRelocator().Copy(
                fixture.Root,
                Path.Combine(fixture.Root, "copie")
            )
        );
        Assert.True(Directory.Exists(fixture.Root));
    }
}
