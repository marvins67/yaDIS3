using System.ComponentModel;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Charge Workspace selon les préférences de yaDIS2.</summary>
    public Task RestoreWorkspaceAsync() =>
        RunAsync(async () =>
        {
            DuneConfiguration settings =
                _configurationStore.LoadSettings();
            string workspace = string.IsNullOrEmpty(
                settings.Workspace
            )
                ? Path.Combine(
                    Environment.CurrentDirectory,
                    "workspace"
                )
                : settings.Workspace;
            await OpenAsync(
                workspace,
                create: true,
                cancellationToken: OperationToken
            );
        });

    /// <summary>
    /// Ouvre un workspace et actualise la sélection des fiches
    /// visibles.
    /// </summary>
    public async Task OpenAsync(
        string path,
        bool create = false,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Workspace loaded = await Task.Run(
            () => ScanAndOpen(path, create, cancellationToken),
            cancellationToken
        );
        foreach (MediaCard card in Cards)
        {
            card.PropertyChanged -= CardChanged;
            card.Dispose();
        }

        _workspace = loaded;
        Cards.Clear();
        VisibleCards.Clear();
        LoadDunePreferences();
        ConfigurationSnapshot snapshot =
            _configurationStore.Load(loaded.Directory);
        foreach (MediaCard card in loaded.Cards)
        {
            if (!IsConfiguredSource(card, snapshot))
            {
                card.Dispose();
                continue;
            }
            Cards.Add(card);
            card.PropertyChanged += CardChanged;
        }

        ReloadFilterGroups();
        RefreshFilter();
        SelectedCard = VisibleCards.FirstOrDefault();
        Status =
            loaded.Warnings.Count == 0
                ? ""
                : string.Join(" · ", loaded.Warnings);
        NotifyAll();
        _configuration = _configuration with
        {
            Workspace = loaded.Directory,
        };
        _configurationStore.SaveSettings(
            loaded.Directory,
            _configuration
        );
    }

    private Workspace ScanAndOpen(
        string path,
        bool create,
        CancellationToken cancellationToken
    )
    {
        if (create)
        {
            _repository.Create(path);
        }
        string root = WorkspaceRepository.ResolveDirectory(path);
        ConfigurationSnapshot snapshot =
            _configurationStore.Load(root);
        _sourceScanner.Scan(
            root,
            snapshot.Folders,
            snapshot.Settings.BasicMode,
            cancellationToken
        );
        Workspace workspace = _repository.Open(
            root,
            cancellationToken
        );
        foreach (MediaCard card in workspace.Cards)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _sourceScanner.CheckAvailability(
                card,
                snapshot.Folders
            );
        }
        return workspace;
    }

    private void CardChanged(
        object? sender,
        PropertyChangedEventArgs args
    )
    {
        Notify(nameof(CountLabel));
        Notify(nameof(HasChanges));
        Notify(nameof(ActorsText));
        Notify(nameof(GenresText));
        Notify(nameof(EditableCard));
        Notify(nameof(CanEditSelection));
        // Comme RefreshMediaList(false) dans yaDIS2, actualiser
        // le statut ne doit pas retirer la fiche pendant la saisie.
    }

    private void RefreshFilter()
    {
        MediaCard? previous = _selectedCard;
        VisibleCards.Clear();
        foreach (
            MediaCard card in Cards.Where(MatchesLibraryFilters)
        )
        {
            VisibleCards.Add(card);
        }

        SelectedCard =
            previous is not null
            && VisibleCards.Contains(previous)
                ? previous
                : VisibleCards.FirstOrDefault();
        Notify(nameof(VisibleCountLabel));
    }

    private async Task ToggleVerificationAsync()
    {
        MediaCard[] selected =
            TreatmentSelection.Count > 0
                ? TreatmentSelection.Distinct().ToArray()
                : SelectedCard is { } currentCard ? [currentCard] : [];

        foreach (MediaCard card in selected)
        {
            if (card.ToggleVerification())
            {
                await _repository.SaveAsync(card, OperationToken);
            }
        }

        RefreshFilter();
        NotifyAll();
    }

    /// <summary>
    /// Enregistre toutes les fiches modifiées du workspace
    /// courant.
    /// </summary>
    public async Task SaveAllAsync()
    {
        foreach (
            MediaCard card in Cards.Where(cardValue =>
                cardValue.IsDirty
            )
        )
        {
            await new LegacyIndexedCardSaver(
                _repository
            ).SaveAsync(card, OperationToken);
        }

        Status = Texts.GetString("351");
        RefreshImages();
        NotifyAll();
    }

    /// <summary>
    /// Crée et enregistre une série à partir d'un dossier vidéo
    /// local.
    /// </summary>
    public async Task AddManualSeriesAsync(string directory)
    {
        Workspace current =
            _workspace
            ?? throw new InvalidOperationException(
                Texts.GetString("352")
            );
        IReadOnlyList<VideoEpisode> detected = await Task.Run(
            () =>
                EpisodeScanner.Detect(directory)
        );
        MediaCard card = _repository.CreateManualSeries(
            current,
            directory,
            detected
        );
        await new LegacyIndexedCardSaver(_repository).SaveAsync(
            card,
            OperationToken
        );
        RememberManualSource(directory);
        Cards.Add(card);
        card.PropertyChanged += CardChanged;
        MediaFilterIndex = 2;
        RefreshStateMenu();
        SelectStateFilter(StateFilters[0]);
        InvertStateFilter = false;
        Filter = "";
        SelectedCard = card;
        Status =
            Texts.Format("372", card.Episodes.Count);
    }

    /// <summary>
    /// Détecte les fichiers vidéo et les fusionne avec la série
    /// sélectionnée.
    /// </summary>
    public async Task ScanAsync(string directory)
    {
        MediaCard card =
            SelectedCard
            ?? throw new InvalidOperationException(
                Texts.GetString("347")
            );
        IReadOnlyList<VideoEpisode> found = await Task.Run(() =>
            EpisodeScanner.Detect(directory)
        );
        EpisodeScanner.Synchronize(card, found);
        SelectedEpisode =
            card.Episodes.Count > 0 ? card.Episodes[0] : null;
        Notify(nameof(SelectedCard));
        Status =
            Texts.Format("373", card.Episodes.Count);
    }
}
