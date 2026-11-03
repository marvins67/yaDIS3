namespace YaDIS3.Core;

/// <summary>
/// Identifie l'emplacement de l'illustration à modifier sur une
/// fiche.
/// </summary>
public enum ArtworkKind
{
    /// <summary>
    /// Affiche de film ou de série.
    /// </summary>
    Poster,

    /// <summary>
    /// Bannière de série.
    /// </summary>
    Banner,

    /// <summary>
    /// Illustration de fond.
    /// </summary>
    Fanart,

    /// <summary>
    /// Miniature d'épisode.
    /// </summary>
    Episode,
}
