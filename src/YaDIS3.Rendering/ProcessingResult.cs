namespace YaDIS3.Rendering;

/// <summary>Identifie les aperçus générés dans le cache du workspace.</summary>
public sealed record ProcessingResult(
    string PreviewPath,
    string? EpisodePreviewPath
);
