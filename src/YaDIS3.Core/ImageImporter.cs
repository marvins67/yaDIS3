namespace YaDIS3.Core;

/// <summary>
/// Conserve les illustrations importées en attente de
/// l'enregistrement de la fiche.
/// </summary>
public sealed class ImageImporter(IJpegEncoder encoder)
{
    /// <summary>
    /// Prépare l'illustration de la fiche ou de l'épisode à
    /// remplacer.
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
