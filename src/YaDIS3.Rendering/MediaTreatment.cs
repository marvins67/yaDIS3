using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

/// <summary>Coordonne la préparation, le rendu et le statut final.</summary>
public sealed class MediaTreatment(
    IWorkspaceRepository repository,
    TreatmentPreparation preparation,
    TimeProvider timeProvider,
    LegacyLanguageCatalog? texts = null,
    ILogger? logger = null
)
{
    private readonly LegacyLanguageCatalog _texts =
        texts ?? LegacyLanguageCatalog.Load();

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
        MediaContext? original = card.IsSeries ? new MediaContext(card) : null;
        MediaContext? previous =
            card.IsSeries
            && !card.IsDirty
            && !card.NeedsEpisodeRerender
            && mode == TreatmentMode.Normal
                ? new MediaContext(card)
                : null;
        progress?.Report(_texts["11"] + " : " + card.FolderName);
        try
        {
            bool prepared;
            try
            {
                prepared = await preparation.PrepareAsync(
                    card, mode, cancellationToken);
            }
            finally
            {
                // Une erreur ultérieure ne doit pas laisser les anciens rendus
                // des épisodes déjà retirés du XML enregistré dans le catch.
                if (original is not null)
                {
                    EpisodeTreatmentCleanup.RemoveDeleted(
                        original, new MediaContext(card), workspace);
                }
            }
            if (!prepared)
            {
                card.MarkTreatmentFailed();
                await repository.SaveAsync(
                    card,
                    cancellationToken
                );
                return false;
            }
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            card.MarkTreatmentFailed();
            await repository.SaveAsync(card, cancellationToken);
            throw;
        }
        try
        {
            // Une exception pendant l’écriture interrompt ProcessForm avant la sauvegarde du statut.
            await new MediaProcessingService(
                repository,
                timeProvider,
                logger
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
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            throw new TreatmentRenderException(exception.Message, exception);
        }
        return true;
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
