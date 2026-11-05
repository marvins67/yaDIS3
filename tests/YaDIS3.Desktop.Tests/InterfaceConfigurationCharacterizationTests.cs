using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;

namespace YaDIS3.Desktop.Tests;

/// <summary>
/// Caractérise Configurer / Interface d'après les routines de yaDIS2 :
/// SetupForm_Load, ButtonSetupOK_Click, CheckBoxActivateRecent_CheckedChanged,
/// CheckBoxCases_CheckedChanged, RadioButtonJPG_CheckedChanged,
/// ComboBoxTemplate_SelectedIndexChanged et la gestion des raccourcis.
/// Les différences de fermeture, d'affichage immédiat et de restauration
/// sont consignées séparément : ces tests ne prétendent pas les valider.
/// </summary>
public sealed class InterfaceConfigurationCharacterizationTests
{
    private const string InitialCategories = "11111111111";
    private const string SelectionName = "Famille";
    private const string FilmName = "Le Film.iso";

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void Snapshot_OptionsBoitiersEtMenus_ConserveChaqueCombinaison(
        bool cases,
        bool selectionBox,
        bool scrollbar
    )
    {
        using ConfigurationViewModel draft = CreateDraft();
        draft.UseCases = cases;
        draft.SelectionBox = selectionBox;
        draft.Scrollbar = scrollbar;

        DuneConfiguration actual = draft.Snapshot().Settings;

        Assert.Equal(cases, actual.Cases);
        Assert.Equal(selectionBox, actual.SelectionBox);
        Assert.Equal(scrollbar, actual.Scrollbar);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 65)]
    [InlineData(false, 100)]
    [InlineData(true, 1)]
    [InlineData(true, 65)]
    [InlineData(true, 100)]
    public void Snapshot_FormatSynopsisEtQualite_ConserveLesValeurs(
        bool aai,
        int quality
    )
    {
        using ConfigurationViewModel draft = CreateDraft();
        draft.AaiBackgrounds = aai;
        draft.JpegQuality = quality;

        DuneConfiguration actual = draft.Snapshot().Settings;

        Assert.Equal(aai, actual.Aai);
        Assert.Equal(!aai, draft.JpegBackgrounds);
        Assert.Equal(quality, actual.JpegQuality);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JpegBackgrounds_ChoixRadio_ChangeLeFormatInverse(
        bool jpeg
    )
    {
        using ConfigurationViewModel draft = CreateDraft();

        draft.JpegBackgrounds = jpeg;

        Assert.Equal(jpeg, draft.JpegBackgrounds);
        Assert.Equal(!jpeg, draft.AaiBackgrounds);
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(false, 1, 60)]
    [InlineData(true, 0, 30)]
    [InlineData(true, 1, 365)]
    public void Snapshot_NouveautesEtTri_GardeLesParametresMemeMasques(
        bool enabled,
        int sortIndex,
        int days
    )
    {
        using ConfigurationViewModel draft = CreateDraft();
        draft.RecentEnabled = enabled;
        draft.RecentSortIndex = sortIndex;
        draft.LastDays = days;

        DuneConfiguration actual = draft.Snapshot().Settings;

        Assert.Equal(enabled ? '1' : '0', actual.Categories[0]);
        Assert.Equal(sortIndex == 1, actual.SortRecentByDate);
        Assert.Equal(days, actual.LastDays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Snapshot_CategorieDesactivee_ModifieUniquementSonBit(
        int index
    )
    {
        using ConfigurationViewModel draft = CreateDraft();
        draft.Categories[index].Enabled = false;

        string categories = draft.Snapshot().Settings.Categories;

        Assert.Equal(InitialCategories.Length, categories.Length);
        for (int position = 0; position < categories.Length; position++)
        {
            Assert.Equal(position == index + 1 ? '0' : '1', categories[position]);
        }
    }

    [Theory]
    [InlineData("1111111111")]
    [InlineData("11111111111")]
    public void Snapshot_LongueurHistoriqueCategories_PreserveLeFormat(
        string initial
    )
    {
        using ConfigurationViewModel draft = CreateDraft(
            new() { Categories = initial }
        );
        draft.Categories[0].Enabled = false;

        string actual = draft.Snapshot().Settings.Categories;

        Assert.Equal(initial.Length, actual.Length);
        Assert.Equal('0', actual[1]);
        Assert.Equal(initial[^1], actual[^1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Snapshot_SelectionActiveeOuNon_PreserveLesMembres(
        bool enabled
    )
    {
        ConfigurationSnapshot initial = InitialSnapshot();
        using ConfigurationViewModel draft = CreateDraft(initial);
        draft.Selections[0].Enabled = enabled;

        XDocument actual = draft.Snapshot().Groups;

        XElement selection = Assert.Single(actual.Descendants("selection"));
        Assert.Equal(
            enabled ? "yes" : "no",
            selection.Attribute("enabled")?.Value
        );
        Assert.Equal(FilmName, selection.Element("movie")?.Value);
        Assert.Equal(
            "yes",
            Assert.Single(initial.Groups.Descendants("selection"))
                .Attribute("enabled")?.Value
        );
    }

    [Fact]
    public void AddShortcut_TroisTypes_DoublonsEtOrdreSontConserves()
    {
        using ConfigurationViewModel draft = CreateDraft();

        draft.ShortcutSelection = SelectionName;
        draft.AddShortcut();
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
            [
                new DuneShortcut("NAS", 2, "smb://nas/Films"),
                new DuneShortcut(SelectionName, 0),
                new DuneShortcut(SelectionName, 0),
                new DuneShortcut("Animation", 1),
            ],
            draft.Snapshot().Shortcuts
        );
        draft.RemoveShortcuts([draft.Shortcuts[1]]);
        Assert.Equal(3, draft.Snapshot().Shortcuts.Count);
    }

    [Fact]
    public void AddShortcut_ChampsObligatoiresAbsents_NAjouteRien()
    {
        using ConfigurationViewModel draft = CreateDraft();

        draft.AddShortcut();
        draft.ShortcutType = 1;
        draft.AddShortcut();
        draft.ShortcutType = 2;
        draft.ShortcutName = "NAS";
        draft.AddShortcut();

        Assert.Empty(draft.Shortcuts);
        Assert.False(draft.CanAddShortcut);
    }

    [Fact]
    public async Task ApplyAsync_OptionsInterface_TransmetUneCopieSansChangerOriginal()
    {
        ConfigurationSnapshot initial = InitialSnapshot();
        ConfigurationSnapshot? saved = null;
        using ConfigurationViewModel draft = CreateDraft(
            initial,
            value => saved = value
        );
        draft.UseCases = true;
        draft.JpegBackgrounds = true;
        draft.JpegQuality = 83;
        draft.SelectionBox = false;
        draft.Scrollbar = false;
        draft.RecentEnabled = false;
        draft.Categories[2].Enabled = false;
        draft.Selections[0].Enabled = false;

        Assert.True(await draft.ApplyAsync());
        Assert.NotNull(saved);
        Assert.True(saved.Settings.Cases);
        Assert.False(saved.Settings.Aai);
        Assert.Equal(83, saved.Settings.JpegQuality);
        Assert.False(saved.Settings.SelectionBox);
        Assert.False(saved.Settings.Scrollbar);
        Assert.Equal('0', saved.Settings.Categories[0]);
        Assert.Equal('0', saved.Settings.Categories[3]);
        Assert.Equal(
            "no",
            Assert.Single(saved.Groups.Descendants("selection"))
                .Attribute("enabled")?.Value
        );
        Assert.False(initial.Settings.Cases);
        Assert.Equal(
            InitialCategories,
            initial.Settings.Categories
        );
        Assert.Equal(
            "yes",
            Assert.Single(initial.Groups.Descendants("selection"))
                .Attribute("enabled")?.Value
        );
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Save_OptionsInterface_RetrouveLesValeursDansUserConfig(
        bool aai,
        bool cases,
        bool selectionBox
    )
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "yadis-interface-" + Guid.NewGuid().ToString("N")
        );
        try
        {
            DuneConfigurationStore store = new(
                userConfigurationPath: Path.Combine(
                    directory,
                    "user.config"
                )
            );
            ConfigurationSnapshot snapshot = InitialSnapshot() with
            {
                Settings = new()
                {
                    Aai = aai,
                    Cases = cases,
                    SelectionBox = selectionBox,
                    Scrollbar = false,
                    JpegQuality = 72,
                    Categories = "01010101010",
                    LastDays = 31,
                    SortRecentByDate = true,
                },
            };
            store.Save(directory, snapshot);

            ConfigurationSnapshot loaded = store.Load(directory);
            Assert.Equal(aai, loaded.Settings.Aai);
            Assert.Equal(cases, loaded.Settings.Cases);
            Assert.Equal(selectionBox, loaded.Settings.SelectionBox);
            Assert.False(loaded.Settings.Scrollbar);
            Assert.Equal(72, loaded.Settings.JpegQuality);
            Assert.Equal("01010101010", loaded.Settings.Categories);
            Assert.Equal(31, loaded.Settings.LastDays);
            Assert.True(loaded.Settings.SortRecentByDate);
            Assert.Equal(
                FilmName,
                Assert.Single(loaded.Groups.Descendants("movie")).Value
            );
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_QualiteJpegInvalide_RefuseAvantEcriture(
        int quality
    )
    {
        ConfigurationSnapshot invalid = InitialSnapshot() with
        {
            Settings = new() { JpegQuality = quality },
        };
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DuneConfigurationStore.Validate(invalid)
        );
    }

    [Fact]
    public async Task ApplyAsync_EchecEnregistrement_NeModifiePasLeSnapshotSource()
    {
        ConfigurationSnapshot initial = InitialSnapshot();
        using ConfigurationViewModel draft = new(
            initial,
            "/workspace",
            [],
            [],
            (_, _, _) => throw new IOException("Accès refusé")
        );
        draft.UseCases = true;
        draft.Selections[0].Enabled = false;

        Assert.False(await draft.ApplyAsync());

        Assert.False(initial.Settings.Cases);
        Assert.Equal(
            "yes",
            Assert.Single(initial.Groups.Descendants("selection"))
                .Attribute("enabled")?.Value
        );
        Assert.False(draft.IsBusy);
        Assert.Equal("Accès refusé", draft.Message);
    }

    private static ConfigurationSnapshot InitialSnapshot() =>
        new(
            new() { Categories = InitialCategories },
            [],
            XDocument.Parse(
                "<groups><selections><selection enabled='yes'>"
                + "<name>Famille</name><movie>Le Film.iso</movie>"
                + "</selection></selections></groups>"
            ),
            []
        );

    private static ConfigurationViewModel CreateDraft(
        DuneConfiguration? settings = null
    ) => CreateDraft(
        InitialSnapshot() with
        {
            Settings = settings ?? new(),
        }
    );

    private static ConfigurationViewModel CreateDraft(
        ConfigurationSnapshot snapshot,
        Action<ConfigurationSnapshot>? save = null
    ) =>
        new(
            snapshot,
            "/workspace",
            [],
            ["Animation"],
            (value, _, _) =>
            {
                save?.Invoke(value);
                return Task.CompletedTask;
            }
        );
}
