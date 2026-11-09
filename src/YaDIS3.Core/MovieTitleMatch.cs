namespace YaDIS3.Core;

/// <summary>Résultat d'identification TMDB sélectionnable.</summary>
public sealed class MovieTitleMatch(
    long id,
    string title,
    string releaseDate,
    string originalTitle = "",
    string posterPath = ""
)
{
    /// <summary>Identifiant du film dans TMDB.</summary>
    public long Id { get; } = id;

    /// <summary>Titre du résultat.</summary>
    public string Title { get; } = title;

    /// <summary>Date de première sortie.</summary>
    public string ReleaseDate { get; } = releaseDate;

    /// <summary>Titre original renvoyé par TMDB.</summary>
    public string OriginalTitle { get; } = originalTitle;

    /// <summary>Chemin de l'affiche renvoyée par TMDB.</summary>
    public string PosterPath { get; } = posterPath;

    /// <inheritdoc />
    public override string ToString() =>
        string.IsNullOrWhiteSpace(ReleaseDate)
            ? Title
            : Title + " (" + ReleaseDate[..Math.Min(4, ReleaseDate.Length)] + ")";
}
