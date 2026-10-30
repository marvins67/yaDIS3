using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using YaDIS3.Core;

namespace YaDIS3.Desktop.ViewModels;

/// <summary>
/// Coordinates card editing, TVDB queries, rendering, and
/// export.
/// </summary>
public sealed partial class MainWindowViewModel
    : INotifyPropertyChanged,
        IDisposable
{
    private readonly WorkspaceRepository _repository = new();

    private readonly ImageImporter _importer = new(
        new SkiaJpegEncoder()
    );

    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(45),
    };

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

    private string _credentials = "TVDB non configuré";

    private string _status =
        "Ouvrez une copie de votre workspace yaDIS3 pour commencer.";

    private bool _busy;

    /// <summary>
    /// Notifies bindings when the displayed state changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets all cards loaded from the workspace.
    /// </summary>
    public ObservableCollection<MediaCard> Cards { get; } = [];

    /// <summary>
    /// Gets the cards matching the current title filter.
    /// </summary>
    public ObservableCollection<MediaCard> VisibleCards { get; } =
    [];

    /// <summary>
    /// Gets the series returned by the most recent TVDB search.
    /// </summary>
    public ObservableCollection<SeriesMatch> SearchResults { get; } =
    [];

    /// <summary>
    /// Gets the episode orders offered for the selected search
    /// result.
    /// </summary>
    public ObservableCollection<EpisodeOrder> Orders { get; } =
    [];

    /// <summary>
    /// Gets the command that saves modified cards.
    /// </summary>
    public ICommand SaveCommand { get; }

    /// <summary>
    /// Gets the command that searches TVDB for series.
    /// </summary>
    public ICommand SearchCommand { get; }

    /// <summary>
    /// Gets the command that applies the selected TVDB result.
    /// </summary>
    public ICommand ApplySeriesCommand { get; }

    /// <summary>
    /// Gets the command that saves and opens the previous
    /// episode.
    /// </summary>
    public ICommand PreviousEpisodeCommand { get; }

    /// <summary>
    /// Gets the command that saves and opens the next episode.
    /// </summary>
    public ICommand NextEpisodeCommand { get; }

    /// <summary>
    /// Gets the current workspace path or its empty-state
    /// label.
    /// </summary>
    public string WorkspacePath =>
        _workspace?.Directory ?? "Aucune bibliothèque ouverte";

    /// <summary>
    /// Indicates whether any loaded card has unsaved edits.
    /// </summary>
    public bool HasChanges =>
        Cards.Any(cardValue => cardValue.IsDirty);

    /// <summary>
    /// Indicates whether a media card is selected.
    /// </summary>
    public bool HasCard => SelectedCard is not null;

    /// <summary>
    /// Indicates whether the current card represents a series.
    /// </summary>
    public bool IsSeries => SelectedCard?.IsSeries == true;

    /// <summary>
    /// Indicates whether an episode is selected.
    /// </summary>
    public bool HasEpisode => SelectedEpisode is not null;

    /// <summary>
    /// Gets the total and modified card counts for the status
    /// display.
    /// </summary>
    public string CountLabel =>
        $"{Cards.Count} fiche(s) · {Cards.Count(cardValue => cardValue.IsDirty)} modifiée(s)";

    /// <summary>
    /// Gets the description of the migrated features available
    /// in this build.
    /// </summary>
    public string MigrationNote =>
        "Édition, rendu des templates et export Dune disponibles. Choisissez un template puis Traiter, ou exportez la bibliothèque depuis l’onglet Export Dune.";

    /// <summary>
    /// Gets the selected episode's position within the series.
    /// </summary>
    public string EpisodePosition =>
        SelectedCard is null || SelectedEpisode is null
            ? "Aucun épisode — analysez un dossier de vidéos."
            : $"Épisode {SelectedCard.Episodes.IndexOf(SelectedEpisode) + 1} / {SelectedCard.Episodes.Count}";

    /// <summary>
    /// Gets or sets the latest operation status shown in the
    /// window.
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
    /// Indicates whether a UI operation is in progress.
    /// </summary>
    public bool IsBusy
    {
        get => _busy;
        private set
        {
            _busy = value;
            Notify();
        }
    }

    /// <summary>
    /// Gets or sets the manual TVDB search text.
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
    /// Gets or sets the title filter applied to the card list.
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
    /// Gets the TVDB configuration status label.
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
    /// Gets the current poster image.
    /// </summary>
    public Bitmap? Poster
    {
        get => _poster;
        private set => Replace(ref _poster, value);
    }

    /// <summary>
    /// Gets the current fanart image.
    /// </summary>
    public Bitmap? Fanart
    {
        get => _fanart;
        private set => Replace(ref _fanart, value);
    }

    /// <summary>
    /// Gets the current banner image.
    /// </summary>
    public Bitmap? Banner
    {
        get => _banner;
        private set => Replace(ref _banner, value);
    }

    /// <summary>
    /// Gets the current episode thumbnail image.
    /// </summary>
    public Bitmap? EpisodeImage
    {
        get => _episodeImage;
        private set => Replace(ref _episodeImage, value);
    }

    /// <summary>
    /// Gets or sets the media card currently being edited.
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
            SelectedEpisode = value?.Episodes.FirstOrDefault();
            RefreshImages();
            NotifyAll();
        }
    }

    /// <summary>
    /// Gets or sets the episode currently being edited.
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
    /// Gets or sets the selected TVDB search result.
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
    /// Gets or sets the episode order to request from TVDB.
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
    /// Initializes editor commands and the bundled Dune
    /// templates.
    /// </summary>
    public MainWindowViewModel()
    {
        InitializeDune();
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
    /// Runs one UI operation and reports failures through the
    /// status text.
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
        }
        catch (OperationCanceledException)
        {
            Status = "Opération annulée.";
        }
        // Report failures at the UI boundary without closing the editor.
        catch (Exception exception)
        {
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

    /// <summary>
    /// Opens a workspace and refreshes the visible card
    /// selection.
    /// </summary>
    public async Task OpenAsync(string path, bool create = false)
    {
        if (create)
        {
            _repository.Create(path);
        }

        Workspace loaded = await Task.Run(() =>
            _repository.Open(path)
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
        foreach (MediaCard card in loaded.Cards)
        {
            Cards.Add(card);
            card.PropertyChanged += CardChanged;
        }

        RefreshFilter();
        SelectedCard = VisibleCards.FirstOrDefault();
        Status =
            loaded.Warnings.Count == 0
                ? "Bibliothèque ouverte."
                : string.Join(" · ", loaded.Warnings);
        NotifyAll();
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
        if (StateFilterIndex == 3 && !IsBusy)
        {
            RefreshFilter();
        }
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

    /// <summary>
    /// Saves every modified card in the current workspace.
    /// </summary>
    public async Task SaveAllAsync()
    {
        foreach (
            MediaCard card in Cards.Where(cardValue =>
                cardValue.IsDirty
            )
        )
        {
            await _repository.SaveAsync(card, OperationToken);
        }

        Status =
            "Modifications enregistrées. Les copies précédentes portent le suffixe .yadis3-modern.bak.";
        RefreshImages();
        NotifyAll();
    }

    /// <summary>
    /// Creates and saves a series card from a local video
    /// folder.
    /// </summary>
    public async Task AddManualSeriesAsync(string directory)
    {
        Workspace current =
            _workspace
            ?? throw new InvalidOperationException(
                "Ouvrez ou créez d'abord une bibliothèque."
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
        await _repository.SaveAsync(card, OperationToken);
        Cards.Add(card);
        card.PropertyChanged += CardChanged;
        MediaFilterIndex = 2;
        StateFilterIndex = 0;
        InvertStateFilter = false;
        Filter = "";
        SelectedCard = card;
        Status =
            $"Série manuelle créée : {card.Episodes.Count} épisode(s).";
    }

    /// <summary>
    /// Detects video episodes and merges them into the selected
    /// series.
    /// </summary>
    public async Task ScanAsync(string directory)
    {
        MediaCard card =
            SelectedCard
            ?? throw new InvalidOperationException(
                "Sélectionnez une série."
            );
        IReadOnlyList<VideoEpisode> found = await Task.Run(() =>
            EpisodeScanner.Detect(directory)
        );
        EpisodeScanner.Synchronize(card, found);
        SelectedEpisode = card.Episodes.FirstOrDefault();
        Notify(nameof(SelectedCard));
        Status =
            $"{card.Episodes.Count} épisode(s). Les informations existantes sont conservées ; enregistrez les changements.";
    }

    /// <summary>
    /// Loads TVDB credentials from the selected configuration
    /// file.
    /// </summary>
    public void ConfigureTvdb(string file)
    {
        _tvdb = new TvdbClient(_http, TvdbSettings.Load(file));
        CredentialsLabel = "TVDB v4 configuré";
        Status = "Configuration TVDB chargée.";
    }

    /// <summary>
    /// Searches TVDB series titles using the configured
    /// language.
    /// </summary>
    public async Task SearchAsync()
    {
        TvdbClient api =
            _tvdb
            ?? throw new InvalidOperationException(
                "Chargez votre tvdb-v4.json avec Configuration TVDB."
            );
        IReadOnlyList<SeriesMatch> found = await api.SearchAsync(
            Query,
            cancellationToken: OperationToken
        );
        SearchResults.Clear();
        Orders.Clear();
        SelectedMatch = null;
        SelectedOrder = null;
        foreach (SeriesMatch match in found)
        {
            SearchResults.Add(match);
        }

        Status =
            found.Count == 0
                ? "Aucune série trouvée. Vous pouvez ajouter une série manuelle depuis son dossier."
                : $"{found.Count} résultat(s) TVDB.";
    }

    /// <summary>
    /// Refreshes the episode orders available for the search
    /// selection.
    /// </summary>
    public async Task LoadOrdersAsync()
    {
        SeriesMatch? match = SelectedMatch;
        if (match is null || _tvdb is null)
        {
            return;
        }

        Orders.Clear();
        SelectedOrder = null;
        IReadOnlyList<EpisodeOrder> available =
            await _tvdb.AvailableOrdersAsync(
                match.Id,
                OperationToken
            );
        if (SelectedMatch != match)
        {
            return;
        }

        foreach (EpisodeOrder order in available)
        {
            Orders.Add(order);
        }

        SelectedOrder =
            available.FirstOrDefault(o =>
                o.Code == SelectedCard?.Order
            ) ?? available.FirstOrDefault();
    }

    /// <summary>
    /// Applies the selected TVDB series metadata to the current
    /// draft.
    /// </summary>
    public async Task ApplySeriesAsync()
    {
        TvdbClient api =
            _tvdb
            ?? throw new InvalidOperationException(
                "TVDB non configuré."
            );
        MediaCard card =
            SelectedCard
            ?? throw new InvalidOperationException(
                "Sélectionnez une série."
            );
        SeriesMatch match =
            SelectedMatch
            ?? throw new InvalidOperationException(
                "Choisissez une série dans les résultats."
            );
        EpisodeOrder order =
            SelectedOrder
            ?? throw new InvalidOperationException(
                "Choisissez un ordre disponible."
            );
        SeriesDetails result = await api.SeriesAsync(
            match.Id,
            order.Code,
            cancellationToken: OperationToken
        );
        card.ApplyTvdb(result, order.Code);
        Notify(nameof(SelectedCard));
        Status =
            "Informations TVDB appliquées au brouillon. Cliquez sur Enregistrer.";
    }

    /// <summary>
    /// Imports artwork into the current card's unsaved draft.
    /// </summary>
    public void ImportImage(ArtworkKind kind, string path)
    {
        MediaCard card =
            SelectedCard
            ?? throw new InvalidOperationException(
                "Sélectionnez une fiche."
            );
        _importer.Import(card, kind, path, SelectedEpisode);
        RefreshImages();
        Status =
            "Image importée dans le brouillon. Cliquez sur Enregistrer pour la conserver.";
    }

    /// <summary>
    /// Saves episode edits before moving to an adjacent
    /// episode.
    /// </summary>
    public async Task NavigateAndSaveAsync(int direction)
    {
        if (SelectedCard is null || SelectedEpisode is null)
        {
            return;
        }

        int index =
            SelectedCard.Episodes.IndexOf(SelectedEpisode)
            + direction;
        if (index < 0 || index >= SelectedCard.Episodes.Count)
        {
            return;
        }

        if (SelectedCard.IsDirty)
        {
            await _repository.SaveAsync(SelectedCard);
        }

        SelectedEpisode = SelectedCard.Episodes[index];
        Status = "Épisode enregistré.";
    }

    /// <summary>
    /// Reloads artwork and the rendered preview for the
    /// selection.
    /// </summary>
    public void RefreshImages()
    {
        Poster = LoadImage(SelectedCard?.PosterPath);
        Banner = LoadImage(SelectedCard?.BannerPath);
        Fanart = LoadImage(SelectedCard?.FanartPath);
        EpisodeImage = LoadImage(SelectedEpisode?.ImagePath);
        RefreshRenderedPreview();
    }

    private static Bitmap? LoadImage(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(
                File.ReadAllBytes(path)
            );
            return new Bitmap(stream);
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or ArgumentException
                        or InvalidDataException
            )
        {
            return null;
        }
    }

    private void Replace(
        ref Bitmap? field,
        Bitmap? value,
        [CallerMemberName] string? name = null
    )
    {
        Bitmap? previous = field;
        field = value;
        Notify(name);
        previous?.Dispose();
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
    /// Releases owned resources and temporary artwork, where
    /// applicable.
    /// </summary>
    public void Dispose()
    {
        foreach (MediaCard card in Cards)
        {
            card.PropertyChanged -= CardChanged;
            card.Dispose();
        }

        _poster?.Dispose();
        _banner?.Dispose();
        _fanart?.Dispose();
        _episodeImage?.Dispose();
        _renderedPreview?.Dispose();
        _activeOperation?.Cancel();
        _http.Dispose();
    }
}

internal sealed class UiCommand(Func<Task> action) : ICommand
{
    private bool _running;

    /// <summary>
    /// Notifies command bindings when execution availability
    /// changes.
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Indicates whether the command is currently idle.
    /// </summary>
    public bool CanExecute(object? parameter)
    {
        return !_running;
    }

    /// <summary>
    /// Runs the command while preventing overlapping
    /// executions.
    /// </summary>
    public async void Execute(object? parameter)
    {
        if (_running)
        {
            return;
        }

        _running = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            await action();
        }
        finally
        {
            _running = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

internal static class ListExtensions
{
    /// <summary>
    /// Finds an item's index using the default equality
    /// comparer.
    /// </summary>
    public static int IndexOf<T>(
        this IReadOnlyList<T> source,
        T value
    )
    {
        for (var i = 0; i < source.Count; i++)
        {
            if (
                EqualityComparer<T>.Default.Equals(
                    source[i],
                    value
                )
            )
            {
                return i;
            }
        }

        return -1;
    }
}
