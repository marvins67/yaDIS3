using System.Collections.ObjectModel;
using System.Windows.Input;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Fiches choisies pour un traitement en lot.</summary>
    public ObservableCollection<MediaCard> TreatmentSelection { get; } =
    [];

    /// <summary>Recharge les métadonnées des fiches choisies.</summary>
    public ICommand ReloadInformationCommand
    {
        get;
        private set;
    } = null!;

    /// <summary>Relit les caractéristiques techniques des vidéos.</summary>
    public ICommand ReloadTechnicalCommand
    {
        get;
        private set;
    } = null!;

    /// <summary>Force le traitement de tous les épisodes.</summary>
    public ICommand ProcessAllEpisodesCommand
    {
        get;
        private set;
    } = null!;

    private UiCommand TreatmentCommand(TreatmentMode mode) =>
        new(() =>
            RunAsync(() =>
                TreatSelectionAsync(mode, OperationToken)
            )
        );

    /// <summary>Traite la sélection avec les options courantes.</summary>
    public Task ProcessSelectedAsync() =>
        TreatSelectionAsync(
            TreatmentMode.Normal,
            OperationToken
        );

    private async Task TreatSelectionAsync(
        TreatmentMode mode,
        CancellationToken cancellationToken
    )
    {
        DuneTemplate template =
            SelectedTemplate
            ?? throw new InvalidOperationException(
                "Chargez un template yaDIS3."
            );
        Workspace workspace =
            _workspace
            ?? throw new InvalidOperationException(
                Texts.GetString("340")
            );
        MediaCard[] cards =
            TreatmentSelection.Count > 0
                ? TreatmentSelection.ToArray()
            : SelectedCard is { } card ? [card]
            : [];
        SaveDunePreferences();
        OnlineTreatmentMetadata metadata = new(
            _http,
            _tvdb,
            Environment.GetEnvironmentVariable(
                "YADIS_TMDB_TOKEN"
            ),
            TemplateLanguage,
            _importer
        );
        TreatmentPreparation preparation = new(
            new ExportFileSystem(),
            metadata,
            new MediaInfoReader()
        );
        MediaTreatment treatment = new(
            _repository,
            preparation,
            TimeProvider.System,
            Texts
        );
        var progress = new Progress<string>(message =>
            Status = message
        );
        string summary = await TreatBatchAsync(
            cards,
            treatment,
            template,
            workspace.Directory,
            mode,
            progress,
            cancellationToken
        );
        SelectedEpisode = SelectedCard?.Episodes
            is { Count: > 0 } episodes
            ? episodes[0]
            : null;
        RefreshImages();
        Status = summary;
    }

    private async Task<string> TreatBatchAsync(
        MediaCard[] cards,
        MediaTreatment treatment,
        DuneTemplate template,
        string workspace,
        TreatmentMode mode,
        IProgress<string> progress,
        CancellationToken cancellationToken
    )
    {
        int completed = 0;
        int skipped = 0;
        List<string> errors = [];
        foreach (MediaCard card in cards)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                bool rendered = await treatment.TreatAsync(
                    card,
                    template,
                    ConfiguredRenderOptions(),
                    workspace,
                    mode,
                    _configuration,
                    progress,
                    cancellationToken
                );
                completed += rendered ? 1 : 0;
                skipped += rendered ? 0 : 1;
            }
            catch (Exception exception)
                when (exception is not OperationCanceledException
                )
            {
                OperationFailed(_logger, exception);
                errors.Add(
                    card.FolderName + " : " + exception.Message
                );
            }
        }
        return Texts.Format("376", completed, skipped)
            + (
                errors.Count == 0
                    ? ""
                    : " " + string.Join(" · ", errors)
            );
    }
}
