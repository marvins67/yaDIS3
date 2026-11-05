using System.Xml.Linq;
using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

/// <summary>
/// Caractérise les options de navigation exportées par yaDIS2
/// pour Configurer, Interface : cases de navigation, catégories,
/// nouveautés, période et tri.
/// </summary>
public sealed class LegacyInterfaceMenusCharacterizationTests
{
    private const string AllCategories = "1111111111";
    private const string MovieCaption = "Été d’Adèle";

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Export_OptionsNavigation_EcritLesDeuxValeurs(
        bool selectionBox,
        bool scrollbar
    )
    {
        using ExportFixture fixture = new();
        ExportResult result = Export(
            fixture,
            new()
            {
                SelectionBox = selectionBox,
                Scrollbar = scrollbar,
            }
        );

        string root = Read(result, "dune_folder.txt");
        Assert.Contains(
            "all_children.paint_icon_selection_box="
                + (selectionBox ? "yes" : "no"),
            root,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "all_children.paint_scrollbar="
                + (scrollbar ? "yes" : "no"),
            root,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData(0, "02")]
    [InlineData(1, "03")]
    [InlineData(2, "04")]
    [InlineData(3, "05")]
    [InlineData(4, "06")]
    [InlineData(5, "07")]
    [InlineData(6, "08")]
    [InlineData(7, "09")]
    [InlineData(8, "10")]
    [InlineData(9, "11")]
    public void Export_CategorieDesactivee_OmetUniquementCeMenu(
        int categoryBit,
        string disabledFolder
    )
    {
        using ExportFixture fixture = new();
        fixture.ProcessIndexes();
        char[] categories = AllCategories.ToCharArray();
        categories[categoryBit] = '0';

        ExportResult result = Export(
            fixture,
            new()
            {
                Categories = new string(categories),
            }
        );

        Assert.False(
            Directory.Exists(
                Path.Combine(
                    result.Directory,
                    "00",
                    disabledFolder
                )
            )
        );
        Assert.True(
            Directory.Exists(
                Path.Combine(result.Directory, "00", "00")
            )
        );
        string other = disabledFolder == "03" ? "04" : "03";
        Assert.True(
            Directory.Exists(
                Path.Combine(result.Directory, "00", other)
            )
        );
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(60, true)]
    public void Export_PeriodeNouveautes_RespecteJourLimite(
        int days,
        bool included
    )
    {
        using ExportFixture fixture = new();
        ExportResult result = Export(
            fixture,
            new() { LastDays = days }
        );

        string recent = Read(
            result,
            "00",
            "02",
            "dune_folder.txt"
        );
        Assert.Equal(
            included,
            recent.Contains(
                "item.0.caption=" + MovieCaption,
                StringComparison.Ordinal
            )
        );
    }

    [Theory]
    [InlineData(false, "item.0.caption=Été d’Adèle")]
    [InlineData(true, "item.0.caption=001_Été d’Adèle")]
    public void Export_TriNouveautes_PreserveLibelleHistorique(
        bool byDate,
        string caption
    )
    {
        using ExportFixture fixture = new();
        ExportResult result = Export(
            fixture,
            new() { SortRecentByDate = byDate }
        );

        Assert.Contains(
            caption,
            Read(result, "00", "02", "dune_folder.txt"),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Export_NouveautesDesactivees_ConserveLaListeFilms()
    {
        using ExportFixture fixture = new();
        ExportResult result = Export(
            fixture,
            new() { Categories = "0111111111" }
        );

        Assert.False(
            Directory.Exists(
                Path.Combine(result.Directory, "00", "02")
            )
        );
        Assert.Contains(
            "item.0.caption=" + MovieCaption,
            Read(result, "00", "00", "dune_folder.txt"),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Export_FilmHorsPeriode_ResteDansLaListeGenerale()
    {
        using ExportFixture fixture = new();
        string data = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "data.xml"
        );
        XDocument document = XDocument.Load(data);
        document.Root!
            .Element("item")!
            .Element("date")!
            .Value = "2026-10-01";
        document.Save(data);

        ExportResult result = Export(
            fixture,
            new() { LastDays = 1 }
        );

        Assert.DoesNotContain(
            "item.0.caption=" + MovieCaption,
            Read(result, "00", "02", "dune_folder.txt"),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "item.0.caption=" + MovieCaption,
            Read(result, "00", "00", "dune_folder.txt"),
            StringComparison.Ordinal
        );
    }

    private static ExportResult Export(
        ExportFixture fixture,
        LegacyCatalogOptions options
    ) =>
        new DuneExporter(
            ExportFixture.Template(),
            new RenderOptions(),
            [],
            catalogOptions: options,
            timeProvider: new FixedTimeProvider()
        ).Export(fixture.Cards(), fixture.Destination);

    private static string Read(
        ExportResult result,
        params string[] segments
    ) =>
        File.ReadAllText(
            Path.Combine(
                result.Directory,
                Path.Combine(segments)
            )
        );
}
