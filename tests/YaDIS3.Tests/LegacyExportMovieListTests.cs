using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyExportMovieListTests
{
    [Fact]
    public void Export_SagaDejaEcrite_ConsommeSonIndiceDansLOrdreDesFiches()
    {
        using ExportFixture fixture = new();
        MediaContext[] cards = fixture.Cards();
        MediaContext[] movies =
        [
            Movie(fixture, "Z (1).mkv", "Premier"),
            cards.Single(card => card.IsSeries),
            Movie(fixture, "inédit.mkv", "Inédit", "3"),
            Movie(fixture, "Y (2).mkv", "Suite"),
            Movie(fixture, "A.mkv", "A"),
        ];
        WriteSaga(
            fixture, "La Trilogie", "Trilogie (tri)",
            "Y (2).mkv", "Z (1).mkv"
        );

        string[] lines = Export(fixture, movies);
        Assert.Equal(
            ["item.0.caption=Trilogie (tri)", "item.2.caption=A"],
            Captions(lines)
        );
        Assert.Contains("item.0.media_url=../98/La Trilogie", lines);
        Assert.Contains("item.0.icon_path=../98/La Trilogie/icon.aai", lines);
        Assert.Contains("item.2.media_url=../99/A.mkv", lines);
    }

    [Fact]
    public void Open_DossiersDuWorkspace_ConserveLeurOrdrePourLExport()
    {
        using ExportFixture fixture = new();
        string data = Path.Combine(fixture.Root, "data");
        string source = Path.Combine(data, ExportFixture.MovieName, "data.xml");
        foreach (string name in new[] { "Z", "B", "A" })
        {
            string directory = Directory.CreateDirectory(Path.Combine(data, name)).FullName;
            File.Copy(source, Path.Combine(directory, "data.xml"));
        }

        using WorkspaceRepository repository = new();
        Assert.Equal(
            Directory.EnumerateDirectories(data),
            repository.Open(fixture.Root).Cards.Select(card => card.DataDirectory)
        );
    }

    [Fact]
    public void Export_TitresDeTriIdentiques_ConserveChaqueFichierEtSaCasse()
    {
        using ExportFixture fixture = new();
        MediaContext[] movies =
        [
            Movie(fixture, "Rendez Vous (1981).mkv", "Rendez-vous à miNuit", "2"),
            Movie(fixture, "Rendez Vôus (1981).mkv", "Rendez-vous à miNuit", "0"),
            Movie(fixture, "autre.mkv", null),
            Movie(fixture, "sans titre.mkv", ""),
        ];

        string[] lines = Export(fixture, movies);
        Assert.Equal(
            [
                "item.0.caption=Rendez-vous à miNuit",
                "item.1.caption=Rendez-vous à miNuit",
                "item.2.caption=",
            ],
            Captions(lines)
        );
        Assert.Contains("item.0.media_url=../99/Rendez Vous (1981).mkv", lines);
        Assert.Contains("item.1.media_url=../99/Rendez Vôus (1981).mkv", lines);
        Assert.Contains("item.1.icon_sel_path=../99/Rendez Vôus (1981).mkv/icon.aai", lines);
        Assert.Contains("item.1.media_action=browse", lines);
    }

    [Fact]
    public void Export_NomDeSagaDejaEnSuffixe_DedoublonneCommeYaDIS2()
    {
        using ExportFixture fixture = new();
        MediaContext[] movies =
        [
            Movie(fixture, "Z.mkv", "A Quest"),
            Movie(fixture, "Y.mkv", "Dans la saga"),
            Movie(fixture, "A.mkv", "A"),
        ];
        WriteSaga(fixture, "Quest", "Le tri", "Y.mkv");

        Assert.Equal(
            ["item.0.caption=A Quest", "item.2.caption=A"],
            Captions(Export(fixture, movies))
        );
    }

    [Theory]
    [InlineData("Origins", 254, 350, false)]
    [InlineData("GLASS Revisited", 250, 333, true)]
    public void Export_ThemeActif_UtiliseSaGrilleEtSonCadre(
        string theme,
        int width,
        int height,
        bool aai
    )
    {
        using ExportFixture fixture = new();
        DuneTemplate template = ExportFixture.Template(theme);
        string[] lines = Export(fixture, fixture.Cards(), template, aai);

        Assert.Contains($"icon_selection_box_width={width}", lines);
        Assert.Contains($"icon_selection_box_height={height}", lines);
        Assert.Contains($"background_path=background.{(aai ? "aai" : "jpg")}", lines);
        Assert.Contains("num_cols=7", lines);
        Assert.Contains("num_rows=3", lines);
        Assert.Contains("content_box_width=1820", lines);
        Assert.Contains("paint_captions=no", lines);
        Assert.Contains("item.0.icon_scale_factor=0.85", lines);
        Assert.Contains("item.0.icon_sel_scale_factor=1", lines);
    }

    private static MediaContext Movie(
        ExportFixture fixture,
        string filename,
        string? sortTitle,
        string status = "1"
    )
    {
        MediaContext card = fixture.Cards().Single(value => !value.IsSeries);
        card.Header.SetElementValue("filename", filename);
        card.Header.SetElementValue("status", status);
        card.Info.SetElementValue("title", "Un titre d’affichage différent");
        card.Info.SetElementValue("sort_title", sortTitle);
        return card;
    }

    private static void WriteSaga(
        ExportFixture fixture,
        string name,
        string sortName,
        params string[] members
    )
    {
        new XElement(
            "groups",
            new XElement("sagas", new XElement(
                "saga",
                new XElement("name", name),
                new XElement("sort_name", sortName),
                members.Select(member => new XElement("movie", member))
            ))
        ).Save(Path.Combine(fixture.Root, "groups.xml"));
    }

    private static string[] Export(
        ExportFixture fixture,
        MediaContext[] cards,
        DuneTemplate? template = null,
        bool aai = false
    )
    {
        Directory.CreateDirectory(fixture.Destination);
        new DuneExporter(
            template ?? ExportFixture.Template(),
            new RenderOptions { AaiBackgrounds = aai },
            [],
            catalogOptions: new LegacyCatalogOptions { Categories = "00000000000" }
        ).ExportLegacy(cards, fixture.Destination);

        return File.ReadAllLines(
            Path.Combine(fixture.Destination, "00", "00", "dune_folder.txt")
        );
    }

    private static IEnumerable<string> Captions(IEnumerable<string> lines) =>
        lines.Where(line =>
            line.StartsWith("item.", StringComparison.Ordinal)
            && line.Contains(".caption=", StringComparison.Ordinal)
        );
}
