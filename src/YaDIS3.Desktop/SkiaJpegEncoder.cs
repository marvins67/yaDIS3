using SkiaSharp;
using YaDIS3.Core;

namespace YaDIS3.Desktop;

/// <summary>
/// Converts supported images to opaque JPEG artwork with
/// Skia.
/// </summary>
public sealed class SkiaJpegEncoder : IJpegEncoder
{
    /// <summary>
    /// Converts the source image to a JPEG at the target path.
    /// </summary>
    public void Encode(string source, string target)
    {
        using FileStream input = File.OpenRead(source);
        using SKCodec codec =
            SKCodec.Create(input)
            ?? throw new InvalidDataException(
                "Image illisible."
            );
        SKImageInfo info = codec.Info;
        if (
            info.Width <= 0
            || info.Height <= 0
            || (long)info.Width * info.Height > 80_000_000
        )
        {
            throw new InvalidDataException(
                "Dimensions d'image excessives."
            );
        }

        using SKBitmap bitmap =
            SKBitmap.Decode(codec)
            ?? throw new InvalidDataException(
                "Image illisible."
            );
        using SKSurface surface =
            SKSurface.Create(
                new SKImageInfo(bitmap.Width, bitmap.Height)
            )
            ?? throw new InvalidDataException(
                "Image trop grande."
            );
        surface.Canvas.Clear(SKColors.Black);
        surface.Canvas.DrawBitmap(bitmap, 0, 0);
        using SKImage image = surface.Snapshot();
        using SKData data =
            image.Encode(SKEncodedImageFormat.Jpeg, 95)
            ?? throw new InvalidDataException(
                "Encodage JPEG impossible."
            );
        using FileStream output = File.Create(target);
        data.SaveTo(output);
    }
}
