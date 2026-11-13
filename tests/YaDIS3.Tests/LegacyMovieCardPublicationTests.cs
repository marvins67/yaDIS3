using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

/// <summary>Vérifie la séparation entre traitement et export des films.</summary>
public sealed class LegacyMovieCardPublicationTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("3")]
    public void ExportLegacy_FilmSansRendu_NeCreePasDeFiche(string status)
    {
        using ExportFixture fixture = new();
        Directory.CreateDirectory(fixture.Destination);

        MediaContext[] cards = fixture.Cards();
        cards.Single(card => !card.IsSeries).Header.SetElementValue("status", status);
        ExportFixture.Exporter().ExportLegacy(cards, fixture.Destination);

        Assert.False(Directory.Exists(MovieDirectory(fixture.Destination)));
    }

    [Fact]
    public void ExportLegacy_CoquilleAncienne_ConserveUneCopieExclue()
    {
        using ExportFixture fixture = new();
        string movie = MovieDirectory(Path.Combine(fixture.Root, "interface"));
        Directory.CreateDirectory(movie);
        File.WriteAllText(Path.Combine(movie, "dune_folder.txt"), "icon_path=icon.aai");
        Directory.CreateDirectory(fixture.Destination);
        var exporter = ExportFixture.Exporter();

        exporter.ExportLegacy(fixture.Cards(), fixture.Destination);
        exporter.ExportLegacy(fixture.Cards(), fixture.Destination);

        Assert.False(Directory.Exists(MovieDirectory(fixture.Destination)));
        Assert.True(File.Exists(Path.Combine(
            Path.GetDirectoryName(movie)!, "_" + ExportFixture.MovieName,
            "dune_folder.txt")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Process_FilmTraite_PublieEtExporteUneFicheComplete(bool aai)
    {
        using ExportFixture fixture = new();
        MediaContext movie = fixture.Cards().Single(card => !card.IsSeries);
        DuneExporter.Process(movie, ExportFixture.Template(),
            new RenderOptions(AaiBackgrounds: aai), fixture.Root);
        string source = MovieDirectory(Path.Combine(fixture.Root, "interface"));
        AssertComplete(source, aai);
        Directory.CreateDirectory(fixture.Destination);
        var exporter = ExportFixture.Exporter();

        exporter.ExportLegacy(fixture.Cards(), fixture.Destination);
        AssertComplete(MovieDirectory(fixture.Destination), aai);
        byte[] icon = File.ReadAllBytes(Path.Combine(source, "icon.aai"));
        exporter.ExportLegacy(fixture.Cards(), fixture.Destination);
        Assert.Equal(icon, File.ReadAllBytes(Path.Combine(
            MovieDirectory(fixture.Destination), "icon.aai")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExportLegacy_RenduSupprime_DemandeUnRetraitementExplicite(bool aai)
    {
        using ExportFixture fixture = new();
        MediaContext movie = fixture.Cards().Single(card => !card.IsSeries);
        RenderOptions options = new(AaiBackgrounds: aai);
        DuneExporter.Process(movie, ExportFixture.Template(), options, fixture.Root);
        string source = MovieDirectory(Path.Combine(fixture.Root, "interface"));
        Directory.Delete(source, true);
        Directory.CreateDirectory(fixture.Destination);
        var exporter = ExportFixture.Exporter();

        ExportResult result = exporter.ExportLegacy(fixture.Cards(), fixture.Destination);

        Assert.False(Directory.Exists(source));
        Assert.False(Directory.Exists(MovieDirectory(fixture.Destination)));
        Assert.Contains(result.Warnings, warning => warning.Contains("Fiche à traiter", StringComparison.Ordinal));
        DuneExporter.Process(movie, ExportFixture.Template(), options, fixture.Root);
        exporter.ExportLegacy(fixture.Cards(), fixture.Destination);
        AssertComplete(MovieDirectory(fixture.Destination), aai);
    }

    [Fact]
    public void Synchronize_CopieBloquee_SignaleErreurPuisReprend()
    {
        using ExportFixture fixture = new();
        string source = Path.Combine(fixture.Root, "source");
        Directory.CreateDirectory(source);
        string target = Path.Combine(fixture.Destination, "icon.aai");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(source, "icon.aai"), "image");
        List<string> errors = [];
        LegacyExportSynchronizer synchronizer = new();

        synchronizer.Synchronize(source, fixture.Destination, errors: errors);
        Assert.Single(errors);
        Directory.Delete(target);
        errors.Clear();
        synchronizer.Synchronize(source, fixture.Destination, errors: errors);

        Assert.Empty(errors);
        Assert.Equal("image", File.ReadAllText(target));
    }

    private static string MovieDirectory(string root) =>
        Path.Combine(root, "00", "99", ExportFixture.MovieName);

    private static void AssertComplete(string directory, bool aai)
    {
        Assert.True(File.Exists(Path.Combine(directory, "icon.aai")));
        string background = aai ? "background.aai" : "background.jpg";
        Assert.True(File.Exists(Path.Combine(directory, background)));
        string definition = File.ReadAllText(Path.Combine(directory, "dune_folder.txt"));
        Assert.Contains("background_path=" + background, definition, StringComparison.Ordinal);
        Assert.Contains("icon_path=icon.aai", definition, StringComparison.Ordinal);
    }
}
