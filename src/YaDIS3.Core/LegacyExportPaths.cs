namespace YaDIS3.Core;

/// <summary>
/// Conserve les noms de dossiers utilisés par yaDIS2.
/// </summary>
public static class LegacyExportPaths
{
    /// <summary>
    /// Conserve le nom original, y compris l'extension vidéo.
    /// </summary>
    public static string Media(string filename, bool series)
    {
        ValidateName(filename);
        return (series ? "01/" : "00/99/") + filename;
    }

    /// <summary>Retourne l'unique dossier de traitement et d'export d'une fiche.</summary>
    public static string WorkspaceMedia(string workspace, string filename, bool series)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        return Path.Combine(workspace, "interface", Media(filename, series).Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// La clé de navigation peut différer de la saison TVDB.
    /// </summary>
    public static string Season(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ValidateName(key);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            key.Length,
            2
        );
        return key[..2];
    }

    /// <summary>
    /// Reprend le premier caractère sans retirer les accents.
    /// </summary>
    public static string Alphabet(string sortTitle)
    {
        ArgumentException.ThrowIfNullOrEmpty(sortTitle);
        string first = sortTitle[..1];
        return string.CompareOrdinal(first, "9") <= 0
            ? "0-9"
            : first;
    }

    /// <summary>
    /// Empêche un nom de fiche de sortir du dossier d'export.
    /// </summary>
    public static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (
            name is "." or ".."
            || name.IndexOfAny(['/', '\\']) >= 0
            || name.Any(char.IsControl)
        )
        {
            throw new InvalidDataException(
                $"Nom de dossier d'export invalide : {name}."
            );
        }
    }
}
