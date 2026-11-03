namespace YaDIS3.Core;

/// <summary>Raccourci vers une sélection (0), un genre (1) ou un dossier (2).</summary>
public sealed record DuneShortcut(
    string Name,
    int Type,
    string Path = ""
);
