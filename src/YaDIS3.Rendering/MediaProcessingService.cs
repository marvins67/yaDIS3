using Microsoft.Extensions.Logging;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

/// <summary>
/// Valide et enregistre le statut uniquement après un rendu
/// réussi.
/// </summary>
public sealed class MediaProcessingService
{
    private readonly IWorkspaceRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger? _logger;

    /// <summary>
    /// Injecte le stockage et l'horloge du traitement.
    /// </summary>
    public MediaProcessingService(
        IWorkspaceRepository repository,
        TimeProvider timeProvider,
        ILogger? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Produit les images puis applique la transition d'état de
    /// yaDIS2.
    /// </summary>
    public async Task<ProcessingResult> ProcessAsync(
        MediaCard card,
        DuneTemplate template,
        RenderOptions options,
        string workspace,
        IProgress<string>? progress = null,
        DuneConfiguration? configuration = null,
        bool useFirstEpisodePreview = false,
        IReadOnlySet<string>? reusableEpisodes = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        if (configuration is not null)
        {
            card.EnsureMovieSortTitle(configuration);
        }
        if (card.IsDirty)
        {
            await new LegacyIndexedCardSaver(
                _repository
            ).SaveAsync(card, cancellationToken);
        }
        MediaContext media = new(card);
        ProcessingResult result = await Task.Run(
            () =>
                DuneExporter.Process(
                    media,
                    template,
                    options,
                    workspace,
                    progress: progress,
                    cancellationToken: cancellationToken,
                    useFirstEpisodePreview: useFirstEpisodePreview,
                    reusableEpisodes: reusableEpisodes,
                    logger: _logger
                ),
            cancellationToken
        );
        cancellationToken.ThrowIfCancellationRequested();
        if (
            configuration is not null
            && !media.IsSeries
            && configuration.AutoSelect
            && int.TryParse(
                media.Header.ValueAt("status"),
                out int status
            )
            && status > 2
        )
        {
            new LegacyAutoSelection().Add(
                workspace,
                configuration.AutoSelectName,
                media.Header.ValueAt("filename")
            );
        }
        card.MarkProcessed(
            DateOnly.FromDateTime(
                _timeProvider.GetLocalNow().DateTime
            )
        );
        if (useFirstEpisodePreview)
        {
            card.CompleteTreatment();
        }
        await _repository.SaveAsync(card, cancellationToken);
        return result;
    }
}
