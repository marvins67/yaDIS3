namespace YaDIS3.Rendering;

/// <summary>
/// Langue, format des fonds et boîtiers utilisés pour le rendu.
/// </summary>
public sealed record RenderOptions(
    string Language = "fr",
    bool AaiBackgrounds = false,
    bool UseCases = false,
    int JpegQuality = 95,
    bool BasicMode = false
);
