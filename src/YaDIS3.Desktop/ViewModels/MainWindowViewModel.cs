using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using YaDIS3.Core;
namespace YaDIS3.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly WorkspaceRepository repository = new();
    private readonly ImageImporter importer = new(new SkiaJpegEncoder());
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(45) };
    private TvdbClient? tvdb;
    private Workspace? workspace;
    private MediaCard? selectedCard;
    private Episode? selectedEpisode;
    private SeriesMatch? selectedMatch;
    private EpisodeOrder? selectedOrder;
    private Bitmap? poster, fanart, banner, episodeImage;
    private string filter = "", query = "", credentials = "TVDB non configuré";
    private string status = "Ouvrez une copie de votre workspace yaDIS3 pour commencer.";
    private bool busy;
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<MediaCard> Cards { get; } = [];
    public ObservableCollection<MediaCard> VisibleCards { get; } = [];
    public ObservableCollection<SeriesMatch> SearchResults { get; } = [];
    public ObservableCollection<EpisodeOrder> Orders { get; } = [];
    public ICommand SaveCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ApplySeriesCommand { get; }
    public ICommand PreviousEpisodeCommand { get; }
    public ICommand NextEpisodeCommand { get; }
    public string WorkspacePath => workspace?.Directory ?? "Aucune bibliothèque ouverte";
    public bool HasChanges => Cards.Any(c => c.IsDirty);
    public bool HasCard => SelectedCard is not null;
    public bool IsSeries => SelectedCard?.IsSeries == true;
    public bool HasEpisode => SelectedEpisode is not null;
    public string CountLabel => $"{Cards.Count} fiche(s) · {Cards.Count(c => c.IsDirty)} modifiée(s)";
    public string MigrationNote => "Édition, rendu des templates et export Dune disponibles. Choisissez un template puis Traiter, ou exportez la bibliothèque depuis l’onglet Export Dune.";
    public string EpisodePosition => SelectedCard is null || SelectedEpisode is null ? "Aucun épisode — analysez un dossier de vidéos." : $"Épisode {SelectedCard.Episodes.IndexOf(SelectedEpisode) + 1} / {SelectedCard.Episodes.Count}";
    public string Status { get => status; set { status = value; Notify(); } }
    public bool IsBusy { get => busy; private set { busy = value; Notify(); } }
    public string Query { get => query; set { query = value; Notify(); } }
    public string Filter { get => filter; set { filter = value; Notify(); RefreshFilter(); } }
    public string CredentialsLabel { get => credentials; private set { credentials = value; Notify(); } }
    public Bitmap? Poster { get => poster; private set => Replace(ref poster, value); }
    public Bitmap? Fanart { get => fanart; private set => Replace(ref fanart, value); }
    public Bitmap? Banner { get => banner; private set => Replace(ref banner, value); }
    public Bitmap? EpisodeImage { get => episodeImage; private set => Replace(ref episodeImage, value); }
    public MediaCard? SelectedCard
    {
        get => selectedCard;
        set
        {
            if (ReferenceEquals(value, selectedCard)) return;
            selectedCard = value; Query = value?.Title ?? "";
            SearchResults.Clear(); Orders.Clear(); SelectedMatch = null; SelectedOrder = null;
            SelectedEpisode = value?.Episodes.FirstOrDefault(); RefreshImages(); NotifyAll();
        }
    }
    public Episode? SelectedEpisode
    {
        get => selectedEpisode;
        set { selectedEpisode = value; EpisodeImage = LoadImage(value?.ImagePath); Notify(); Notify(nameof(HasEpisode)); Notify(nameof(EpisodePosition)); RefreshRenderedPreview(); }
    }
    public SeriesMatch? SelectedMatch { get => selectedMatch; set { selectedMatch = value; Notify(); } }
    public EpisodeOrder? SelectedOrder { get => selectedOrder; set { selectedOrder = value; Notify(); } }
    public MainWindowViewModel()
    {
        InitializeDune();
        SaveCommand = new UiCommand(() => RunAsync(SaveAllAsync));
        SearchCommand = new UiCommand(() => RunAsync(SearchAsync));
        ApplySeriesCommand = new UiCommand(() => RunAsync(ApplySeriesAsync));
        PreviousEpisodeCommand = new UiCommand(() => RunAsync(() => NavigateAndSaveAsync(-1)));
        NextEpisodeCommand = new UiCommand(() => RunAsync(() => NavigateAndSaveAsync(1)));
    }
    public async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true; activeOperation = new CancellationTokenSource();
        try { await action(); }
        catch (OperationCanceledException) { Status = "Opération annulée."; }
        catch (Exception e) { Status = e.Message; }
        finally { activeOperation.Dispose(); activeOperation = null; IsBusy = false; NotifyAll(); }
    }
    public async Task OpenAsync(string path, bool create = false)
    {
        if (create) repository.Create(path);
        var loaded = await Task.Run(() => repository.Open(path));
        foreach (var card in Cards) { card.PropertyChanged -= CardChanged; card.Dispose(); }
        workspace = loaded; Cards.Clear(); VisibleCards.Clear(); LoadDunePreferences();
        foreach (var card in loaded.Cards) { Cards.Add(card); card.PropertyChanged += CardChanged; }
        RefreshFilter(); SelectedCard = VisibleCards.FirstOrDefault();
        Status = loaded.Warnings.Count == 0 ? "Bibliothèque ouverte." : string.Join(" · ", loaded.Warnings);
        NotifyAll();
    }
    private void CardChanged(object? sender, PropertyChangedEventArgs args) { Notify(nameof(CountLabel)); Notify(nameof(HasChanges)); }
    private void RefreshFilter()
    {
        var previous = selectedCard;
        VisibleCards.Clear();
        foreach (var card in Cards.Where(c => c.DisplayName.Contains(Filter, StringComparison.CurrentCultureIgnoreCase))) VisibleCards.Add(card);
        SelectedCard = previous is not null && VisibleCards.Contains(previous) ? previous : VisibleCards.FirstOrDefault();
    }
    public async Task SaveAllAsync()
    {
        foreach (var card in Cards.Where(c => c.IsDirty)) await repository.SaveAsync(card, OperationToken);
        Status = "Modifications enregistrées. Les copies précédentes portent le suffixe .yadis3-modern.bak.";
        RefreshImages(); NotifyAll();
    }
    public async Task AddManualSeriesAsync(string directory)
    {
        var current = workspace ?? throw new InvalidOperationException("Ouvrez ou créez d'abord une bibliothèque.");
        var detected = await Task.Run(() => EpisodeScanner.Detect(directory));
        var card = repository.CreateManualSeries(current, directory, detected);
        await repository.SaveAsync(card, OperationToken); Cards.Add(card); card.PropertyChanged += CardChanged;
        Filter = ""; SelectedCard = card; Status = $"Série manuelle créée : {card.Episodes.Count} épisode(s).";
    }
    public async Task ScanAsync(string directory)
    {
        var card = SelectedCard ?? throw new InvalidOperationException("Sélectionnez une série.");
        var found = await Task.Run(() => EpisodeScanner.Detect(directory));
        EpisodeScanner.Synchronize(card, found); SelectedEpisode = card.Episodes.FirstOrDefault();
        Notify(nameof(SelectedCard)); Status = $"{card.Episodes.Count} épisode(s). Les informations existantes sont conservées ; enregistrez les changements.";
    }
    public void ConfigureTvdb(string file)
    {
        tvdb = new TvdbClient(http, TvdbSettings.Load(file)); CredentialsLabel = "TVDB v4 configuré"; Status = "Configuration TVDB chargée.";
    }
    public async Task SearchAsync()
    {
        var api = tvdb ?? throw new InvalidOperationException("Chargez votre tvdb-v4.json avec Configuration TVDB.");
        var found = await api.SearchAsync(Query, cancellationToken: OperationToken);
        SearchResults.Clear(); Orders.Clear(); SelectedMatch = null; SelectedOrder = null;
        foreach (var match in found) SearchResults.Add(match);
        Status = found.Count == 0 ? "Aucune série trouvée. Vous pouvez ajouter une série manuelle depuis son dossier." : $"{found.Count} résultat(s) TVDB.";
    }
    public async Task LoadOrdersAsync()
    {
        var match = SelectedMatch; if (match is null || tvdb is null) return;
        Orders.Clear(); SelectedOrder = null;
        var available = await tvdb.AvailableOrdersAsync(match.Id, OperationToken);
        if (SelectedMatch != match) return;
        foreach (var order in available) Orders.Add(order);
        SelectedOrder = available.FirstOrDefault(o => o.Code == SelectedCard?.Order) ?? available.FirstOrDefault();
    }
    public async Task ApplySeriesAsync()
    {
        var api = tvdb ?? throw new InvalidOperationException("TVDB non configuré.");
        var card = SelectedCard ?? throw new InvalidOperationException("Sélectionnez une série.");
        var match = SelectedMatch ?? throw new InvalidOperationException("Choisissez une série dans les résultats.");
        var order = SelectedOrder ?? throw new InvalidOperationException("Choisissez un ordre disponible.");
        var result = await api.SeriesAsync(match.Id, order.Code, cancellationToken: OperationToken);
        card.ApplyTvdb(result, order.Code); Notify(nameof(SelectedCard));
        Status = "Informations TVDB appliquées au brouillon. Cliquez sur Enregistrer.";
    }
    public void ImportImage(ArtworkKind kind, string path)
    {
        var card = SelectedCard ?? throw new InvalidOperationException("Sélectionnez une fiche.");
        importer.Import(card, kind, path, SelectedEpisode); RefreshImages(); Status = "Image importée dans le brouillon. Cliquez sur Enregistrer pour la conserver.";
    }
    public async Task NavigateAndSaveAsync(int direction)
    {
        if (SelectedCard is null || SelectedEpisode is null) return;
        var index = SelectedCard.Episodes.IndexOf(SelectedEpisode) + direction;
        if (index < 0 || index >= SelectedCard.Episodes.Count) return;
        if (SelectedCard.IsDirty) await repository.SaveAsync(SelectedCard);
        SelectedEpisode = SelectedCard.Episodes[index]; Status = "Épisode enregistré.";
    }
    public void RefreshImages()
    {
        Poster = LoadImage(SelectedCard?.PosterPath); Banner = LoadImage(SelectedCard?.BannerPath);
        Fanart = LoadImage(SelectedCard?.FanartPath); EpisodeImage = LoadImage(SelectedEpisode?.ImagePath); RefreshRenderedPreview();
    }
    private static Bitmap? LoadImage(string? path)
    {
        if (path is null || !File.Exists(path)) return null;
        try { using var stream = new MemoryStream(File.ReadAllBytes(path)); return new Bitmap(stream); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException) { return null; }
    }
    private void Replace(ref Bitmap? field, Bitmap? value, [CallerMemberName] string? name = null)
    { var previous = field; field = value; Notify(name); previous?.Dispose(); }
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private void NotifyAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    public void Dispose()
    {
        foreach (var card in Cards) { card.PropertyChanged -= CardChanged; card.Dispose(); }
        poster?.Dispose(); banner?.Dispose(); fanart?.Dispose(); episodeImage?.Dispose(); renderedPreview?.Dispose(); activeOperation?.Cancel(); http.Dispose();
    }
}
internal sealed class UiCommand(Func<Task> action) : ICommand
{
    private bool running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !running;
    public async void Execute(object? parameter)
    {
        if (running) return;
        running = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await action(); }
        finally { running = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }
}
internal static class ListExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> source, T value)
    { for (var i = 0; i < source.Count; i++) if (EqualityComparer<T>.Default.Equals(source[i], value)) return i; return -1; }
}
