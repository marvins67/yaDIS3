using SkiaSharp;
using Xunit;
using YaDIS3.Desktop;

namespace YaDIS3.Desktop.Tests;

public sealed class SkiaJpegEncoderTests
{
    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Png)]
    public void Encode_ImageValide_ConserveJpegOuConvertitPng(
        SKEncodedImageFormat format
    )
    {
        string root = Path.Combine(
            Path.GetTempPath(), "yadis3-jpeg-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "source");
            string target = Path.Combine(root, "target.jpg");
            using SKBitmap bitmap = new(32, 24);
            using (SKCanvas canvas = new(bitmap))
            {
                canvas.Clear(SKColors.CornflowerBlue);
            }
            using SKImage image = SKImage.FromBitmap(bitmap);
            using SKData data = image.Encode(format, 65)!;
            byte[] original = data.ToArray();
            File.WriteAllBytes(source, original);

            new SkiaJpegEncoder().Encode(source, target);

            byte[] output = File.ReadAllBytes(target);
            using SKCodec codec = SKCodec.Create(target)!;
            Assert.Equal(SKEncodedImageFormat.Jpeg, codec.EncodedFormat);
            Assert.Equal(32, codec.Info.Width);
            Assert.Equal(24, codec.Info.Height);
            if (format == SKEncodedImageFormat.Jpeg)
            {
                Assert.Equal(original, output);
            }
            else
            {
                Assert.NotEqual(original, output);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Encode_ImageInvalide_NeCreePasDeFichier()
    {
        string root = Path.Combine(
            Path.GetTempPath(), "yadis3-jpeg-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "invalid.jpg");
            string target = Path.Combine(root, "target.jpg");
            File.WriteAllBytes(source, [1, 2, 3, 4]);

            Assert.Throws<InvalidDataException>(
                () => new SkiaJpegEncoder().Encode(source, target)
            );
            Assert.False(File.Exists(target));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
