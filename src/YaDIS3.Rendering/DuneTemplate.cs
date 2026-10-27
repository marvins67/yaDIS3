using System.Globalization;
using System.Xml;
using System.Xml.Linq;
namespace YaDIS3.Rendering;

public sealed class DuneTemplate
{
    public string Directory { get; }
    public XElement Root { get; }
    public string Name => (string?)Root.Attribute("name") ?? Path.GetFileName(Directory);
    private readonly Dictionary<string, string> assets;
    private readonly Dictionary<string, string> common;
    private DuneTemplate(string directory, XElement root, string? commonImages)
    {
        Directory = directory; Root = root;
        assets = Inventory(directory); common = commonImages is not null && System.IO.Directory.Exists(commonImages) ? Inventory(commonImages) : [];
        var supported = new HashSet<string>(["static", "fanart", "poster", "banner", "thumbnail", "special", "rating", "vcodec", "acodec", "format", "certification", "country", "countries", "channels", "resolution", "aspect", "framerate", "frequency", "title", "originaltitle", "genres", "year", "runtime", "director", "studios", "tagline", "saga", "cast", "plot", "network", "season", "aired"], StringComparer.Ordinal);
        foreach (var node in Root.Descendants().Where(n => n.Name == "text" || n.Name == "image"))
            if (node.Attribute("type") is { } type && !supported.Contains(type.Value.ToLowerInvariant())) throw new InvalidDataException($"Type de template non pris en charge : {type.Value}.");
    }
    private static Dictionary<string, string> Inventory(string root) => System.IO.Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint })
        .ToDictionary(p => Path.GetRelativePath(root, p).Replace('\\', '/'), StringComparer.OrdinalIgnoreCase);
    public static DuneTemplate Load(string directory, string? commonImages = null)
    {
        directory = Path.GetFullPath(directory);
        using var reader = XmlReader.Create(Path.Combine(directory, "template.xml"), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 });
        var root = XDocument.Load(reader).Root;
        if (root?.Name != "template" || root.Element("movie") is null || root.Element("tv") is null) throw new InvalidDataException("Template yaDIS3 incomplet.");
        return new DuneTemplate(directory, root, commonImages);
    }
    public string? Asset(string name, bool required = false)
    {
        name = name.Trim().Replace('\\', '/');
        if (name.StartsWith('/') || name.Contains(':') || name.Split('/').Any(s => s is ".." or ".")) throw new InvalidDataException("Chemin de ressource hors du template.");
        if (assets.TryGetValue(name, out var file) || common.TryGetValue(name, out file)) return file;
        if (required) throw new FileNotFoundException($"Ressource du template absente : {name}.");
        return null;
    }
    public IEnumerable<string> Fonts => assets.Values.Where(p => Path.GetExtension(p).Equals(".ttf", StringComparison.OrdinalIgnoreCase));
    public XElement Node(string path) => Root.XPath(path) ?? throw new InvalidDataException($"Élément de template absent : {path}.");
    public static IReadOnlyList<DuneTemplate> Bundled(string? baseDirectory = null)
    {
        var root = baseDirectory ?? AppContext.BaseDirectory; var folder = Path.Combine(root, "Assets", "Templates");
        if (!System.IO.Directory.Exists(folder)) return [];
        var common = Path.Combine(root, "Assets", "Images");
        return System.IO.Directory.EnumerateDirectories(folder).Select(p => Load(p, common)).OrderBy(t => t.Name == "Origins" ? 0 : 1).ThenBy(t => t.Name, StringComparer.Ordinal).ToArray();
    }
    public override string ToString() => Name;
}
internal static class XmlFields
{
    public static XElement? XPath(this XElement node, string path)
    { XElement? current = node; foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries)) current = current?.Element(part); return current; }
    public static string ValueAt(this XElement node, string path) => node.XPath(path)?.Value ?? "";
    public static string Attr(this XElement? node, string name, string fallback = "") => (string?)node?.Attribute(name) ?? fallback;
    public static int Int(this XElement? node, string name, int fallback = 0) => int.TryParse(node.Attr(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    public static float Float(this XElement? node, string name, float fallback = 0) => float.TryParse(node.Attr(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && float.IsFinite(n) ? n : fallback;
}
