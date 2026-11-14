using System.Buffers.Binary;
using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyAaiDimensionsTests
{
    [Theory]
    [InlineData("A", 32)]
    [InlineData("B", 32)]
    [InlineData("H", 32)]
    [InlineData("J", 30)]
    [InlineData("T", 30)]
    [InlineData("0-9", 54)]
    public void RenderLegacyIndexLabel_GlassAlphabet_ReproduitDimensionsGdiPlus(
        string caption,
        int width
    )
    {
        using ExportFixture fixture = new();
        using TemplateRenderer renderer = new(
            ExportFixture.Template("GLASS Revisited"),
            new RenderOptions()
        );
        string directory = Path.Combine(fixture.Root, "aai-" + caption);
        renderer.RenderLegacyIndexLabel(directory, caption, "alphabet");

        foreach (string filename in new[] { "icon.aai", "icon_sel.aai" })
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(directory, filename));
            Assert.Equal(width, BinaryPrimitives.ReadInt32LittleEndian(bytes));
            Assert.Equal(35, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)));
            Assert.Equal(8L + width * 35L * 4, bytes.LongLength);
        }
    }

    [Fact]
    public void RenderLegacyIndexLabel_Pays_NeModifiePasLesDimensions()
    {
        using ExportFixture fixture = new();
        using TemplateRenderer renderer = new(
            ExportFixture.Template("GLASS Revisited"),
            new RenderOptions()
        );
        string directory = Path.Combine(fixture.Root, "country");
        renderer.RenderLegacyIndexLabel(directory, "FR", "countries");

        foreach (string filename in new[] { "icon.aai", "icon_sel.aai" })
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(directory, filename));
            Assert.Equal(128, BinaryPrimitives.ReadInt32LittleEndian(bytes));
            Assert.Equal(64, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)));
        }
    }
}
