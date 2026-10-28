namespace YaDIS3.Core;

/// <summary>
/// Identifies the artwork slot to update on a media card.
/// </summary>
public enum ArtworkKind
{
    /// <summary>Movie or series poster.</summary>
    Poster,

    /// <summary>Wide series banner.</summary>
    Banner,

    /// <summary>Background artwork.</summary>
    Fanart,

    /// <summary>Episode thumbnail.</summary>
    Episode,
}

/// <summary>
/// Converts imported images to the workspace JPEG format.
/// </summary>
public interface IJpegEncoder
{
    /// <summary>
    /// Converts the source image to a JPEG at the target path.
    /// </summary>
    void Encode(string source, string target);
}

/// <summary>
/// Stages artwork replacements until the card is saved.
/// </summary>
public sealed class ImageImporter(IJpegEncoder encoder)
{
    /// <summary>
    /// Stages an image for the requested card or episode
    /// artwork slot.
    /// </summary>
    public void Import(
        MediaCard card,
        ArtworkKind kind,
        string source,
        Episode? episode = null
    )
    {
        if (
            kind == ArtworkKind.Episode
            && (
                episode is null
                || !card.Episodes.Contains(episode)
            )
        )
        {
            throw new ArgumentException(
                "Épisode absent de cette fiche."
            );
        }

        string name = kind switch
        {
            ArtworkKind.Poster => "poster.jpg",
            ArtworkKind.Banner => "banner.jpg",
            ArtworkKind.Fanart => "fanart.jpg",
            ArtworkKind.Episode =>
                $"{episode!.Season}x{episode.Number}.jpg",
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind)
            ),
        };
        string temporary = Path.Combine(
            Path.GetTempPath(),
            "yadis3-artwork-"
                + Guid.NewGuid().ToString("N")
                + ".jpg"
        );
        try
        {
            encoder.Encode(source, temporary);
            card.StageImage(name, temporary);
            card.Update(() =>
            {
                if (kind == ArtworkKind.Episode)
                {
                    episode!.MarkImageManual();
                }
            });
        }
        catch
        {
            MediaCard.TryDelete(temporary);
            throw;
        }
    }
}
