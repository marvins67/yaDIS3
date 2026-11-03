using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;

namespace YaDIS3.Desktop.Tests;

public sealed class ConfigurationViewModelTests
{
    private static ConfigurationSnapshot Snapshot() =>
        new(
            new(),
            [],
            XDocument.Parse(
                "<groups><selections><selection enabled='yes'><name>Famille</name><movie>Wall·E.iso</movie></selection></selections><sagas><saga><name>Le Hobbit</name></saga></sagas></groups>"
            ),
            [new("Famille", 0)]
        );

    [Fact]
    public void Snapshot_Changements_NeModifiePasLaConfigurationActive()
    {
        ConfigurationSnapshot original = Snapshot();
        using ConfigurationViewModel draft = new(
            original,
            "/workspace",
            [],
            [],
            (_, _, _) =>
                throw new InvalidOperationException(
                    "Ne doit pas enregistrer"
                )
        );
        draft.Selections[0].Enabled = false;
        draft.BasicMode = true;
        draft.Articles = "le, la, l'";
        ConfigurationSnapshot actual = draft.Snapshot();
        Assert.Equal(
            "no",
            actual
                .Groups.Descendants("selection")
                .Single()
                .Attribute("enabled")!
                .Value
        );
        Assert.Equal(
            "yes",
            original
                .Groups.Descendants("selection")
                .Single()
                .Attribute("enabled")!
                .Value
        );
        Assert.Equal(
            "Wall·E.iso",
            actual.Groups.Descendants("movie").Single().Value
        );
        Assert.Equal(
            "Le Hobbit",
            actual
                .Groups.Descendants("saga")
                .Single()
                .Element("name")!
                .Value
        );
        Assert.False(original.Settings.BasicMode);
        Assert.True(actual.Settings.BasicMode);
        Assert.Equal("le,la,l'", actual.Settings.Articles);
    }

    [Fact]
    public async Task ApplyAsync_Reglages_TransmetLesOptionsAuService()
    {
        ConfigurationSnapshot? saved = null;
        using ConfigurationViewModel draft = new(
            Snapshot(),
            "/workspace",
            [],
            [],
            (value, _, _) =>
            {
                saved = value;
                return Task.CompletedTask;
            }
        );
        draft.Mappings = "/Volumes/Films => smb://nas/Films";
        draft.JpegQuality = 78;
        draft.LastDays = 30;
        draft.RecentSortIndex = 1;
        draft.Categories[2].Enabled = false;
        Assert.True(await draft.ApplyAsync());
        Assert.Equal(
            "/Volumes/Films => smb://nas/Films",
            saved!.Settings.Mappings
        );
        Assert.Equal(78, saved.Settings.JpegQuality);
        Assert.Equal(30, saved.Settings.LastDays);
        Assert.True(saved.Settings.SortRecentByDate);
        Assert.Equal('0', saved.Settings.Categories[3]);
        Assert.False(draft.IsBusy);
    }

    [Fact]
    public async Task ApplyAsync_EchecEcriture_LaisseLeDialogueOuvertAvecErreur()
    {
        using ConfigurationViewModel draft = new(
            Snapshot(),
            "/workspace",
            [],
            [],
            (_, _, _) =>
                throw new IOException("Disque indisponible")
        );
        Assert.False(await draft.ApplyAsync());
        Assert.Equal("Disque indisponible", draft.Message);
        Assert.False(draft.IsBusy);
    }

    [Fact]
    public void AddShortcut_TypesEtDoublons_ConserveOrdreEtChampsDuProgrammeOriginal()
    {
        using ConfigurationViewModel draft = new(
            Snapshot(),
            "/workspace",
            [],
            [],
            (_, _, _) => Task.CompletedTask
        );
        draft.ShortcutSelection = "Famille";
        draft.AddShortcut();
        draft.ShortcutType = 1;
        draft.ShortcutGenre = "Animation";
        draft.AddShortcut();
        draft.ShortcutType = 2;
        draft.ShortcutName = "NAS";
        draft.ShortcutPath = "smb://nas/Films";
        draft.AddShortcut();
        draft.MoveShortcut(3, 0);
        Assert.Equal(
            new DuneShortcut("NAS", 2, "smb://nas/Films"),
            draft.Shortcuts[0]
        );
        Assert.Equal(
            2,
            draft.Shortcuts.Count(value =>
                value.Name == "Famille"
            )
        );
        draft.RemoveShortcuts([draft.Shortcuts[2]]);
        Assert.Equal(3, draft.Shortcuts.Count);
    }

    [Fact]
    public void CanAccept_ModeBasique_RendLeCheminDuneFacultatif()
    {
        SourceFolderViewModel normal = new(
            new("/Volumes/Films", 0, "")
        );
        SourceFolderViewModel basic = new(
            new("/Volumes/Films", 0, "")
        )
        {
            BasicMode = true,
        };
        Assert.False(normal.CanAccept);
        Assert.True(basic.CanAccept);
        normal.DunePath = "smb://nas/Films";
        Assert.True(normal.CanAccept);
    }

    [Fact]
    public void ChooseWorkspace_PremierLancement_PreserveLesDocumentsExistants()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "yadis3-config-" + Guid.NewGuid().ToString("N"),
            "workspace"
        );
        try
        {
            new DuneConfigurationStore().Save(
                directory,
                Snapshot() with
                {
                    Settings = new() { JpegQuality = 77 },
                }
            );
            using ConfigurationViewModel draft = new(
                new(new(), [], new(new XElement("groups")), []),
                "",
                [],
                [],
                (_, _, _) => Task.CompletedTask
            );
            draft.ChooseWorkspace(directory);
            Assert.Equal(77, draft.JpegQuality);
            Assert.Equal(
                "Famille",
                Assert.Single(draft.Selections).Name
            );
            Assert.Single(draft.Shortcuts);
            Assert.Equal(
                "Wall·E.iso",
                draft
                    .Snapshot()
                    .Groups.Descendants("movie")
                    .Single()
                    .Value
            );
        }
        finally
        {
            Directory.Delete(
                Path.GetDirectoryName(directory)!,
                true
            );
        }
    }

    [Fact]
    public async Task ApplyAsync_DepuisFenetrePrincipale_RaccordeModeBasiqueEtDossiers()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "yadis3-config-main-" + Guid.NewGuid().ToString("N")
        );
        string source = Path.Combine(root, "Films");
        Directory.CreateDirectory(
            Path.Combine(source, "Le Château (2023)")
        );
        try
        {
            using MainWindowViewModel main = new();
            await main.OpenAsync(
                Path.Combine(root, "workspace"),
                create: true
            );
            using ConfigurationViewModel draft =
                main.CreateConfiguration();
            draft.BasicMode = true;
            draft.JpegQuality = 77;
            draft.UpdateFolder(
                new(new(source, 0, "")) { BasicMode = true }
            );
            Assert.True(await draft.ApplyAsync(), draft.Message);
            Assert.False(main.CanExport);
            Assert.Equal(
                "Le Château (2023)",
                Assert.Single(main.Cards).DisplayName
            );
            ConfigurationSnapshot stored =
                new DuneConfigurationStore().Load(
                    main.WorkspacePath
                );
            Assert.True(stored.Settings.BasicMode);
            Assert.Equal(77, stored.Settings.JpegQuality);
            Assert.Equal(
                source,
                Assert.Single(stored.Folders).Path
            );
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task AddManualSeriesAsync_SourcesConfigurees_ResteVisibleApresReouverture()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "yadis3-manual-source-"
                + Guid.NewGuid().ToString("N")
        );
        string workspace = Path.Combine(root, "workspace");
        string videos = Path.Combine(root, "Série manuelle");
        Directory.CreateDirectory(videos);
        File.WriteAllBytes(
            Path.Combine(videos, "S01E01.mkv"),
            []
        );
        try
        {
            using MainWindowViewModel main = new();
            await main.OpenAsync(workspace, create: true);
            new DuneConfigurationStore().SaveSettings(
                workspace,
                new() { SourceFoldersConfigured = true }
            );
            await main.OpenAsync(workspace);
            await main.AddManualSeriesAsync(videos);
            await main.OpenAsync(workspace);
            Assert.Equal(
                "Série manuelle",
                Assert.Single(main.Cards).DisplayName
            );
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
