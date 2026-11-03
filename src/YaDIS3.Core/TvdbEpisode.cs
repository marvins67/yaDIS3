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
);
