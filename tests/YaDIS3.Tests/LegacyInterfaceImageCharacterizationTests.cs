using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

/// <summary>
/// Vérifie les sorties des réglages Boîtiers, AAI/JPG et qualité JPEG
/// d'après ProcessForm et MainModule de yaDIS2.
/// </summary>
public sealed class LegacyInterfaceImageCharacterizationTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 100)]
    [InlineData(true, 1)]
    [InlineData(true, 100)]
    public void Export_FormatEtQualite_ProduitLeFondAttendu(
        bool aai,
        int quality
    )
    {
        using ExportFixture fixture = new();
        DuneExporter exporter = new(
            ExportFixture.Template(),
            new RenderOptions(
                AaiBackgrounds: aai,
                JpegQuality: quality
            ),
            []
        );

        ExportResult result = exporter.Export(
            fixture.Cards(),
            fixture.Destination
        );

        string background = Path.Combine(
            result.Directory,
            aai ? "background.aai" : "background.jpg"
        );
        Assert.True(File.Exists(background));
        Assert.False(
            File.Exists(
                Path.Combine(
                    result.Directory,
                    aai ? "background.jpg" : "background.aai"
                )
            )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Export_AvecOuSansBoitier_GenereIconeFilm(
        bool useCases
    )
    {
        using ExportFixture fixture = new();
        DuneExporter exporter = new(
            ExportFixture.Template(),
            new RenderOptions(UseCases: useCases),
            []
        );

        ExportResult result = exporter.Export(
            fixture.Cards(),
            fixture.Destination
        );

        Assert.True(
            File.Exists(
                Path.Combine(
                    result.Directory,
                    "00",
                    "99",
                    ExportFixture.MovieName,
                    "icon.aai"
                )
            )
        );
    }

    [Fact]
    public void Export_BoitierDeSecoursAbsent_ContinueSansImageDeCadre()
    {
        using ExportFixture fixture = new();
        string source = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Images"
        );
        string images = Path.Combine(
            fixture.Root,
            "images-sans-boitiers"
        );
        Directory.CreateDirectory(images);
        foreach (
            string file in Directory.EnumerateFiles(
                source,
                "*",
                SearchOption.AllDirectories
            )
        )
        {
            if (
                Path.GetFileName(file).StartsWith(
                    "case_",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                continue;
            }

            string relative = Path.GetRelativePath(
                source,
                file
            );
            string target = Path.Combine(images, relative);
            Directory.CreateDirectory(
                Path.GetDirectoryName(target)!
            );
            File.Copy(file, target);
        }

        DuneTemplate original = ExportFixture.Template();
        DuneTemplate withoutCases = DuneTemplate.Load(
            original.Directory,
            images
        );
        DuneExporter exporter = new(
            withoutCases,
            new RenderOptions(UseCases: true),
            []
        );

        ExportResult result = exporter.Export(
            fixture.Cards(),
            fixture.Destination
        );

        Assert.True(
            File.Exists(
                Path.Combine(
                    result.Directory,
                    "00",
                    "99",
                    ExportFixture.MovieName,
                    "icon.aai"
                )
            )
        );
    }
}
