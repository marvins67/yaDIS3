using SkiaSharp;
using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

/// <summary>Caractérise la mesure entière et l'arrondi des étiquettes CreateIndexItem.</summary>
public sealed class LegacyIndexLabelMetricsTests
{
    [Theory]
    [InlineData("0-9", 54)]
    [InlineData("R", 32)]
    public void RenderLegacyIndexLabel_Glass_ConserveLargeurMesureeEtArrondi(string caption, int width)
    {
        using ExportFixture fixture = new();
        using TemplateRenderer renderer = new(
            ExportFixture.Template("GLASS Revisited"), new RenderOptions());
        string output = Path.Combine(fixture.Root, "label");

        renderer.RenderLegacyIndexLabel(output, caption, "alphabet");

        using SKBitmap icon = AaiCodec.Load(Path.Combine(output, "icon.aai"));
        using SKBitmap selected = AaiCodec.Load(Path.Combine(output, "icon_sel.aai"));
        Assert.Equal(width, icon.Width);
        Assert.Equal(38, icon.Height);
        Assert.Equal(icon.Info, selected.Info);
    }
}
