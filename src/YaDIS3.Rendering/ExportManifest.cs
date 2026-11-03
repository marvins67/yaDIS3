namespace YaDIS3.Rendering;

/// <summary>Identifie les exports appartenant à l'application avant leur remplacement.</summary>
internal sealed record ExportManifest(
    string Producer,
    int Version,
    string Template,
    int MediaCount,
    int EpisodeCount
);
