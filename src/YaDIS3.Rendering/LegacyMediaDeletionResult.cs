namespace YaDIS3.Rendering;

/// <summary>Conséquences de la suppression des fiches.</summary>
public sealed record LegacyMediaDeletionResult(
    IReadOnlyList<string> DeletedDataPaths,
    IReadOnlyList<string> Errors
);
