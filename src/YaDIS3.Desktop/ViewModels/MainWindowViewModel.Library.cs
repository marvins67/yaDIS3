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
    public IReadOnlyList<string> MediaFilters { get; } =
    ["Films et séries", "Films", "Séries TV"];

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
            ? "Cliquez sur Traiter pour créer l’aperçu de cette fiche."
            : "Ouvrez une bibliothèque et sélectionnez un film ou une série.";

    /// <summary>
    /// Libellé de l'onglet d'informations du média sélectionné.
    /// </summary>
    public string MediaTabTitle =>
        IsSeries ? "Série TV" : "Film";

    /// <summary>
    /// Nombre de fiches visibles et chargées.
    /// </summary>
    public string VisibleCountLabel =>
        VisibleCards.Count == 0
            ? ""
            : $"{VisibleCards.Count} "
                + (IsSeriesFilter ? "Séries TV" : "Films");

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
        IsSeriesFilter ? "Séries TV" : "Films";

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
        && card.ProcessingState
            is MediaProcessingState.Checked
                or MediaProcessingState.Unchecked
        && card.HasInformation
            ? card
            : null;

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
