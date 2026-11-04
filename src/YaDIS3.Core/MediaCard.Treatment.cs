using System.Xml.Linq;

namespace YaDIS3.Core;

public sealed partial class MediaCard
{
    internal string TreatmentSource =>
        Path.Combine(
            Header.Element("path")?.Value ?? "",
            FolderName
        );

    internal XElement? TreatmentInfo => Info;

    internal bool HasTreatmentInfo => Info is not null;

    internal void UpdateForTreatment(Action action)
    {
        action();
        _revision++;
        Notify();
    }

    internal void SynchronizeForTreatment(
        IReadOnlyList<VideoEpisode> detected,
        IExportFileSystem files
    )
    {
        EpisodeScanner.Synchronize(
            this,
            detected,
            manual: false
        );
        UpdateForTreatment(() =>
        {
            foreach (
                XElement episode in Info!
                    .Elements("episode")
                    .ToArray()
            )
            {
                string file =
                    episode.Element("file")?.Value ?? "";
                if (!files.FileExists(file))
                {
                    episode.Remove();
                }
            }
        });
        RefreshEpisodes();
    }

    internal void ApplyMovieForTreatment(XElement info)
    {
        UpdateForTreatment(() =>
        {
            Info?.Remove();
            Root.Add(info);
        });
    }

    internal void ApplySeriesForTreatment(
        SeriesDetails series,
        TreatmentMode mode
    )
    {
        string previousStatus =
            Header.Element("status")?.Value ?? "";
        if (
            TvdbId.Length == 0
            || mode == TreatmentMode.ReloadInformation
        )
        {
            if (mode == TreatmentMode.ReloadInformation)
            {
                UpdateForTreatment(() =>
                {
                    foreach (Episode episode in Episodes.Where(item => !item.IsManual))
                    {
                        episode.Node.Element("mediainfo")?.Remove();
                    }
                });
            }
            ApplyTvdb(series, Order);
            UpdateForTreatment(() =>
            {
                Info!.Attribute("manual_blank")?.Remove();
                Info.Element("images")?.Remove();
                Info.Add(
                    new XElement(
                        "images",
                        series.Artwork.Select(
                            entry => new XElement(
                                entry.Key,
                                entry.Value
                            )
                        )
                    )
                );
                Put(Header, "status", previousStatus);
            });
            return;
        }
        UpdateForTreatment(() => CompleteEpisodes(series));
    }

    private static void ReplaceSeriesNames(
        XElement info, string container, string element,
        IReadOnlyList<string> names
    )
    {
        info.Element(container)?.Remove();
        info.Elements(element).Remove();
        info.Add(new XElement(container, names.Select(name =>
            new XElement(element, name))));
    }

    private void CompleteEpisodes(SeriesDetails series)
    {
        var episodes = series
            .Episodes.GroupBy(item => (item.Season, item.Number))
            .ToDictionary(
                group => group.Key,
                group => group.First()
            );
        foreach (Episode episode in Episodes)
        {
            if (
                episode.IsManual
                || episode.Node.Element("aired")?.Value.Length
                    > 0
                || !episodes.TryGetValue(
                    (episode.Season, episode.Number),
                    out var match
                )
            )
            {
                continue;
            }
            Put(episode.Node, "title", match.Name);
            Put(episode.Node, "plot", match.Overview);
            Put(episode.Node, "aired", match.Aired);
            Put(episode.Node, "rating", match.Rating);
            Put(episode.Node, "director", match.Director);
            if (episode.Node.Attribute("manual_image") is null)
            {
                Put(
                    episode.Node,
                    "image",
                    LegacyArtwork.Encode(match.Image)
                );
            }
        }
    }

    /// <summary>Indique si le rendu doit reprendre les retouches enregistrées.</summary>
    public bool NeedsEpisodeRerender =>
        _artwork.Count > 0
        || Info?.Attribute("episode_edits_pending") is not null;

    /// <summary>Enregistre la fin du rendu des retouches.</summary>
    public void CompleteTreatment()
    {
        if (
            Info?.Attribute("episode_edits_pending") is
            { } pending
        )
        {
            UpdateForTreatment(pending.Remove);
        }
    }

    /// <summary>Maintient la fiche dans les éléments à traiter.</summary>
    public void MarkTreatmentFailed()
    {
        if (ProcessingState != MediaProcessingState.Unprocessed)
        {
            UpdateForTreatment(() => Put(Header, "status", "3"));
        }
    }
}
