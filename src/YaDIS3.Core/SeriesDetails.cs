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
)
{
    /// <summary>Note du public ; vide si le fournisseur ne la propose pas.</summary>
    public string Rating { get; init; } = "";

    /// <summary>Genres dans l'ordre du fournisseur.</summary>
    public IReadOnlyList<string> Genres { get; init; } = [];

    /// <summary>Interprètes et invités, sans doublons.</summary>
    public IReadOnlyList<string> Actors { get; init; } = [];

    /// <summary>Illustrations préférées pour la langue demandée.</summary>
    public IReadOnlyDictionary<string, string> Artwork
    {
        get;
        init;
    } = new Dictionary<string, string>(StringComparer.Ordinal);
}
