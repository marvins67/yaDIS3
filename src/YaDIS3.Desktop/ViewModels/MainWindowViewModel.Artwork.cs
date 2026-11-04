using System.Collections.ObjectModel;
using YaDIS3.Core;

namespace YaDIS3.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private MediaCard? _catalogCard;
    private ArtworkReference[] _catalog = [];
    private CancellationTokenSource? _catalogLoading;
    private ArtworkKind? _visibleArtworkKind;

    /// <summary>Affiches du catalogue de la fiche sélectionnée.</summary>
    public ObservableCollection<ArtworkChoiceViewModel> PosterChoices
    {
        get;
    } = [];

    /// <summary>Fanarts du catalogue de la fiche sélectionnée.</summary>
    public ObservableCollection<ArtworkChoiceViewModel> FanartChoices
    {
        get;
    } = [];

    private void RefreshArtworkChoices()
    {
        ArtworkReference[] catalog =
            SelectedCard?.MovieArtwork.ToArray() ?? [];
        if (ReferenceEquals(_catalogCard, SelectedCard)
            && _catalog.SequenceEqual(catalog))
        {
            return;
        }
        ClearArtworkChoices();
        _catalogCard = SelectedCard;
        _catalog = catalog;
        foreach (ArtworkReference reference in catalog)
        {
            var choice = new ArtworkChoiceViewModel(
                reference,
                () => RunAsync(() => SelectArtworkAsync(
                    reference, OperationToken
                )),
                Texts.GetString("107")
            );
            Choices(reference.Kind).Add(choice);
        }
        if (_visibleArtworkKind is { } kind)
        {
            _ = LoadArtworkChoicesAsync(kind);
        }
    }

    private ObservableCollection<ArtworkChoiceViewModel> Choices(
        ArtworkKind kind
    ) => kind == ArtworkKind.Poster
        ? PosterChoices : FanartChoices;

    /// <summary>
    /// Charge les vignettes à l'ouverture de leur onglet ; une
    /// erreur d'image laisse les autres choix disponibles.
    /// </summary>
    public async Task LoadArtworkChoicesAsync(
        ArtworkKind kind,
        CancellationToken cancellationToken = default
    )
    {
        _visibleArtworkKind = kind;
        _catalogLoading?.Cancel();
        if (SelectedCard is not { IsSeries: false } card
            || _disposed)
        {
            return;
        }
        using CancellationTokenSource loading =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );
        _catalogLoading = loading;
        try
        {
            await LoadArtworkThumbnailsAsync(
                card, kind, loading.Token
            );
        }
        catch (OperationCanceledException)
            when (loading.IsCancellationRequested)
        {
            // Le changement de fiche abandonne son ancien catalogue.
        }
        finally
        {
            if (ReferenceEquals(_catalogLoading, loading))
            {
                _catalogLoading = null;
            }
        }
    }

    private async Task LoadArtworkThumbnailsAsync(
        MediaCard card,
        ArtworkKind kind,
        CancellationToken cancellationToken
    )
    {
        string workspace = Path.GetDirectoryName(
            Path.GetDirectoryName(card.DataDirectory)
        )!;
        MovieArtworkService service = new(_http, _importer);
        ArtworkChoiceViewModel[] choices = Choices(kind).ToArray();
        foreach (ArtworkChoiceViewModel choice in choices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (choice.Thumbnail is not null)
            {
                continue;
            }
            try
            {
                string path = await service.ThumbnailAsync(
                    choice.Reference,
                    workspace,
                    cancellationToken
                );
                cancellationToken.ThrowIfCancellationRequested();
                choice.Thumbnail = LoadImage(path)
                    ?? throw new InvalidDataException(path);
                choice.Hint = choice.Reference.Url;
            }
            catch (Exception exception)
                when (exception is not OperationCanceledException)
            {
                OperationFailed(_logger, exception);
                choice.Hint = exception.Message;
                choice.MarkFailed();
            }
        }
    }

    /// <summary>Applique l'original sans enregistrer le brouillon.</summary>
    public async Task SelectArtworkAsync(
        ArtworkReference reference,
        CancellationToken cancellationToken = default
    )
    {
        MediaCard card = SelectedCard
            ?? throw new InvalidOperationException(Texts["330"]);
        await new MovieArtworkService(_http, _importer)
            .SelectAsync(card, reference, cancellationToken);
        if (ReferenceEquals(SelectedCard, card))
        {
            foreach (ArtworkChoiceViewModel choice in
                Choices(reference.Kind))
            {
                choice.IsSelected =
                    choice.Reference == reference;
            }
            RefreshImages();
        }
        Status = Texts.GetString("331");
    }

    private void ClearArtworkChoices()
    {
        _catalogLoading?.Cancel();
        foreach (ArtworkChoiceViewModel choice in
            PosterChoices.Concat(FanartChoices))
        {
            choice.Dispose();
        }
        PosterChoices.Clear();
        FanartChoices.Clear();
    }
}
