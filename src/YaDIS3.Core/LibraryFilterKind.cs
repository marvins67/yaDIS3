namespace YaDIS3.Core;

/// <summary>
/// Filtres du bouton d'état, dans l'ordre de yaDIS2.
/// </summary>
public enum LibraryFilterKind
{
    /// <summary>Tous les statuts.</summary>
    All,

    /// <summary>Statut zéro.</summary>
    Checked,

    /// <summary>Statut un.</summary>
    Unchecked,

    /// <summary>Statut deux.</summary>
    Missing,

    /// <summary>Statut trois.</summary>
    Unprocessed,

    /// <summary>Une saga nommée.</summary>
    Saga,

    /// <summary>Une sélection nommée.</summary>
    Selection,

    /// <summary>Un genre exact.</summary>
    Genre,

    /// <summary>Films appartenant à une saga.</summary>
    AllSagas,

    /// <summary>Films appartenant à une sélection.</summary>
    AllSelections,
}
