namespace YaDIS3.Core;

/// <summary>
/// Série renvoyée par une recherche TVDB.
/// </summary>
public sealed record SeriesMatch(
    long Id,
    string Name,
    string Overview,
    string Year
)
{
    /// <summary>
    /// Libellé affiché dans la liste de sélection.
    /// </summary>
    public string Label =>
        string.IsNullOrEmpty(Year) ? Name : $"{Name} ({Year})";
}
