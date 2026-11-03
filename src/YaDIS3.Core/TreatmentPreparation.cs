using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>Prépare la fiche avant de lancer le rendu Dune.</summary>
public sealed class TreatmentPreparation(
    IExportFileSystem files,
    ITreatmentMetadata metadata,
    IVideoInformationReader technical
)
{
    /// <summary>Vérifie la source et complète les données locales.</summary>
    public async Task<bool> PrepareAsync(
        MediaCard card,
        TreatmentMode mode,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        cancellationToken.ThrowIfCancellationRequested();
        if (card.ProcessingState == MediaProcessingState.Missing)
        {
            return false;
        }
        string source = card.TreatmentSource;
        if (card.IsSeries)
        {
            await PrepareSeriesAsync(
                card,
                source,
                mode,
                cancellationToken
            );
            if (card.Episodes.Count == 0)
            {
                return false;
            }
        }
        else
        {
            await PrepareMovieAsync(
                card,
                source,
                mode,
                cancellationToken
            );
        }
        await ReadTechnicalAsync(card, mode, cancellationToken);
        return true;
    }

    private async Task PrepareSeriesAsync(
        MediaCard card,
        string source,
        TreatmentMode mode,
        CancellationToken cancellationToken
    )
    {
        if (!files.DirectoryExists(source))
        {
            throw new DirectoryNotFoundException(source);
        }
        IReadOnlyList<VideoEpisode> detected = await Task.Run(
            () => EpisodeScanner.Detect(source),
            cancellationToken
        );
        SeriesDetails? series = null;
        if (
            !card.HasTreatmentInfo
            || card.TvdbId.Length > 0
            || mode == TreatmentMode.ReloadInformation
        )
        {
            series = await metadata.FindSeriesAsync(
                card,
                cancellationToken
            );
        }
        card.SynchronizeForTreatment(detected, files);
        if (series is not null)
        {
            card.ApplySeriesForTreatment(series, mode);
        }
        await metadata.CompleteArtworkAsync(
            card,
            cancellationToken
        );
    }

    private async Task PrepareMovieAsync(
        MediaCard card,
        string source,
        TreatmentMode mode,
        CancellationToken cancellationToken
    )
    {
        string file = FindMovieVideo(source);
        if (
            !card.HasTreatmentInfo
            || mode == TreatmentMode.ReloadInformation
        )
        {
            XElement info =
                await metadata.FindMovieAsync(
                    card,
                    cancellationToken
                )
                ?? throw new InvalidDataException(
                    "Film non identifié."
                );
            card.ApplyMovieForTreatment(info);
        }
        await metadata.CompleteArtworkAsync(
            card,
            cancellationToken
        );
        await ReadTrackAsync(
            card,
            card.TreatmentInfo!,
            file,
            mode,
            cancellationToken
        );
    }

    private string FindMovieVideo(string source)
    {
        if (files.FileExists(source))
        {
            return source;
        }
        if (!files.DirectoryExists(source))
        {
            throw new DirectoryNotFoundException(source);
        }
        foreach (
            string folder in new[]
            {
                "VIDEO_TS",
                "BDMV",
                "video_ts",
                "bdmv",
            }
        )
        {
            string disc = Path.Combine(source, folder);
            if (files.DirectoryExists(disc))
            {
                return disc;
            }
        }
        return files
                .EnumerateFiles(source)
                .Where(LegacyVideoFiles.IsVideo)
                .Order(StringComparer.Ordinal)
                .FirstOrDefault()
            ?? throw new InvalidDataException(
                "Aucune vidéo dans " + source
            );
    }

    private async Task ReadTechnicalAsync(
        MediaCard card,
        TreatmentMode mode,
        CancellationToken cancellationToken
    )
    {
        foreach (Episode episode in card.Episodes)
        {
            await ReadTrackAsync(
                card,
                episode.Node,
                episode.Node.Element("file")?.Value ?? "",
                mode,
                cancellationToken
            );
        }
    }

    private async Task ReadTrackAsync(
        MediaCard card,
        XElement node,
        string file,
        TreatmentMode mode,
        CancellationToken cancellationToken
    )
    {
        if (
            node.Element("mediainfo") is not null
            && mode != TreatmentMode.ReloadTechnicalInformation
        )
        {
            return;
        }
        XElement result = await technical.ReadAsync(
            file,
            cancellationToken
        );
        card.UpdateForTreatment(() =>
        {
            node.Element("mediainfo")?.Remove();
            node.Add(result);
        });
    }
}
