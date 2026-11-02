using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyAaiExportTests
{
    [Fact]
    public void Export_AaiBackgrounds_UsesLegacyBackgroundReferences()
    {
        using ExportFixture fixture = new();
        DuneExporter exporter = new(
            ExportFixture.Template(),
            new RenderOptions(AaiBackgrounds: true),
            []
        );
        ExportResult result = exporter.Export(
            fixture.Cards(),
            fixture.Destination
        );
        Assert.True(
            File.Exists(
                Path.Combine(result.Directory, "background.aai")
            )
        );
        string movie = File.ReadAllText(
            Path.Combine(
                result.Directory,
                "00",
                "99",
                ExportFixture.MovieName,
                "dune_folder.txt"
            )
        );
        Assert.Contains(
            "background_path=background.aai",
            movie,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "background.jpg",
            movie,
            StringComparison.Ordinal
        );
        Assert.True(
            File.Exists(
                Path.Combine(
                    result.Directory,
                    "00",
                    "05",
                    "background2.aai"
                )
            )
        );
    }
}
