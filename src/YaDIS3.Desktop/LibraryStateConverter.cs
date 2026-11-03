using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace YaDIS3.Desktop;

/// <summary>
/// Affiche l'état d'édition d'une fiche et la présence de son
/// aperçu.
/// </summary>
public sealed class LibraryStateConverter : IMultiValueConverter
{
    private readonly Dictionary<string, Bitmap> _images = new(
        StringComparer.Ordinal
    );

    /// <summary>
    /// Renvoie l'icône d'état ou sa couleur associée.
    /// </summary>
    public object Convert(
        IList<object?> values,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        bool edited = values.Count > 1 && values[1] is true;
        bool hasPreview =
            values.Count > 0
            && values[0] is string file
            && File.Exists(
                Path.Combine(
                    Path.GetDirectoryName(file)!,
                    "preview.jpg"
                )
            );
        if (parameter is "image")
        {
            string name =
                edited ? "edit"
                : hasPreview ? "accept_green"
                : "question_orange";
            return StateImage(name);
        }

        if (parameter is "color")
        {
            return edited ? Brushes.DarkGoldenrod
                : hasPreview ? Brushes.SeaGreen
                : Brushes.Peru;
        }

        return edited ? "*"
            : hasPreview ? "✓"
            : "?";
    }

    private Bitmap StateImage(string name)
    {
        if (!_images.TryGetValue(name, out Bitmap? image))
        {
            using Stream stream = AssetLoader.Open(
                new Uri(
                    $"avares://yaDIS3.Desktop/Assets/Icons/{name}.png"
                )
            );
            image = new Bitmap(stream);
            _images.Add(name, image);
        }
        return image;
    }
}
