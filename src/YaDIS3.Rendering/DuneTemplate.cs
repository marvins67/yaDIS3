using System.Xml;
using System.Xml.Linq;

namespace YaDIS3.Rendering;

/// <summary>
/// Charge un template et résout ses images et ses polices.
/// </summary>
public sealed class DuneTemplate
{
    /// <summary>
    /// Dossier contenant le template chargé.
    /// </summary>
    public string Directory { get; }

    /// <summary>
    /// Racine XML des données chargées.
    /// </summary>
    public XElement Root { get; }

    /// <summary>
    /// Nom affiché du template.
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
    /// Charge et valide un template avec ses éventuelles images
    /// partagées.
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
    /// Résout une ressource en refusant les chemins sortant du
    /// template.
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
    /// Fichiers de polices disponibles dans le template.
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
    /// Obtient un élément obligatoire ou signale un template
    /// incomplet.
    /// </summary>
    public XElement Node(string path)
    {
        return Root.XPath(path)
            ?? throw new InvalidDataException(
                $"Élément de template absent : {path}."
            );
    }

    /// <summary>
    /// Charge les templates distribués avec l'application.
    /// </summary>
    public static IReadOnlyList<DuneTemplate> Bundled(
        string? baseDirectory = null
    )
    {
        string root = baseDirectory ?? AppContext.BaseDirectory;
        string folder = Path.Combine(root, "templates");
        if (!System.IO.Directory.Exists(folder))
        {
            return [];
        }

        string common = Path.Combine(
            root,
            "Assets",
            "Images"
        );
        return System
            .IO.Directory.EnumerateDirectories(folder)
            .Select(pathValue => Load(pathValue, common))
            .ToArray();
    }

    /// <summary>
    /// Renvoie le nom affiché du template.
    /// </summary>
    public override string ToString()
    {
        return Name;
    }
}
