using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SkiaSharp;
namespace YaDIS3.Rendering;

/// <summary>Legacy Dune AAI: LE width/height then straight BGRA; opaque alpha is 254.</summary>
public static class AaiCodec
{
    public static void Save(SKBitmap source, string path)
    {
        CheckSize(source.Width, source.Height);
        using var straight = new SKBitmap(new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        using var pixels = source.PeekPixels();
        if (!pixels.ReadPixels(straight.Info, straight.GetPixels(), straight.RowBytes, 0, 0)) throw new InvalidDataException("Conversion AAI impossible.");
        var bytes = new byte[checked(source.Width * source.Height * 4)];
        Marshal.Copy(straight.GetPixels(), bytes, 0, bytes.Length);
        for (var i = 3; i < bytes.Length; i += 4) if (bytes[i] == 255) bytes[i] = 254;
        using var output = File.Create(path);
        Span<byte> header = stackalloc byte[8]; BinaryPrimitives.WriteInt32LittleEndian(header, source.Width); BinaryPrimitives.WriteInt32LittleEndian(header[4..], source.Height);
        output.Write(header); output.Write(bytes);
    }
    public static SKBitmap Load(string path)
    {
        using var input = File.OpenRead(path); Span<byte> header = stackalloc byte[8]; input.ReadExactly(header);
        var width = BinaryPrimitives.ReadInt32LittleEndian(header); var height = BinaryPrimitives.ReadInt32LittleEndian(header[4..]); CheckSize(width, height);
        var length = checked(width * height * 4); if (input.Length != length + 8L) throw new InvalidDataException("Taille du fichier AAI invalide.");
        var bytes = new byte[length]; input.ReadExactly(bytes);
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul)); Marshal.Copy(bytes, 0, bitmap.GetPixels(), length); return bitmap;
    }
    internal static void CheckSize(int width, int height)
    { if (width <= 0 || height <= 0 || width > 8192 || height > 8192 || (long)width * height > 16_000_000) throw new InvalidDataException("Dimensions d'image Dune invalides."); }
}
