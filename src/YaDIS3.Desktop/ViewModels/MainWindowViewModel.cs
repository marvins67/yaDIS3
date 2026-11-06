using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YaDIS3.Core;
using YaDIS3.Desktop.Localization;

namespace YaDIS3.Desktop.ViewModels;

/// <summary>
/// Coordonne l'édition des fiches, TVDB, le rendu et l'export.
/// </summary>
public sealed partial class MainWindowViewModel
    : INotifyPropertyChanged,
        IDisposable
{
    private readonly IWorkspaceRepository _repository;
    private readonly IApplicationRestart _restart;

    /// <summary>Catalogue chargé pour la durée de ce processus.</summary>
    public LegacyLanguageCatalog Texts { get; }
    private readonly ImageImporter _importer;
    private readonly HttpClient _http;
    private readonly DuneConfigurationStore _configurationStore;
    private readonly DuneConfigurationService _configurationService;
    private readonly SourceFolderScanner _sourceScanner;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly ILogger<ConfigurationViewModel> _configurationLogger;
    private readonly ServiceProvider? _ownedServices;
    private bool _disposed;

    private TvdbClient? _tvdb;

    private Workspace? _workspace;

    private MediaCard? _selectedCard;

    private Episode? _selectedEpisode;

    private SeriesMatch? _selectedMatch;

    private EpisodeOrder? _selectedOrder;

    private Bitmap? _poster;

    private Bitmap? _fanart;

    private Bitmap? _banner;

    private Bitmap? _episodeImage;

    private string _filter = "";

    private string _query = "";

    private string _credentials = "";

    private string _status = "";

    private bool _busy;

    /// <summary>
    /// Signale aux liaisons de données que l'état affiché a
    /// changé.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Ensemble des fiches chargées depuis le workspace.
    /// </summary>
    public ObservableCollection<MediaCard> Cards { get; } = [];

    /// <summary>
    /// Fiches correspondant aux filtres courants de la
    /// bibliothèque.
    /// </summary>
    public ObservableCollection<MediaCard> VisibleCards { get; } =
    [];

    /// <summary>
    /// Séries renvoyées par la dernière recherche TVDB.
    /// </summary>
    public ObservableCollection<SeriesMatch> SearchResults { get; } =
    [];

    /// <summary>
    /// Ordres d'épisodes proposés pour le résultat de recherche
    /// sélectionné.
    /// </summary>
    public ObservableCollection<EpisodeOrder> Orders { get; } =
    [];

    /// <summary>
    /// Commande d'enregistrement des fiches modifiées.
    /// </summary>
    public ICommand SaveCommand { get; }

    /// <summary>
    /// Commande de recherche des séries sur TVDB.
    /// </summary>
    public ICommand SearchCommand { get; }

    /// <summary>
    /// Commande d'application du résultat TVDB sélectionné.
    /// </summary>
    public ICommand ApplySeriesCommand { get; }

    /// <summary>
    /// Commande d'enregistrement et de passage à l'épisode
    /// précédent.
    /// </summary>
    public ICommand PreviousEpisodeCommand { get; }

    /// <summary>
    /// Commande d'enregistrement et de passage à l'épisode
    /// suivant.
    /// </summary>
    public ICommand NextEpisodeCommand { get; }

    /// <summary>
    /// Chemin du workspace courant ou libellé indiquant son
    /// absence.
    /// </summary>
    public string WorkspacePath =>
        _workspace?.Directory ?? Texts.GetString("336");

    /// <summary>
    /// Indique si une fiche chargée contient des modifications
    /// non enregistrées.
    /// </summary>
    public bool HasChanges =>
        Cards.Any(cardValue => cardValue.IsDirty);

    /// <summary>
    /// Indique si une fiche est sélectionnée.
    /// </summary>
    public bool HasCard => SelectedCard is not null;

    /// <summary>
    /// Indique si la fiche représente une série.
    /// </summary>
    public bool IsSeries =>
        SelectedCard?.IsSeries ?? IsSeriesFilter;

    /// <summary>
    /// Indique si un épisode est sélectionné.
    /// </summary>
    public bool HasEpisode => SelectedEpisode is not null;

    /// <summary>
    /// Nombre total de fiches et nombre de fiches modifiées pour
    /// la barre d'état.
    /// </summary>
    public string CountLabel =>
        Texts.Format("370", Cards.Count, Cards.Count(cardValue => cardValue.IsDirty));

    /// <summary>
    /// Description des fonctionnalités disponibles dans cette
    /// version.
    /// </summary>
    public string MigrationNote => Texts.GetString("337");

    /// <summary>
    /// Position de l'épisode sélectionné dans la série.
    /// </summary>
    public string EpisodePosition =>
        SelectedCard is null || SelectedEpisode is null
            ? Texts.GetString("338")
            : Texts.Format("371", SelectedCard.Episodes.IndexOf(SelectedEpisode) + 1, SelectedCard.Episodes.Count);

    /// <summary>
    /// Dernier état d'opération affiché dans la fenêtre.
    /// </summary>
    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            Notify();
        }
    }

    /// <summary>
    /// Indique si une opération de l'interface est en cours.
    /// </summary>
    public bool IsBusy
    {
        get => _busy;
        private set
        {
            _busy = value;
            Notify();
            Notify(nameof(CanEditSelection));
        }
    }

    /// <summary>
    /// Texte de recherche manuelle TVDB.
    /// </summary>
    public string Query
    {
        get => _query;
        set
        {
            _query = value;
            Notify();
        }
    }

    /// <summary>
    /// Texte du filtre appliqué à la liste des fiches.
    /// </summary>
    public string Filter
    {
        get => _filter;
        set
        {
            _filter = value;
            Notify();
            RefreshFilter();
        }
    }

    /// <summary>
    /// Libellé d'état de la configuration TVDB.
    /// </summary>
    public string CredentialsLabel
    {
        get => _credentials;
        private set
        {
            _credentials = value;
            Notify();
        }
    }

    /// <summary>
    /// Affiche actuellement chargée.
    /// </summary>
    public Bitmap? Poster
    {
        get => _poster;
        private set => Replace(ref _poster, value);
    }

    /// <summary>
    /// Fanart actuellement chargé.
    /// </summary>
    public Bitmap? Fanart
    {
        get => _fanart;
        private set => Replace(ref _fanart, value);
    }

    /// <summary>
    /// Bannière actuellement chargée.
    /// </summary>
    public Bitmap? Banner
    {
        get => _banner;
        private set => Replace(ref _banner, value);
    }

    /// <summary>
    /// Miniature de l'épisode actuellement chargé.
    /// </summary>
    public Bitmap? EpisodeImage
    {
        get => _episodeImage;
        private set => Replace(ref _episodeImage, value);
    }

    /// <summary>
    /// Fiche en cours d'édition.
    /// </summary>
    public MediaCard? SelectedCard
    {
        get => _selectedCard;
        set
        {
            if (ReferenceEquals(value, _selectedCard))
            {
                return;
            }

            _selectedCard = value;
            Query = value?.Title ?? "";
            SearchResults.Clear();
            Orders.Clear();
            SelectedMatch = null;
            SelectedOrder = null;
            SelectedEpisode = value?.Episodes
                is { Count: > 0 } episodes
                ? episodes[0]
                : null;
            RefreshImages();
            NotifyAll();
        }
    }

    /// <summary>
    /// États historiques des commandes contextuelles.
    /// </summary>
    public LegacyMediaContextMenuState ContextMenuState =>
        LegacyMediaContextMenuState.From(
            SelectedCard?.IsSeries ?? false,
            SelectedCard?.ProcessingState
                ?? MediaProcessingState.Unknown,
            TreatmentSelection.Count > 0
                ? TreatmentSelection.Count
                : SelectedCard is null ? 0 : 1
        );

    /// <summary>Bascule la vérification des titres sélectionnés.</summary>
    public ICommand ToggleVerificationCommand { get; }

    /// <summary>
    /// Épisode en cours d'édition.
    /// </summary>
    public Episode? SelectedEpisode
    {
        get => _selectedEpisode;
        set
        {
            _selectedEpisode = value;
            EpisodeImage = LoadImage(value?.ImagePath);
            Notify();
            Notify(nameof(HasEpisode));
            Notify(nameof(EpisodePosition));
            RefreshRenderedPreview();
        }
    }

    /// <summary>
    /// Résultat de recherche TVDB sélectionné.
    /// </summary>
    public SeriesMatch? SelectedMatch
    {
        get => _selectedMatch;
        set
        {
            _selectedMatch = value;
            Notify();
        }
    }

    /// <summary>
    /// Ordre des épisodes à demander à TVDB.
    /// </summary>
    public EpisodeOrder? SelectedOrder
    {
        get => _selectedOrder;
        set
        {
            _selectedOrder = value;
            Notify();
        }
    }

    /// <summary>
    /// Initialise les commandes d'édition et les templates
    /// fournis.
    /// </summary>
    public MainWindowViewModel()
        : this(DesktopServices.CreateProvider()) { }

    /// <summary>Utilise un fichier utilisateur explicitement choisi.</summary>
    public MainWindowViewModel(string userConfigurationPath)
        : this(
            DesktopServices.CreateProvider(userConfigurationPath)
        )
    {
    }

    private MainWindowViewModel(ServiceProvider services)
        : this(
            services.GetRequiredService<IWorkspaceRepository>(),
            services.GetRequiredService<ImageImporter>(),
            services.GetRequiredService<IHttpClientFactory>(),
            services.GetRequiredService<DuneConfigurationStore>(),
            services.GetRequiredService<DuneConfigurationService>(),
            services.GetRequiredService<SourceFolderScanner>(),
            services.GetRequiredService<
                ILogger<MainWindowViewModel>
            >(),
            services.GetRequiredService<
                ILogger<ConfigurationViewModel>
            >(),
            services.GetRequiredService<LegacyLanguageCatalog>(),
            services.GetRequiredService<IApplicationRestart>()
        )
    {
        _ownedServices = services;
    }

    /// <summary>
    /// Injecte les services et initialise les commandes sans
    /// effectuer de requête réseau.
    /// </summary>
    public MainWindowViewModel(
        IWorkspaceRepository repository,
        ImageImporter importer,
        IHttpClientFactory clients,
        DuneConfigurationStore configurationStore,
        DuneConfigurationService configurationService,
        SourceFolderScanner sourceScanner,
        ILogger<MainWindowViewModel> logger,
        ILogger<ConfigurationViewModel> configurationLogger,
        LegacyLanguageCatalog? texts = null,
        IApplicationRestart? restart = null
    )
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(importer);
        ArgumentNullException.ThrowIfNull(clients);
        ArgumentNullException.ThrowIfNull(configurationStore);
        ArgumentNullException.ThrowIfNull(configurationService);
        ArgumentNullException.ThrowIfNull(sourceScanner);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(configurationLogger);
        Texts = texts ?? LegacyLanguageCatalog.Load();
        MediaFilters = [
            Texts.GetString("333"),
            Texts.GetString("63"),
            Texts.GetString("64"),
        ];

        _restart = restart ?? new DesktopApplicationRestart();
        _repository = repository;
        _importer = importer;
        _http = clients.CreateClient(
            DesktopServices.TvdbClientName
        );
        _configurationStore = configurationStore;
        _configurationService = configurationService;
        _sourceScanner = sourceScanner;
        _logger = logger;
        _configurationLogger = configurationLogger;
        ToggleMediaFilterCommand = new UiCommand(() =>
            SelectMediaAsync(IsSeriesFilter ? 1 : 2)
        );
        SelectMoviesCommand = new UiCommand(() =>
            SelectMediaAsync(1)
        );
        SelectSeriesCommand = new UiCommand(() =>
            SelectMediaAsync(2)
        );
        StateFilters = CreateStateFilters();
        _stateFilterLabel = StateFilters[0].Label;
        _credentials = Texts.GetString("300");
        CycleStateFilterCommand = new UiCommand(
            CycleStateFilterAsync
        );
        InitializeDune();
        TreatmentSelection.CollectionChanged += (_, _) =>
        {
            Notify(nameof(ContextMenuState));
            Notify(nameof(SagaContextChoices));
            Notify(nameof(SelectionContextChoices));
        };
        ToggleVerificationCommand = new UiCommand(() =>
            RunAsync(ToggleVerificationAsync)
        );
        SaveCommand = new UiCommand(() =>
            RunAsync(SaveAllAsync)
        );
        SearchCommand = new UiCommand(() =>
            RunAsync(SearchAsync)
        );
        ApplySeriesCommand = new UiCommand(() =>
            RunAsync(ApplySeriesAsync)
        );
        PreviousEpisodeCommand = new UiCommand(() =>
            RunAsync(() => NavigateAndSaveAsync(-1))
        );
        NextEpisodeCommand = new UiCommand(() =>
            RunAsync(() => NavigateAndSaveAsync(1))
        );
    }

    /// <summary>
    /// Exécute une opération et présente ses erreurs dans la
    /// barre d'état.
    /// </summary>
    public async Task RunAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        _activeOperation = new CancellationTokenSource();
        try
        {
            await action();
            ReloadFilterGroups();
        }
        catch (OperationCanceledException)
        {
            Status = Texts.GetString("339");
        }
        // Présenter l'erreur à l'utilisateur laisse l'éditeur et ses brouillons ouverts.
        catch (Exception exception)
        {
            OperationFailed(_logger, exception);
            Status = exception.Message;
        }
        finally
        {
            _activeOperation.Dispose();
            _activeOperation = null;
            IsBusy = false;
            RefreshFilter();
            NotifyAll();
        }
    }

    private void Notify([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(name)
        );
    }

    private void NotifyAll()
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(string.Empty)
        );
    }

    /// <summary>
    /// Libère les ressources possédées et les illustrations
    /// temporaires.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        foreach (MediaCard card in Cards)
        {
            card.PropertyChanged -= CardChanged;
            card.Dispose();
        }

        ClearArtworkChoices();
        _poster?.Dispose();
        _banner?.Dispose();
        _fanart?.Dispose();
        _episodeImage?.Dispose();
        _renderedPreview?.Dispose();
        _activeOperation?.Cancel();
        _tvdb?.Dispose();
        _ownedServices?.Dispose();
        _http.Dispose();
    }

    [LoggerMessage(
        1,
        LogLevel.Error,
        "Échec d'une opération de la fenêtre principale"
    )]
    private static partial void OperationFailed(
        ILogger logger,
        Exception exception
    );
}
