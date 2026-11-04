namespace YaDIS3.Core;

/// <summary>
/// Métadonnées d'un épisode dans l'ordre demandé.
/// </summary>
public sealed record TvdbEpisode(
    int Season,
    int Number,
    string Name,
    string Overview,
    string Aired,
    string Image
)
{
    /// <summary>Note du public, distincte du score de popularité TVDB.</summary>
    public string Rating { get; init; } = "";

    /// <summary>Réalisateurs séparés par une barre verticale comme yaDIS2.</summary>
    public string Director { get; init; } = "";
}
