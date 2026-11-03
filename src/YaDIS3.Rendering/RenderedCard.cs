namespace YaDIS3.Rendering;

/// <summary>
/// Chemins des illustrations et des aperçus d'épisodes générés.
/// </summary>
public sealed record RenderedCard(
    string Directory,
    string Preview,
    IReadOnlyDictionary<string, string> EpisodePreviews
);
