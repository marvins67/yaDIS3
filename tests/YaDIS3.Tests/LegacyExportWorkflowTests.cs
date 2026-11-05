using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

/// <summary>
/// Valide le nouveau parcours de publication depuis workspace/interface.
/// </summary>
public sealed class LegacyExportWorkflowTests
{
    [Fact]
    public void ExportLegacy_DestinationAbsente_RefuseLaPublication()
    {
        using ExportFixture fixture = new();

        Assert.Throws<DirectoryNotFoundException>(() =>
            ExportFixture.Exporter().ExportLegacy(
                fixture.Cards(),
                fixture.Destination
            )
        );
        Assert.False(Directory.Exists(fixture.Destination));
    }

    [Fact]
    public void ExportLegacy_DeuxExports_SynchroniseSansSauvegardePrevious()
    {
        using ExportFixture fixture = new();
        fixture.ProcessIndexes();
        Directory.CreateDirectory(fixture.Destination);
        DuneExporter exporter = ExportFixture.Exporter();

        ExportResult first = exporter.ExportLegacy(
            fixture.Cards(),
            fixture.Destination
        );
        string obsolete = Path.Combine(first.Directory, "obsolete.txt");
        File.WriteAllText(obsolete, "ancien");
        exporter.ExportLegacy(fixture.Cards(), fixture.Destination);

        Assert.False(File.Exists(obsolete));
        Assert.False(Directory.Exists(fixture.Destination + ".previous"));
        Assert.True(
            File.Exists(
                Path.Combine(fixture.Root, "interface", "dune_folder.txt")
            )
        );
    }

    [Fact]
    public void ExportLegacy_ImagesTraitees_ReutiliseLesFichiersSource()
    {
        using ExportFixture fixture = new();
        fixture.ProcessIndexes();
        string movie = Path.Combine(
            fixture.Root,
            "interface",
            "00",
            "99",
            ExportFixture.MovieName
        );
        Directory.CreateDirectory(movie);
        byte[] icon = [1, 2, 3, 4];
        File.WriteAllBytes(Path.Combine(movie, "icon.aai"), icon);
        Directory.CreateDirectory(fixture.Destination);

        ExportFixture.Exporter().ExportLegacy(
            fixture.Cards(),
            fixture.Destination
        );

        Assert.Equal(
            icon,
            File.ReadAllBytes(
                Path.Combine(
                    fixture.Destination,
                    "00",
                    "99",
                    ExportFixture.MovieName,
                    "icon.aai"
                )
            )
        );
    }
}
