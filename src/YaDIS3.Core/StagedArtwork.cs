namespace YaDIS3.Core;

/// <summary>
/// Remplacement d'une illustration et empreinte du fichier
/// d'origine.
/// </summary>
public sealed record StagedArtwork(
    string Target,
    string Temporary,
    string OriginalHash
);
