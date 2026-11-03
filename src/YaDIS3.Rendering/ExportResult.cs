namespace YaDIS3.Rendering;

/// <summary>
/// Décrit le catalogue exporté et les avertissements non
/// bloquants.
/// </summary>
public sealed record ExportResult(
    string Directory,
    int MediaCount,
    int EpisodeCount,
    IReadOnlyList<string> Warnings
);
