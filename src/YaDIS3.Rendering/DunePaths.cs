using System.Text.RegularExpressions;

namespace YaDIS3.Rendering;

/// <summary>
/// Maps a local media root to a Dune playback address.
/// </summary>
public sealed record PathMapping(string Source, string Dune);

/// <summary>
/// Validates paths and resolves local files to Dune
/// addresses.
/// </summary>
public static class DunePaths
{
    private static readonly Regex s_scheme = new(
        "^(smb|nfs(?:-tcp|-udp)?|storage_name|storage_label|storage_uuid|main_storage|http|https)://",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    /// <summary>
    /// Rejects control characters that could corrupt Dune
    /// configuration.
    /// </summary>
    public static string CleanValue(string value)
    {
        if (value.Any(char.IsControl))
        {
            throw new InvalidDataException(
                "Un chemin Dune contient un caractère de contrôle."
            );
        }

        return value;
    }

    /// <summary>
    /// Validates a Dune address and normalizes its trailing
    /// separator.
    /// </summary>
    public static string ValidateDune(string value)
    {
        CleanValue(value);
        Match scheme = s_scheme.Match(value);
        if (
            (!scheme.Success && !value.StartsWith('/'))
            || scheme.Success
                && (
                    value.Length == scheme.Length
                    || value[scheme.Length] == '/'
                )
        )
        {
            throw new InvalidDataException(
                "Indiquez une adresse Dune SMB, NFS, storage_label, storage_uuid ou un chemin absolu Dune."
            );
        }

        return value == "/" ? value : value.TrimEnd('/');
    }

    private static string Local(string value)
    {
        value = CleanValue(value).Replace('\\', '/');
        if (value != "/")
        {
            value = value.TrimEnd('/');
        }

        if (
            value
                .Split('/')
                .Any(segment => segment is ".." or ".")
        )
        {
            throw new InvalidDataException(
                "Les chemins vidéo doivent être normalisés, sans . ou .."
            );
        }

        return value;
    }

    /// <summary>
    /// Indicates whether the path uses a Windows drive or UNC
    /// root.
    /// </summary>
    public static bool Windows(string value)
    {
        return Regex.IsMatch(value, "^[a-zA-Z]:")
            || value.StartsWith("//", StringComparison.Ordinal);
    }

    /// <summary>
    /// Joins a local directory and filename across supported
    /// path styles.
    /// </summary>
    public static string JoinLocal(string directory, string name)
    {
        return directory.TrimEnd('/', '\\')
            + "/"
            + name.TrimStart('/', '\\');
    }

    /// <summary>
    /// Maps a local video path to a validated Dune playback
    /// address.
    /// </summary>
    public static string Resolve(
        string file,
        IReadOnlyList<PathMapping> mappings,
        string legacySource = "",
        string legacyDune = ""
    )
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            throw new InvalidDataException(
                "Un média n'a pas de chemin vidéo."
            );
        }

        if (s_scheme.IsMatch(file))
        {
            return ValidateDune(file);
        }

        string normalized = Local(file);
        IReadOnlyList<PathMapping> candidates = mappings;
        if (
            !string.IsNullOrWhiteSpace(legacySource)
            && !string.IsNullOrWhiteSpace(legacyDune)
        )
        {
            candidates =
            [
                .. mappings,
                new PathMapping(legacySource, legacyDune),
            ];
        }

        foreach (
            PathMapping mapping in mappings
                .OrderByDescending(media =>
                    Local(media.Source).Length
                )
                .Concat(candidates.Skip(mappings.Count))
        )
        {
            string source = Local(mapping.Source);
            if (source.Length == 0)
            {
                continue;
            }

            StringComparison comparison = Windows(source)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (
                normalized.Equals(source, comparison)
                || normalized.StartsWith(
                    source == "/" ? source : source + "/",
                    comparison
                )
            )
            {
                return ValidateDune(mapping.Dune).TrimEnd('/')
                    + (
                        source == "/"
                            ? normalized
                            : normalized[source.Length..]
                    );
            }
        }

        throw new InvalidDataException(
            "Un chemin vidéo n'a pas de correspondance Dune. Complétez les racines de chemins dans l'onglet Export Dune."
        );
    }

    /// <summary>
    /// Parses one local-to-Dune root mapping per input line.
    /// </summary>
    public static IReadOnlyList<PathMapping> Parse(string text)
    {
        var result = new List<PathMapping>();
        foreach (
            string line in text.Replace("\r", "")
                .Split(
                    '\n',
                    StringSplitOptions.RemoveEmptyEntries
                        | StringSplitOptions.TrimEntries
                )
        )
        {
            string[] split = line.Split(
                "=>",
                2,
                StringSplitOptions.TrimEntries
            );
            if (split.Length != 2 || split[0].Length == 0)
            {
                throw new InvalidDataException(
                    "Écrivez une correspondance par ligne : racine locale => adresse Dune."
                );
            }

            _ = Local(split[0]);
            _ = ValidateDune(split[1]);
            result.Add(new(split[0], split[1]));
        }

        return result;
    }
}
