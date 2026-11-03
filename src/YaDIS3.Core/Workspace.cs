namespace YaDIS3.Core;

/// <summary>
/// Fiches et avertissements chargés depuis un workspace.
/// </summary>
public sealed record Workspace(
    string Directory,
    IReadOnlyList<MediaCard> Cards,
    IReadOnlyList<string> Warnings
);
