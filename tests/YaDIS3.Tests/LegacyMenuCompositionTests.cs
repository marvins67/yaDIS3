using System.Xml.Linq;
using SkiaSharp;
using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyMenuCompositionTests
{
    [Theory]
    [InlineData("Origins")]
    [InlineData("GLASS Revisited")]
    public void Menu_TransparentOverlays_DoNotAccumulateJpegLoss(string name)
    {
        using ExportFixture fixture = new();
        using TemplateRenderer renderer = new(ExportFixture.Template(name),
            new RenderOptions(JpegQuality: 45));
        string directory = Path.Combine(fixture.Root, "menu");
        renderer.RenderMenu(directory, "menu");
        string background = Path.Combine(directory, "background.jpg");
        byte[] original = File.ReadAllBytes(background);
        using SKBitmap icon = new(8, 8);
        icon.Erase(SKColors.Transparent);
        string path = Path.Combine(fixture.Root, "empty.aai");
        AaiCodec.Save(icon, path);
        XElement area = new("area", new XAttribute("x", 20),
            new XAttribute("y", 30), new XAttribute("width", 8),
            new XAttribute("height", 8));
        renderer.OverlayNavigation(directory, path, area);
        renderer.OverlayNavigation(directory, path, area);
        Assert.Equal(original, File.ReadAllBytes(background));
    }

    [Theory]
    [InlineData("Origins")]
    [InlineData("GLASS Revisited")]
    public void Menu_StaticText_RespectsLanguage(string name)
    {
        using ExportFixture fixture = new();
        DuneTemplate template = ExportFixture.Template(name);
        XElement scene = new("background");
        template.Root.Element("menu")!.Add(new XElement("iconview", scene));
        string directory = Path.Combine(fixture.Root, "menu");
        using TemplateRenderer renderer = new(template,
            new RenderOptions(AaiBackgrounds: true, Language: "fr"));
        renderer.RenderMenu(directory, "menu");
        string path = Path.Combine(directory, "background.aai");
        byte[] empty = File.ReadAllBytes(path);
        XElement text = new(template.Node("menu/index/text"));
        text.SetAttributeValue("type", "static");
        text.SetAttributeValue("language", "en");
        text.SetAttributeValue("x", 100);
        text.SetAttributeValue("y", 100);
        text.SetAttributeValue("width", 400);
        text.SetAttributeValue("height", 80);
        text.SetAttributeValue("color", "#ffffff");
        text.Value = "Été 0123";
        scene.Add(text);
        renderer.RenderMenu(directory, "menu");
        Assert.Equal(empty, File.ReadAllBytes(path));
        text.SetAttributeValue("language", "fr");
        renderer.RenderMenu(directory, "menu");
        Assert.False(empty.SequenceEqual(File.ReadAllBytes(path)));
    }

}
