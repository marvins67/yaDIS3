namespace YaDIS3.Core;

/// <summary>
/// Associe une fiche à un index sans dépendance graphique.
/// </summary>
public sealed record LegacyIndexEntry(
    string Directory,
    string Caption
);
