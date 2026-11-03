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
    ) => ImportCore(card, kind, source, episode, true);

    internal void ImportAutomatic(
        MediaCard card,
        ArtworkKind kind,
        string source,
        Episode? episode
    ) => ImportCore(card, kind, source, episode, false);

    private void ImportCore(
        MediaCard card,
        ArtworkKind kind,
        string source,
        Episode? episode,
        bool manual
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
            if (!manual)
            {
                card.UpdateForTreatment(() => { });
                return;
            }
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
