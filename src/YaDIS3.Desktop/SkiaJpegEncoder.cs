using SkiaSharp;
using YaDIS3.Core;
namespace YaDIS3.Desktop;
public sealed class SkiaJpegEncoder : IJpegEncoder
{
    public void Encode(string source, string target)
    {
        using var input = File.OpenRead(source);
        using var codec = SKCodec.Create(input) ?? throw new InvalidDataException("Image illisible.");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > 80_000_000) throw new InvalidDataException("Dimensions d'image excessives.");
        using var bitmap = SKBitmap.Decode(codec) ?? throw new InvalidDataException("Image illisible.");
        using var surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height)) ?? throw new InvalidDataException("Image trop grande.");
        surface.Canvas.Clear(SKColors.Black);
        surface.Canvas.DrawBitmap(bitmap, 0, 0);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95) ?? throw new InvalidDataException("Encodage JPEG impossible.");
        using var output = File.Create(target); data.SaveTo(output);
    }
}
