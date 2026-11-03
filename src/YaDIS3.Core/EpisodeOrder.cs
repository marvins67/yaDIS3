namespace YaDIS3.Core;

/// <summary>
/// Ordre d'épisodes proposé par TVDB.
/// </summary>
public sealed record EpisodeOrder(
    string Id,
    string Code,
    string Name
);
