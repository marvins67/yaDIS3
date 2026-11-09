using Xunit;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyExportNavigationCategoriesTests
{
    [Theory]
    [InlineData("11111111111", true, true, true)]
    [InlineData("10000000000", true, false, false)]
    [InlineData("11000000000", true, true, false)]
    [InlineData("10100000000", true, false, true)]
    public void Export_CategoriesConfigured_OnlyEnabledFoldersAreVisible(
        string categories,
        bool recent,
        bool alphabet,
        bool genres
    )
    {
        using ExportFixture fixture = new();
        Directory.CreateDirectory(fixture.Destination);
        Exporter(categories).ExportLegacy(
            fixture.Cards(),
            fixture.Destination
        );

        Assert.Equal(
            recent,
            Directory.Exists(Path.Combine(fixture.Destination, "00", "02"))
        );
        Assert.Equal(
            alphabet,
            Directory.Exists(Path.Combine(fixture.Destination, "00", "03"))
        );
        Assert.Equal(
            genres,
            Directory.Exists(Path.Combine(fixture.Destination, "00", "04"))
        );
    }

    [Fact]
    public void Export_CategoriesChanged_RemovesDisabledNavigationAndRestoresEnabled()
    {
        using ExportFixture fixture = new();
        Directory.CreateDirectory(fixture.Destination);
        fixture.ProcessIndexes();
        MediaContext[] cards = fixture.Cards();
        string movies = Path.Combine(fixture.Destination, "00");

        Exporter("11111111111").ExportLegacy(cards, fixture.Destination);
        Assert.True(Directory.Exists(Path.Combine(movies, "03")));
        Assert.True(Directory.Exists(Path.Combine(movies, "04")));
        Assert.True(Directory.Exists(Path.Combine(movies, "11")));

        Exporter("11000000000").ExportLegacy(cards, fixture.Destination);
        Assert.True(Directory.Exists(Path.Combine(movies, "03")));
        Assert.False(Directory.Exists(Path.Combine(movies, "04")));
        Assert.False(Directory.Exists(Path.Combine(movies, "11")));

        string source = Path.Combine(fixture.Root, "interface", "00");
        Assert.True(Directory.Exists(Path.Combine(source, "_04")));
        Assert.True(Directory.Exists(Path.Combine(source, "_11")));

        Exporter("11111111111").ExportLegacy(cards, fixture.Destination);
        Assert.True(Directory.Exists(Path.Combine(movies, "03")));
        Assert.True(Directory.Exists(Path.Combine(movies, "04")));
        Assert.True(Directory.Exists(Path.Combine(movies, "11")));
    }

    private static DuneExporter Exporter(string categories) =>
        new(
            ExportFixture.Template(),
            new RenderOptions(),
            [],
            catalogOptions: new LegacyCatalogOptions
            {
                Categories = categories,
            }
        );
}
