using SkiaSharp;
using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

/// <summary>Dimensions mesurées sur les fichiers AAI yaDIS2 de référence.</summary>
public sealed class LegacyIndexLabelMetricsTests
{
    [Fact]
    public void RenderLegacyIndexLabel_Pays_ConserveLeDrapeau128Par64()
    {
        using ExportFixture fixture = new();
        using TemplateRenderer renderer = new(
            ExportFixture.Template("GLASS Revisited"),
            new RenderOptions()
        );
        string directory = Path.Combine(fixture.Root, "country");

        renderer.RenderLegacyIndexLabel(directory, "US", "countries");

        using SKBitmap icon = AaiCodec.Load(
            Path.Combine(directory, "icon.aai")
        );
        using SKBitmap selected = AaiCodec.Load(
            Path.Combine(directory, "icon_sel.aai")
        );
        Assert.Equal(128, icon.Width);
        Assert.Equal(64, icon.Height);
        Assert.Equal(icon.Info, selected.Info);
    }

    [Theory]
    [InlineData("0-9", "alphabet", 54)]
    [InlineData("A", "alphabet", 32)]
    [InlineData("E", "alphabet", 30)]
    [InlineData("H", "alphabet", 32)]
    [InlineData("I", "alphabet", 26)]
    [InlineData("J", "alphabet", 30)]
    [InlineData("M", "alphabet", 36)]
    [InlineData("R", "alphabet", 32)]
    [InlineData("T", "alphabet", 30)]
    [InlineData("W", "alphabet", 38)]
    [InlineData("B", "alphabet", 32)]
    [InlineData("C", "alphabet", 32)]
    [InlineData("D", "alphabet", 32)]
    [InlineData("F", "alphabet", 30)]
    [InlineData("G", "alphabet", 32)]
    [InlineData("K", "alphabet", 32)]
    [InlineData("L", "alphabet", 30)]
    [InlineData("N", "alphabet", 32)]
    [InlineData("O", "alphabet", 32)]
    [InlineData("P", "alphabet", 32)]
    [InlineData("Q", "alphabet", 32)]
    [InlineData("S", "alphabet", 32)]
    [InlineData("U", "alphabet", 32)]
    [InlineData("V", "alphabet", 32)]
    [InlineData("X", "alphabet", 32)]
    [InlineData("Y", "alphabet", 32)]
    [InlineData("Z", "alphabet", 30)]
    [InlineData("Animation", "genres", 124)]
    [InlineData("Familial", "genres", 104)]
    [InlineData("Fantastique", "genres", 148)]
    [InlineData("Roy Atwell", "actors", 136)]
    [InlineData("Adriana Caselotti", "actors", 206)]
    [InlineData("Pinto Colvig", "actors", 146)]
    [InlineData("Harry Stockwell", "actors", 200)]
    [InlineData("Lucille La Verne", "actors", 184)]
    [InlineData("David Hand", "directors", 132)]
    [InlineData("1h 30", "durations", 72)]
    [InlineData("1930", "years", 70)]
    [InlineData("7", "ratings", 30)]
    [InlineData("Matroska", "types", 124)]
    public void RenderLegacyIndexLabel_ReferenceYaDIS2_DimensionsExactes(
        string caption,
        string category,
        int width
    )
    {
        using ExportFixture fixture = new();
        using TemplateRenderer renderer = new(
            ExportFixture.Template("GLASS Revisited"),
            new RenderOptions()
        );
        string directory = Path.Combine(fixture.Root, "label");

        renderer.RenderLegacyIndexLabel(directory, caption, category);

        using SKBitmap icon = AaiCodec.Load(Path.Combine(directory, "icon.aai"));
        using SKBitmap selected = AaiCodec.Load(Path.Combine(directory, "icon_sel.aai"));
        Assert.Equal(width, icon.Width);
        Assert.Equal(35, icon.Height);
        Assert.Equal(icon.Info, selected.Info);
    }
}
