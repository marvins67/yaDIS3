using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace YaDIS3.Desktop;

/// <summary>
/// Displays an item's edit state and saved preview state.
/// </summary>
public sealed class LibraryStateConverter : IMultiValueConverter
{
    /// <summary>
    /// Returns the state glyph or its matching indicator brush.
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
}
