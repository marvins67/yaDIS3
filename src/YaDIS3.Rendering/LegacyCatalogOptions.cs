namespace YaDIS3.Rendering;

/// <summary>
/// Conserve les valeurs par défaut de la configuration yaDIS2.
/// </summary>
public sealed record LegacyCatalogOptions
{
    /// <summary>
    /// Définit les catégories 02 à 11 visibles dans le menu.
    /// </summary>
    public string Categories { get; init; } = "11111111111";

    /// <summary>
    /// Conserve la fenêtre de nouveautés de soixante jours.
    /// </summary>
    public int LastDays { get; init; } = 60;

    /// <summary>
    /// Préfixe les légendes récentes par leur ancienneté si
    /// demandé.
    /// </summary>
    public bool SortRecentByDate { get; init; }

    /// <summary>
    /// Conserve le cadre de sélection configuré dans yaDIS2.
    /// </summary>
    public bool SelectionBox { get; init; } = true;

    /// <summary>
    /// Conserve la barre de défilement configurée dans yaDIS2.
    /// </summary>
    public bool Scrollbar { get; init; } = true;
}
