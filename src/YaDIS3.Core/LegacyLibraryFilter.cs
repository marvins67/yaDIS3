using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>
/// Reprend les prédicats RefreshMediaList de yaDIS2.
/// Le type de média et la recherche restent indépendants.
/// </summary>
public sealed record LegacyLibraryFilter(
    LibraryFilterKind Kind = LibraryFilterKind.All,
    string Name = "",
    bool Inverted = false
)
{
    /// <summary>
    /// Compare le statut XML ou l'appartenance exacte, sans
    /// confondre un aperçu présent avec un traitement réussi.
    /// </summary>
    public bool Matches(MediaCard card, XDocument groups)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(groups);
        bool matches = MatchesCard(card, groups);
        // yaDIS2 inverse aussi « Tous les états » :
        // aucune fiche ne reste alors visible.
        return Inverted ? !matches : matches;
    }

    private bool MatchesCard(MediaCard card, XDocument groups)
    {
        MediaProcessingState status = card.ProcessingState;
        return Kind switch
        {
            LibraryFilterKind.All => true,
            LibraryFilterKind.Checked => status
                == MediaProcessingState.Checked,
            LibraryFilterKind.Unchecked => status
                == MediaProcessingState.Unchecked,
            LibraryFilterKind.Missing => status
                == MediaProcessingState.Missing,
            LibraryFilterKind.Unprocessed => status
                == MediaProcessingState.Unprocessed,
            LibraryFilterKind.Genre => Genres(card)
                .Contains(Name, StringComparer.Ordinal),
            _ => MatchesGroup(card, groups),
        };
    }

    /// <summary>
    /// Relit les éléments genre, y compris le XML historique.
    /// </summary>
    public static IEnumerable<string> Genres(MediaCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.Snapshot()
            .Document.Descendants("genre")
            .Select(node => node.Value);
    }

    private bool MatchesGroup(MediaCard card, XDocument groups)
    {
        string element = Kind switch
        {
            LibraryFilterKind.Saga
            or LibraryFilterKind.AllSagas => "saga",
            _ => "selection",
        };
        bool all =
            Kind
            is LibraryFilterKind.AllSagas
                or LibraryFilterKind.AllSelections;
        return groups
            .Descendants(element)
            .Where(group =>
                all || group.Element("name")?.Value == Name
            )
            .Any(group =>
                group
                    .Elements("movie")
                    .Any(movie => movie.Value == card.FolderName)
            );
    }
}
