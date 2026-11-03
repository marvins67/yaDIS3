using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

/// <summary>Coordonne la préparation, le rendu et le statut final.</summary>
public sealed class MediaTreatment(
    IWorkspaceRepository repository,
    TreatmentPreparation preparation,
    TimeProvider timeProvider
)
{
    /// <summary>Traite une fiche et indique si elle a été rendue.</summary>
    public async Task<bool> TreatAsync(
        MediaCard card,
        DuneTemplate template,
        RenderOptions options,
        string workspace,
        TreatmentMode mode,
        DuneConfiguration configuration,
        IProgress<string>? progress,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card.ProcessingState == MediaProcessingState.Missing)
        {
            return false;
        }
        MediaContext? previous =
            card.IsSeries
            && !card.IsDirty
            && !card.NeedsEpisodeRerender
            && mode == TreatmentMode.Normal
                ? new MediaContext(card)
                : null;
        progress?.Report("Préparation : " + card.FolderName);
        try
        {
            if (
                !await preparation.PrepareAsync(
                    card,
                    mode,
                    cancellationToken
                )
            )
            {
                card.MarkTreatmentFailed();
                await repository.SaveAsync(
                    card,
                    cancellationToken
                );
                return false;
            }
            await new MediaProcessingService(
                repository,
                timeProvider
            ).ProcessAsync(
                card,
                template,
                options,
                workspace,
                progress,
                configuration,
                useFirstEpisodePreview: true,
                reusableEpisodes: FindReusableEpisodes(
                    previous,
                    card
                ),
                cancellationToken: cancellationToken
            );
            return true;
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            card.MarkTreatmentFailed();
            await repository.SaveAsync(card, cancellationToken);
            throw;
        }
    }

    private static HashSet<string>? FindReusableEpisodes(
        MediaContext? previous,
        MediaCard card
    )
    {
        if (previous is null || card.NeedsEpisodeRerender)
        {
            return null;
        }
        MediaContext current = new(card);
        XElement oldInfo = new(previous.Info);
        XElement newInfo = new(current.Info);
        oldInfo.Elements("episode").Remove();
        newInfo.Elements("episode").Remove();
        if (!XNode.DeepEquals(oldInfo, newInfo))
        {
            return null;
        }
        return current
            .Episodes.Where(episode =>
                previous.Episodes.Any(old =>
                    XNode.DeepEquals(old, episode)
                )
            )
            .Select(episode =>
                Path.Combine(
                    MediaContext.ViewingSeason(episode),
                    MediaContext.EpisodeFolder(episode)
                )
            )
            .ToHashSet(StringComparer.Ordinal);
    }
}
