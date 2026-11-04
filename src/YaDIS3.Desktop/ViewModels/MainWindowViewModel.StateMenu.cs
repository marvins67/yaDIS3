using System.Windows.Input;
using YaDIS3.Core;

namespace YaDIS3.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string _stateFilterLabel = "";

    /// <summary>Libellé du filtre actif.</summary>
    public string StateFilterLabel => _stateFilterLabel;

    /// <summary>États dans l'ordre du bouton yaDIS2.</summary>
    public IReadOnlyList<LibraryFilterItem> StateFilters { get; }

    /// <summary>Sagas précédées du filtre global.</summary>
    public IReadOnlyList<LibraryFilterItem> SagaFilters
    {
        get;
        private set;
    } = [];

    /// <summary>Sélections précédées du filtre global.</summary>
    public IReadOnlyList<LibraryFilterItem> SelectionFilters
    {
        get;
        private set;
    } = [];

    /// <summary>Genres du type de média courant.</summary>
    public IReadOnlyList<LibraryFilterItem> GenreFilters
    {
        get;
        private set;
    } = [];

    /// <summary>
    /// Les groupes Dune ne concernent que les films.
    /// </summary>
    public bool ShowSagaFilters =>
        !IsSeriesFilter
        && !_configuration.BasicMode
        && SagaFilters.Count > 1;

    /// <summary>
    /// Les groupes désactivés restent filtrables dans yaDIS2.
    /// </summary>
    public bool ShowSelectionFilters =>
        !IsSeriesFilter
        && !_configuration.BasicMode
        && SelectionFilters.Count > 1;

    /// <summary>Masque le menu sans genre disponible.</summary>
    public bool ShowGenreFilters => GenreFilters.Count > 0;

    /// <summary>Parcourt les états lors du clic principal.</summary>
    public ICommand CycleStateFilterCommand { get; }

    private LibraryFilterItem[] CreateStateFilters() =>
        [
            StateItem(
                Texts.GetString("56"),
                LibraryFilterKind.All
            ),
            StateItem(
                Texts.GetString("85"),
                LibraryFilterKind.Checked
            ),
            StateItem(
                Texts.GetString("67"),
                LibraryFilterKind.Unchecked
            ),
            StateItem(
                Texts.GetString("68"),
                LibraryFilterKind.Missing
            ),
            StateItem(
                Texts.GetString("69"),
                LibraryFilterKind.Unprocessed
            ),
        ];

    private Task CycleStateFilterAsync()
    {
        int index = (int)_stateFilter.Kind;
        SelectStateFilter(
            StateFilters[
                index < StateFilters.Count - 1 ? index + 1 : 0
            ]
        );
        return Task.CompletedTask;
    }

    private LibraryFilterItem StateItem(
        string label,
        LibraryFilterKind kind
    ) => new(label, new(kind), SelectStateFilter);

    private void SelectStateFilter(LibraryFilterItem item)
    {
        _stateFilter = item.Filter;
        _stateFilterLabel = item.Label;
        RefreshFilter();
        Notify(nameof(StateFilterLabel));
    }

    private void ReloadFilterGroups()
    {
        if (_workspace is not null)
        {
            _filterGroups = _configurationStore
                .Load(_workspace.Directory)
                .Groups;
        }
        RefreshStateMenu();
    }

    private void RefreshStateMenu()
    {
        SagaFilters = GroupFilters(
            "saga",
            LibraryFilterKind.Saga,
            Texts.GetString("198"),
            LibraryFilterKind.AllSagas
        );
        SelectionFilters = GroupFilters(
            "selection",
            LibraryFilterKind.Selection,
            Texts.GetString("199"),
            LibraryFilterKind.AllSelections
        );
        GenreFilters = Cards
            .Where(MatchesMediaCategory)
            .SelectMany(LegacyLibraryFilter.Genres)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.CurrentCulture)
            .Select(name => new LibraryFilterItem(
                name,
                new(LibraryFilterKind.Genre, name),
                SelectStateFilter
            ))
            .ToArray();
        NotifyStateMenu();
    }

    private void NotifyStateMenu()
    {
        Notify(nameof(SagaFilters));
        Notify(nameof(SelectionFilters));
        Notify(nameof(GenreFilters));
        Notify(nameof(ShowSagaFilters));
        Notify(nameof(ShowSelectionFilters));
        Notify(nameof(ShowGenreFilters));
    }

    private LibraryFilterItem[] GroupFilters(
        string element,
        LibraryFilterKind kind,
        string allLabel,
        LibraryFilterKind allKind
    ) =>
        [
            StateItem(allLabel, allKind),
            .. _filterGroups
                .Descendants(element)
                .Select(node =>
                    node.Element("name")?.Value ?? ""
                )
                .Order(StringComparer.CurrentCulture)
                .Select(name => new LibraryFilterItem(
                    name,
                    new(kind, name),
                    SelectStateFilter
                )),
        ];

    private bool MatchesMediaCategory(MediaCard card) =>
        MediaFilterIndex switch
        {
            1 => !card.IsSeries,
            2 => card.IsSeries,
            _ => true,
        };
}
