using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>
/// Lit et préserve la section de préférences utilisateur yaDIS2.
/// Le fichier est extérieur au workspace.
/// </summary>
public sealed class UserConfigurationStore(
    string filePath,
    IExportFileSystem files
)
{
    /// <summary>Nom de section historique des préférences.</summary>
    public const string SectionName = "yaDIS.My.MySettings";

    /// <summary>Emplacement du fichier utilisateur injecté.</summary>
    public string FilePath { get; } = Path.GetFullPath(filePath);

    internal XDocument ReadDocument()
    {
        if (!files.FileExists(FilePath))
        {
            return new(new XElement("configuration"));
        }
        using StringReader input = new(files.ReadText(FilePath));
        using XmlReader reader = XmlReader.Create(
            input,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            }
        );
        XDocument document = XDocument.Load(
            reader,
            LoadOptions.PreserveWhitespace
        );
        if (document.Root?.Name != "configuration")
        {
            throw new InvalidDataException(
                "Racine user.config invalide : " + FilePath
            );
        }
        return document;
    }

    /// <summary>Charge les clés historiques sans créer de fichier.</summary>
    public DuneConfiguration Load()
    {
        XElement? section = ReadDocument()
            .Root!.Element("userSettings")
            ?.Element(SectionName);
        return ReadSettings(section);
    }

    private static string? Value(
        XElement? section,
        string name
    ) =>
        section
            ?.Elements("setting")
            .FirstOrDefault(node =>
                (string?)node.Attribute("name") == name
            )
            ?.Element("value")
            ?.Value;

    private static DuneConfiguration ReadSettings(
        XElement? section
    )
    {
        DuneConfiguration defaults = new();
        return defaults with
        {
            Workspace = Value(section, "Workspace") ?? "",
            Destination =
                Value(section, "ExportPath")
                ?? defaults.Destination,
            Template =
                Value(section, "Template") ?? defaults.Template,
            Language = Value(section, "Language") ?? "",
            Aai = Boolean(section, "UseAAI", defaults.Aai),
            Cases = Boolean(section, "UseCase", defaults.Cases),
            BasicMode = Boolean(
                section,
                "BasicMode",
                defaults.BasicMode
            ),
            JpegQuality = Integer(
                section,
                "JPGQuality",
                defaults.JpegQuality
            ),
            Categories =
                Value(section, "Categories")
                ?? defaults.Categories,
            LastDays = Integer(
                section,
                "LastDays",
                defaults.LastDays
            ),
            SortRecentByDate =
                Integer(section, "RecentSorting", 0) == 1,
            SelectionBox = Boolean(
                section,
                "SelectionBox",
                defaults.SelectionBox
            ),
            Scrollbar = Boolean(
                section,
                "Scrollbar",
                defaults.Scrollbar
            ),
            IgnoreArticles = Boolean(
                section,
                "IgnoreArticles",
                defaults.IgnoreArticles
            ),
            Articles =
                Value(section, "Articles") ?? defaults.Articles,
            AutoSelect = Boolean(
                section,
                "AutoSelect",
                defaults.AutoSelect
            ),
            AutoSelectName =
                Value(section, "AutoSelectName") ?? "",
        };
    }

    private static bool Boolean(
        XElement? section,
        string key,
        bool fallback
    ) =>
        Value(section, key) is { } value
            ? bool.Parse(value)
            : fallback;

    private static int Integer(
        XElement? section,
        string key,
        int fallback
    ) =>
        Value(section, key) is { } value
            ? int.Parse(value, CultureInfo.InvariantCulture)
            : fallback;

    internal byte[] Serialize(DuneConfiguration settings)
    {
        XDocument document = ReadDocument();
        XElement section = EnsureSection(document.Root!);
        WriteSettings(section, settings);
        using MemoryStream stream = new();
        document.Save(stream);
        return stream.ToArray();
    }

    private static void WriteSettings(
        XElement section,
        DuneConfiguration settings
    )
    {
        Put(section, "Workspace", settings.Workspace);
        Put(section, "ExportPath", settings.Destination);
        Put(section, "Template", settings.Template);
        Put(section, "Language", settings.Language);
        Put(section, "UseAAI", settings.Aai);
        Put(section, "UseCase", settings.Cases);
        Put(section, "BasicMode", settings.BasicMode);
        Put(section, "JPGQuality", settings.JpegQuality);
        Put(section, "Categories", settings.Categories);
        Put(section, "LastDays", settings.LastDays);
        Put(
            section,
            "RecentSorting",
            settings.SortRecentByDate ? 1 : 0
        );
        Put(section, "SelectionBox", settings.SelectionBox);
        Put(section, "Scrollbar", settings.Scrollbar);
        Put(section, "IgnoreArticles", settings.IgnoreArticles);
        Put(section, "Articles", settings.Articles);
        Put(section, "AutoSelect", settings.AutoSelect);
        Put(section, "AutoSelectName", settings.AutoSelectName);
    }

    private static void Put(
        XElement section,
        string key,
        object value
    )
    {
        XElement? setting = section
            .Elements("setting")
            .FirstOrDefault(node =>
                (string?)node.Attribute("name") == key
            );
        if (setting is null)
        {
            setting = new(
                "setting",
                new XAttribute("name", key),
                new XAttribute("serializeAs", "String")
            );
            section.Add(setting);
        }
        setting.SetElementValue(
            "value",
            Convert.ToString(value, CultureInfo.InvariantCulture)
        );
    }

    private static XElement EnsureSection(XElement root)
    {
        XElement? declarations = root.Element("configSections");
        if (declarations is null)
        {
            declarations = new("configSections");
            root.AddFirst(declarations);
        }
        XElement? group = declarations
            .Elements("sectionGroup")
            .FirstOrDefault(node =>
                (string?)node.Attribute("name") == "userSettings"
            );
        if (group is null)
        {
            group = new(
                "sectionGroup",
                new XAttribute("name", "userSettings"),
                new XAttribute(
                    "type",
                    "System.Configuration.UserSettingsGroup, System"
                )
            );
            declarations.Add(group);
        }
        if (
            !group
                .Elements("section")
                .Any(node =>
                    (string?)node.Attribute("name")
                    == SectionName
                )
        )
        {
            group.Add(
                new XElement(
                    "section",
                    new XAttribute("name", SectionName),
                    new XAttribute(
                        "type",
                        "System.Configuration.ClientSettingsSection, System"
                    ),
                    new XAttribute(
                        "allowExeDefinition",
                        "MachineToLocalUser"
                    ),
                    new XAttribute("requirePermission", "false")
                )
            );
        }
        XElement? user = root.Element("userSettings");
        if (user is null)
        {
            user = new("userSettings");
            root.Add(user);
        }
        XElement? section = user.Element(SectionName);
        if (section is null)
        {
            section = new(SectionName);
            user.Add(section);
        }
        return section;
    }
}
