using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using YaDIS3.Core;

namespace YaDIS3.Desktop;

/// <summary>
/// Affiche l'icône du statut XML de yaDIS2.
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
        MediaProcessingState state =
            values.Count > 0
            && values[0] is MediaProcessingState value
                ? value
                : MediaProcessingState.Unknown;
        return parameter switch
        {
            "image" => StateImage(IconName(state)),
            "color" => StateColor(state),
            _ => state switch
            {
                MediaProcessingState.Checked
                or MediaProcessingState.Unchecked => "✓",
                MediaProcessingState.Missing => "?",
                _ => "!",
            },
        };
    }

    private static string IconName(MediaProcessingState state) =>
        state switch
        {
            MediaProcessingState.Checked => "accept_green",
            MediaProcessingState.Unchecked => "accept_blue",
            MediaProcessingState.Missing => "question_orange",
            _ => "stop_round",
        };

    private static IBrush StateColor(
        MediaProcessingState state
    ) =>
        state switch
        {
            MediaProcessingState.Checked => Brushes.SeaGreen,
            MediaProcessingState.Unchecked => Brushes.SteelBlue,
            MediaProcessingState.Missing => Brushes.Peru,
            _ => Brushes.Firebrick,
        };

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
