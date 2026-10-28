using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace YaDIS3.Rendering;

/// <summary>
/// Loads a template and resolves its bundled image and font
/// assets.
/// </summary>
public sealed class DuneTemplate
{
    /// <summary>
    /// Gets the directory that contains the loaded template.
    /// </summary>
    public string Directory { get; }

    /// <summary>
    /// Gets the XML root for the loaded data.
    /// </summary>
    public XElement Root { get; }

    /// <summary>
    /// Gets the template's display name.
    /// </summary>
    public string Name =>
        (string?)Root.Attribute("name")
        ?? Path.GetFileName(Directory);

    private readonly Dictionary<string, string> _assets;

    private readonly Dictionary<string, string> _common;

    private DuneTemplate(
        string directory,
        XElement root,
        string? commonImages
    )
    {
        Directory = directory;
        Root = root;
        _assets = Inventory(directory);
        _common =
            commonImages is not null
            && System.IO.Directory.Exists(commonImages)
                ? Inventory(commonImages)
                : [];
        var supported = new HashSet<string>(
            [
                "static",
                "fanart",
                "poster",
                "banner",
                "thumbnail",
                "special",
                "rating",
                "vcodec",
                "acodec",
                "format",
                "certification",
                "country",
                "countries",
                "channels",
                "resolution",
                "aspect",
                "framerate",
                "frequency",
                "title",
                "originaltitle",
                "genres",
                "year",
                "runtime",
                "director",
                "studios",
                "tagline",
                "saga",
                "cast",
                "plot",
                "network",
                "season",
                "aired",
            ],
            StringComparer.Ordinal
        );
        foreach (
            XElement node in Root.Descendants()
                .Where(nodeValue =>
                    nodeValue.Name == "text"
                    || nodeValue.Name == "image"
                )
        )
        {
            if (
                node.Attribute("type") is { } type
                && !supported.Contains(
                    type.Value.ToLowerInvariant()
                )
            )
            {
                throw new InvalidDataException(
                    $"Type de template non pris en charge : {type.Value}."
                );
            }
        }
    }

    private static Dictionary<string, string> Inventory(
        string root
    )
    {
        return System
            .IO.Directory.EnumerateFiles(
                root,
                "*",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    AttributesToSkip =
                        FileAttributes.ReparsePoint,
                }
            )
            .ToDictionary(
                pathValue =>
                    Path.GetRelativePath(root, pathValue)
                        .Replace('\\', '/'),
                StringComparer.OrdinalIgnoreCase
            );
    }

    /// <summary>
    /// Loads and validates a template with optional shared
    /// image resources.
    /// </summary>
    public static DuneTemplate Load(
        string directory,
        string? commonImages = null
    )
    {
        directory = Path.GetFullPath(directory);
        using XmlReader reader = XmlReader.Create(
            Path.Combine(directory, "template.xml"),
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 4 * 1024 * 1024,
            }
        );
        XElement? root = XDocument.Load(reader).Root;
        if (
            root?.Name != "template"
            || root.Element("movie") is null
            || root.Element("tv") is null
        )
        {
            throw new InvalidDataException(
                "Template yaDIS3 incomplet."
            );
        }

        return new DuneTemplate(directory, root, commonImages);
    }

    /// <summary>
    /// Resolves a template resource while rejecting paths
    /// outside the template.
    /// </summary>
    public string? Asset(string name, bool required = false)
    {
        name = name.Trim().Replace('\\', '/');
        if (
            name.StartsWith('/')
            || name.Contains(':')
            || name.Split('/')
                .Any(segment => segment is ".." or ".")
        )
        {
            throw new InvalidDataException(
                "Chemin de ressource hors du template."
            );
        }

        if (
            _assets.TryGetValue(name, out var file)
            || _common.TryGetValue(name, out file)
        )
        {
            return file;
        }

        if (required)
        {
            throw new FileNotFoundException(
                $"Ressource du template absente : {name}."
            );
        }

        return null;
    }

    /// <summary>
    /// Gets the font files available in the template.
    /// </summary>
    public IEnumerable<string> Fonts =>
        _assets.Values.Where(pathValue =>
            Path.GetExtension(pathValue)
                .Equals(
                    ".ttf",
                    StringComparison.OrdinalIgnoreCase
                )
        );

    /// <summary>
    /// Gets a required template element or reports an
    /// incomplete template.
    /// </summary>
    public XElement Node(string path)
    {
        return Root.XPath(path)
            ?? throw new InvalidDataException(
                $"Élément de template absent : {path}."
            );
    }

    /// <summary>
    /// Loads the templates distributed with the application.
    /// </summary>
    public static IReadOnlyList<DuneTemplate> Bundled(
        string? baseDirectory = null
    )
    {
        string root = baseDirectory ?? AppContext.BaseDirectory;
        string folder = Path.Combine(
            root,
            "Assets",
            "Templates"
        );
        if (!System.IO.Directory.Exists(folder))
        {
            return [];
        }

        string common = Path.Combine(root, "Assets", "Images");
        return System
            .IO.Directory.EnumerateDirectories(folder)
            .Select(pathValue => Load(pathValue, common))
            .OrderBy(template =>
                template.Name == "Origins" ? 0 : 1
            )
            .ThenBy(
                template => template.Name,
                StringComparer.Ordinal
            )
            .ToArray();
    }

    /// <summary>
    /// Returns the template's display name.
    /// </summary>
    public override string ToString()
    {
        return Name;
    }
}

internal static class XmlFields
{
    /// <summary>
    /// Finds an XML element using a slash-separated relative
    /// path.
    /// </summary>
    public static XElement? XPath(
        this XElement node,
        string path
    )
    {
        XElement? current = node;
        foreach (
            string part in path.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries
            )
        )
        {
            current = current?.Element(part);
        }

        return current;
    }

    /// <summary>
    /// Reads an XML element's text, or returns an empty string.
    /// </summary>
    public static string ValueAt(this XElement node, string path)
    {
        return node.XPath(path)?.Value ?? "";
    }

    /// <summary>
    /// Reads an XML attribute with an optional fallback value.
    /// </summary>
    public static string Attr(
        this XElement? node,
        string name,
        string fallback = ""
    )
    {
        return (string?)node?.Attribute(name) ?? fallback;
    }

    /// <summary>
    /// Reads an integer XML attribute with an optional fallback
    /// value.
    /// </summary>
    public static int Int(
        this XElement? node,
        string name,
        int fallback = 0
    )
    {
        return int.TryParse(
            node.Attr(name),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var number
        )
            ? number
            : fallback;
    }

    /// <summary>
    /// Reads a finite numeric XML attribute with an optional
    /// fallback value.
    /// </summary>
    public static float Float(
        this XElement? node,
        string name,
        float fallback = 0
    )
    {
        return
            float.TryParse(
                node.Attr(name),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number
            ) && float.IsFinite(number)
            ? number
            : fallback;
    }
}
