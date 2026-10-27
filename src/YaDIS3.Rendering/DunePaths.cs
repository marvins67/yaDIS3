using System.Text.RegularExpressions;
namespace YaDIS3.Rendering;

public sealed record PathMapping(string Source, string Dune);
public static class DunePaths
{
    private static readonly Regex Scheme = new("^(smb|nfs(?:-tcp|-udp)?|storage_name|storage_label|storage_uuid|main_storage|http|https)://", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public static string CleanValue(string value)
    {
        if (value.Any(char.IsControl)) throw new InvalidDataException("Un chemin Dune contient un caractère de contrôle.");
        return value;
    }
    public static string ValidateDune(string value)
    {
        CleanValue(value);
        var scheme = Scheme.Match(value);
        if ((!scheme.Success && !value.StartsWith('/')) || scheme.Success && (value.Length == scheme.Length || value[scheme.Length] == '/'))
            throw new InvalidDataException("Indiquez une adresse Dune SMB, NFS, storage_label, storage_uuid ou un chemin absolu Dune.");
        return value == "/" ? value : value.TrimEnd('/');
    }
    private static string Local(string value)
    {
        value = CleanValue(value).Replace('\\', '/');
        if (value != "/") value = value.TrimEnd('/');
        if (value.Split('/').Any(s => s is ".." or ".")) throw new InvalidDataException("Les chemins vidéo doivent être normalisés, sans . ou ..");
        return value;
    }
    public static bool Windows(string value) => Regex.IsMatch(value, "^[a-zA-Z]:") || value.StartsWith("//", StringComparison.Ordinal);
    public static string JoinLocal(string directory, string name) => directory.TrimEnd('/', '\\') + "/" + name.TrimStart('/', '\\');
    public static string Resolve(string file, IReadOnlyList<PathMapping> mappings, string legacySource = "", string legacyDune = "")
    {
        if (string.IsNullOrWhiteSpace(file)) throw new InvalidDataException("Un média n'a pas de chemin vidéo.");
        if (Scheme.IsMatch(file)) return ValidateDune(file);
        var normalized = Local(file);
        var candidates = mappings;
        if (!string.IsNullOrWhiteSpace(legacySource) && !string.IsNullOrWhiteSpace(legacyDune)) candidates = [.. mappings, new PathMapping(legacySource, legacyDune)];
        foreach (var mapping in mappings.OrderByDescending(m => Local(m.Source).Length).Concat(candidates.Skip(mappings.Count)))
        {
            var source = Local(mapping.Source); if (source.Length == 0) continue;
            var comparison = Windows(source) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (normalized.Equals(source, comparison) || normalized.StartsWith(source == "/" ? source : source + "/", comparison))
                return ValidateDune(mapping.Dune).TrimEnd('/') + (source == "/" ? normalized : normalized[source.Length..]);
        }
        throw new InvalidDataException("Un chemin vidéo n'a pas de correspondance Dune. Complétez les racines de chemins dans l'onglet Export Dune.");
    }
    public static IReadOnlyList<PathMapping> Parse(string text)
    {
        var result = new List<PathMapping>();
        foreach (var line in text.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = line.Split("=>", 2, StringSplitOptions.TrimEntries);
            if (split.Length != 2 || split[0].Length == 0) throw new InvalidDataException("Écrivez une correspondance par ligne : racine locale => adresse Dune.");
            _ = Local(split[0]); _ = ValidateDune(split[1]); result.Add(new(split[0], split[1]));
        }
        return result;
    }
}
