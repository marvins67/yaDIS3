using System.Windows.Input;
using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Desktop.ViewModels;

/// <summary>
/// Filtres de bibliothèque et libellés de sélection.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private int _mediaFilterIndex = 1;

    private LegacyLibraryFilter _stateFilter = new();

    private XDocument _filterGroups = new(
        new XElement("groups")
    );

    private bool _invertStateFilter;

    /// <summary>
    /// Catégories de médias disponibles.
    /// </summary>
    public IReadOnlyList<string> MediaFilters { get; }

    /// <summary>
    /// Catégorie de médias sélectionnée.
    /// </summary>
    public int MediaFilterIndex
    {
        get => _mediaFilterIndex;
        set
        {
            _mediaFilterIndex = value;
            if (
                value == 2
                && _stateFilter.Kind
                    > LibraryFilterKind.Unprocessed
            )
            {
                SelectStateFilter(StateFilters[0]);
            }
            RefreshStateMenu();
            RefreshFilter();
            Notify();
            Notify(nameof(IsSeriesFilter));
            Notify(nameof(MediaFilterLabel));
            Notify(nameof(IsSeries));
            Notify(nameof(MediaTabTitle));
        }
    }

    /// <summary>
    /// Choisit l'icône télévision lorsque le filtre porte sur
    /// les séries.
    /// </summary>
    public bool IsSeriesFilter => MediaFilterIndex == 2;

    /// <summary>
    /// Inverse le filtre de l'état sélectionné.
    /// </summary>
    public bool InvertStateFilter
    {
        get => _invertStateFilter;
        set
        {
            _invertStateFilter = value;
            RefreshFilter();
            Notify();
        }
    }

    /// <summary>
    /// Indique si un workspace est ouvert.
    /// </summary>
    public bool HasWorkspace => _workspace is not null;

    /// <summary>
    /// Indique si la sélection possède un aperçu généré.
    /// </summary>
    public bool HasRenderedPreview =>
        RenderedPreview is not null;

    /// <summary>
    /// Message affiché en l'absence d'aperçu pour la sélection.
    /// </summary>
    public string PreviewHint =>
        HasCard
            ? Texts.GetString("334")
            : Texts.GetString("335");

    /// <summary>
    /// Libellé de l'onglet d'informations du média sélectionné.
    /// </summary>
    public string MediaTabTitle =>
        IsSeries ? Texts.GetString("21") : Texts.GetString("15");

    /// <summary>
    /// Nombre de fiches visibles et chargées.
    /// </summary>
    public string VisibleCountLabel =>
        VisibleCards.Count == 0
            ? ""
            : $"{VisibleCards.Count} "
                + (
                    IsSeriesFilter
                        ? Texts.GetString("64")
                        : Texts.GetString("63")
                );

    /// <summary>
    /// Noms des acteurs conservés dans les métadonnées
    /// sélectionnées.
    /// </summary>
    public string ActorsText => SelectedCard?.Actors ?? "";

    /// <summary>
    /// Genres conservés dans les métadonnées sélectionnées.
    /// </summary>
    public string GenresText => SelectedCard?.Genres ?? "";

    /// <summary>Libellé du sélecteur de type historique.</summary>
    public string MediaFilterLabel =>
        IsSeriesFilter
            ? Texts.GetString("64")
            : Texts.GetString("63");

    /// <summary>Alterner films et séries par le clic principal.</summary>
    public ICommand ToggleMediaFilterCommand { get; }

    /// <summary>Choisir explicitement les films.</summary>
    public ICommand SelectMoviesCommand { get; }

    /// <summary>Choisir explicitement les séries.</summary>
    public ICommand SelectSeriesCommand { get; }

    private Task SelectMediaAsync(int index)
    {
        MediaFilterIndex = index;
        return Task.CompletedTask;
    }

    /// <summary>Le traitement exclut les sources manquantes.</summary>
    public bool CanProcessSelection =>
        SelectedCard is { } card
        && card.ProcessingState != MediaProcessingState.Missing;

    /// <summary>Désactive et vide l'éditeur sans fiche traitée.</summary>
    public MediaCard? EditableCard =>
        SelectedCard is { } card
        && (
            card.HasInformation
            && card.ProcessingState is
                MediaProcessingState.Checked
                or MediaProcessingState.Unchecked
            || _manualMovieEdit
                && !card.IsSeries
                && card.ProcessingState ==
                    MediaProcessingState.Unprocessed
        )
            ? card
            : null;

    /// <summary>Active l'édition manuelle des films non traités.</summary>
    public void BeginManualMovieEdit()
    {
        MediaCard? card = SelectedCard;
        if (
            card is null
            || card.IsSeries
            || card.ProcessingState !=
                MediaProcessingState.Unprocessed
        )
        {
            return;
        }

        card.BeginManualMovieEdit();
        _manualMovieEdit = true;
        Notify(nameof(EditableCard));
        Notify(nameof(CanEditSelection));
    }

    /// <summary>Activation des onglets comme EnableEdit/DisableEdit.</summary>
    public bool CanEditSelection =>
        EditableCard is not null && !IsBusy;

    private bool MatchesLibraryFilters(MediaCard card)
    {
        return MatchesMediaCategory(card)
            && (
                _stateFilter with
                {
                    Inverted = InvertStateFilter,
                }
            ).Matches(card, _filterGroups)
            && card.DisplayName.Contains(
                Filter,
                StringComparison.CurrentCultureIgnoreCase
            );
    }
}
