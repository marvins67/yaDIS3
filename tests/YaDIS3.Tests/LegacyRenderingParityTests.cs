using SkiaSharp;
using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyRenderingParityTests
{
    [Theory]
    [InlineData("Origins")]
    [InlineData("GLASS Revisited")]
    public void Label_LowerCase_MeasuresTheDrawnCaption(string name)
    {
        using ExportFixture fixture = new();
        DuneTemplate template = ExportFixture.Template(name);
        template.Node("menu/index/text").SetAttributeValue("case", "lower");
        using TemplateRenderer renderer = new(template, new RenderOptions());
        string first = Path.Combine(fixture.Root, "first");
        string second = Path.Combine(fixture.Root, "second");
        renderer.RenderLegacyIndexLabel(first, "WWWÉ", "alphabet");
        renderer.RenderLegacyIndexLabel(second, "wwwé", "alphabet");
        Assert.Equal(File.ReadAllBytes(Path.Combine(first, "icon.aai")),
            File.ReadAllBytes(Path.Combine(second, "icon.aai")));
        Assert.Equal(File.ReadAllBytes(Path.Combine(first, "icon_sel.aai")),
            File.ReadAllBytes(Path.Combine(second, "icon_sel.aai")));
    }

    [Fact]
    public void Aai_StraightAlpha_WritesHistoricalBgraBytes()
    {
        using ExportFixture fixture = new();
        using SKBitmap image = new(new SKImageInfo(2, 1,
            SKColorType.Bgra8888, SKAlphaType.Unpremul));
        image.SetPixel(0, 0, new SKColor(10, 20, 30, 255));
        image.SetPixel(1, 0, new SKColor(40, 50, 60, 128));
        string path = Path.Combine(fixture.Root, "pixels.aai");
        AaiCodec.Save(image, path);
        byte[] expected = [2, 0, 0, 0, 1, 0, 0, 0,
            30, 20, 10, 254, 60, 50, 40, 128];
        Assert.Equal(expected, File.ReadAllBytes(path));
        using SKBitmap decoded = AaiCodec.Load(path);
        Assert.Equal(new SKColor(40, 50, 60, 128), decoded.GetPixel(1, 0));
    }
}
