namespace YaDIS3.Core;

/// <summary>
/// Valeurs du champ item/status de yaDIS2.
/// </summary>
public enum MediaProcessingState
{
    /// <summary>Statut absent ou non reconnu.</summary>
    Unknown = -1,

    /// <summary>Fiche vérifiée par l'utilisateur.</summary>
    Checked = 0,

    /// <summary>Fiche traitée, non vérifiée.</summary>
    Unchecked = 1,

    /// <summary>Source vidéo introuvable.</summary>
    Missing = 2,

    /// <summary>Fiche pas encore traitée.</summary>
    Unprocessed = 3,
}
