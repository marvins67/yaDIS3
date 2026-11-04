using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace YaDIS3.Desktop;

/// <summary>
/// Reproduit les icônes grisées des boutons Windows désactivés.
/// Les deux variantes sont conservées pour éviter un décodage
/// à chaque changement de sélection.
/// </summary>
public sealed class ToolbarIconConverter : IValueConverter
{
    private readonly Dictionary<string, Bitmap> _images = new(
        StringComparer.Ordinal
    );

    /// <inheritdoc />
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        string name = (string)parameter!;
        bool enabled = value is true;
        string key = name + enabled;
        if (!_images.TryGetValue(key, out Bitmap? image))
        {
            image = LoadIcon(name, enabled);
            _images.Add(key, image);
        }
        return image;
    }

    private static Bitmap LoadIcon(string name, bool enabled)
    {
        using Stream source = AssetLoader.Open(
            new Uri(
                "avares://yaDIS3.Desktop/Assets/Icons/"
                    + name
                    + ".png"
            )
        );
        using SKBitmap original = SKBitmap.Decode(source);
        using SKBitmap output = new(
            original.Width,
            original.Height
        );
        using SKCanvas canvas = new(output);
        using SKPaint paint = new();
        using SKColorFilter? filter = enabled
            ? null
            : CreateDisabledFilter();
        paint.ColorFilter = filter;
        canvas.DrawBitmap(original, 0, 0, paint);
        using SKImage image = SKImage.FromBitmap(output);
        using SKData data = image.Encode(
            SKEncodedImageFormat.Png,
            100
        );
        using Stream stream = data.AsStream();
        return new Bitmap(stream);
    }

    private static SKColorFilter CreateDisabledFilter() =>
        SKColorFilter.CreateColorMatrix([
            .299f,
            .587f,
            .114f,
            0,
            0,
            .299f,
            .587f,
            .114f,
            0,
            0,
            .299f,
            .587f,
            .114f,
            0,
            0,
            0,
            0,
            0,
            .45f,
            0,
        ]);

    /// <inheritdoc />
    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}
