namespace YaDIS3.Core;

/// <summary>
/// Métadonnées de série et épisodes renvoyés par TVDB.
/// </summary>
public sealed record SeriesDetails(
    long Id,
    string Name,
    string Overview,
    string FirstAired,
    string Network,
    IReadOnlyList<TvdbEpisode> Episodes
);
