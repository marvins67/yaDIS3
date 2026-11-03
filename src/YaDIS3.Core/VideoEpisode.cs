namespace YaDIS3.Core;

/// <summary>
/// Épisode détecté à partir du nom d'un fichier vidéo local.
/// </summary>
public sealed record VideoEpisode(
    int Season,
    int Number,
    string File
);
