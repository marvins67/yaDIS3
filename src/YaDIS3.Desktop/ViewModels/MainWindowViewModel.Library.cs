using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Desktop.ViewModels;

/// <summary>
/// Provides the compact library filters and selection labels.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private int _mediaFilterIndex;

    private int _stateFilterIndex;

    private bool _invertStateFilter;

    /// <summary>
    /// Gets the available media categories.
    /// </summary>
    public IReadOnlyList<string> MediaFilters { get; } =
    ["Films et séries", "Films", "Séries TV"];

    /// <summary>
    /// Gets the available preview and edit states.
    /// </summary>
    public IReadOnlyList<string> StateFilters { get; } =
    ["Tous les états", "Avec aperçu", "Sans aperçu", "Modifiés"];

    /// <summary>
    /// Gets or sets the selected media category.
    /// </summary>
    public int MediaFilterIndex
    {
        get => _mediaFilterIndex;
        set
        {
            _mediaFilterIndex = value;
            RefreshFilter();
            Notify();
        }
    }

    /// <summary>
    /// Gets or sets the selected preview or edit state.
    /// </summary>
    public int StateFilterIndex
    {
        get => _stateFilterIndex;
        set
        {
            _stateFilterIndex = value;
            RefreshFilter();
            Notify();
        }
    }

    /// <summary>
    /// Gets or sets whether the selected state is excluded.
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
    /// Indicates whether a workspace has been opened.
    /// </summary>
    public bool HasWorkspace => _workspace is not null;

    /// <summary>
    /// Indicates whether the selection has a rendered preview.
    /// </summary>
    public bool HasRenderedPreview =>
        RenderedPreview is not null;

    /// <summary>
    /// Gets the empty-preview guidance for the current selection.
    /// </summary>
    public string PreviewHint =>
        HasCard
            ? "Cliquez sur Traiter pour créer l’aperçu de cette fiche."
            : "Ouvrez une bibliothèque et sélectionnez un film ou une série.";

    /// <summary>
    /// Gets the label for the selected media's information tab.
    /// </summary>
    public string MediaTabTitle =>
        IsSeries ? "Série TV" : "Film";

    /// <summary>
    /// Gets the number of visible and loaded cards.
    /// </summary>
    public string VisibleCountLabel =>
        $"{VisibleCards.Count} / {Cards.Count} fiche(s)";

    /// <summary>
    /// Gets the cast names preserved in the selected metadata.
    /// </summary>
    public string ActorsText => ReadNames("actors", "actor");

    /// <summary>
    /// Gets the genres preserved in the selected metadata.
    /// </summary>
    public string GenresText => ReadNames("genres", "genre");

    private string ReadNames(string container, string element)
    {
        XElement? root = SelectedCard?.Snapshot().Document.Root;
        XElement? info =
            root?.Element("info")
            ?? root?.Element("item")?.Element("info");
        XElement? group = info?.Element(container);
        if (group is null)
        {
            return "";
        }

        string[] names = group
            .Elements(element)
            .Select(item =>
                item.Element("name")?.Value ?? item.Value
            )
            .ToArray();
        return names.Length > 0
            ? string.Join(", ", names)
            : group.Value.Replace('|', ',');
    }

    private bool MatchesLibraryFilters(MediaCard card)
    {
        bool matchesCategory = MediaFilterIndex switch
        {
            1 => !card.IsSeries,
            2 => card.IsSeries,
            _ => true,
        };
        bool hasPreview = File.Exists(
            Path.Combine(card.DataDirectory, "preview.jpg")
        );
        bool matchesState = StateFilterIndex switch
        {
            1 => hasPreview,
            2 => !hasPreview,
            3 => card.IsDirty,
            _ => true,
        };
        if (InvertStateFilter && StateFilterIndex != 0)
        {
            matchesState = !matchesState;
        }

        return matchesCategory
            && matchesState
            && card.DisplayName.Contains(
                Filter,
                StringComparison.CurrentCultureIgnoreCase
            );
    }
}
