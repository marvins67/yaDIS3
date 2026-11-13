using System.Xml;
using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>
/// Relit les préférences du portage et les documents XML de
/// yaDIS2.
/// </summary>
public sealed class DuneConfigurationStore(
    IExportFileSystem? fileSystem = null,
    string? userConfigurationPath = null
)
{
    private readonly IExportFileSystem _files =
        fileSystem ?? new ExportFileSystem();
    private readonly UserConfigurationStore _user = new(
        userConfigurationPath
            ?? UserConfigurationLocation.Resolve(),
        fileSystem ?? new ExportFileSystem()
    );

    /// <summary>Emplacement du fichier user.config utilisé.</summary>
    public string UserConfigurationPath => _user.FilePath;

    /// <summary>Charge les préférences globales de yaDIS2.</summary>
    public DuneConfiguration LoadSettings() => _user.Load();

    /// <summary>
    /// Charge une copie sans créer ni modifier de fichier.
    /// </summary>
    public ConfigurationSnapshot Load(string workspace)
    {
        DuneConfiguration settings = _user.Load();
        return new(
            settings,
            ReadFolders(workspace),
            ReadXml(workspace, "groups.xml", "groups"),
            ReadShortcuts(workspace)
        );
    }

    private MediaSourceFolder[] ReadFolders(string workspace) =>
        ReadXml(workspace, "folders.xml", "folders")
            .Descendants("folder")
            .Select(node => new MediaSourceFolder(
                node.Element("path")?.Value ?? "",
                ParseInteger(node.Element("type")?.Value),
                node.Element("dunepath")?.Value ?? ""
            )
            {
                Original = new XElement(node),
            })
            .ToArray();

    private DuneShortcut[] ReadShortcuts(string workspace) =>
        ReadXml(workspace, "shortcuts.xml", "shortcuts")
            .Descendants("shortcut")
            .Select(node => new DuneShortcut(
                node.Value,
                ParseInteger(node.Attribute("type")?.Value),
                node.Attribute("path")?.Value ?? ""
            ))
            .ToArray();

    private static int ParseInteger(string? value) =>
        int.Parse(
            value ?? "0",
            System.Globalization.CultureInfo.InvariantCulture
        );

    private XDocument ReadXml(
        string workspace,
        string name,
        string root
    )
    {
        string path = Path.Combine(workspace, name);
        if (!_files.FileExists(path))
        {
            return LegacyWorkspaceXml.Create(
                root == "groups"
                    ? new XElement(root, new XElement("sagas", ""), new XElement("selections", ""))
                    : new XElement(root, ""));
        }
        using StringReader text = new(_files.ReadText(path));
        using XmlTextReader reader = new(text)
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            Normalization = false,
            WhitespaceHandling = WhitespaceHandling.Significant,
        };
        return XDocument.Load(
            reader,
            LoadOptions.PreserveWhitespace
        );
    }

    /// <summary>
    /// Refuse les valeurs invalides avant la première écriture.
    /// </summary>
    public static void Validate(ConfigurationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            snapshot.Settings.JpegQuality,
            1
        );
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            snapshot.Settings.JpegQuality,
            100
        );
        ArgumentOutOfRangeException.ThrowIfNegative(
            snapshot.Settings.LastDays
        );
        if (
            snapshot.Settings.Categories.Length is not (10 or 11)
            || snapshot.Settings.Categories.Any(value =>
                value is not ('0' or '1')
            )
        )
        {
            throw new InvalidDataException(
                "Les catégories Dune doivent être définies."
            );
        }
        foreach (MediaSourceFolder folder in snapshot.Folders)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                folder.Path
            );
            if (folder.Type is not (0 or 1))
            {
                throw new InvalidDataException(
                    "Le dossier doit contenir des films ou des séries."
                );
            }
        }
    }

    /// <summary>
    /// Enregistre ensemble réglages, dossiers, sélections et
    /// raccourcis.
    /// </summary>
    public void Save(
        string workspace,
        ConfigurationSnapshot snapshot
    )
    {
        Validate(snapshot);
        _files.CreateDirectory(workspace);
        Dictionary<string, byte[]> documents = Documents(
            workspace,
            snapshot
        );
        documents[_user.FilePath] = _user.Serialize(
            snapshot.Settings
        );
        WriteDocuments(documents);
    }

    /// <summary>Enregistre les groupes sans modifier les autres réglages.</summary>
    public void SaveGroups(string workspace, XDocument groups)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        ArgumentNullException.ThrowIfNull(groups);
        if (groups.Root?.Name != "groups")
        {
            throw new InvalidDataException("Racine groups.xml invalide.");
        }

        WriteDocuments(
            new()
            {
                [Path.Combine(workspace, "groups.xml")] =
                    LegacyWorkspaceXml.Serialize(groups),
            }
        );
    }

    /// <summary>
    /// Préserve les autres documents lors d'un traitement ou
    /// d'un export.
    /// </summary>
    public void SaveSettings(
        string workspace,
        DuneConfiguration settings
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        Validate(
            new(settings, [], new(new XElement("groups")), [])
        );
        WriteDocuments(
            new()
            {
                [_user.FilePath] = _user.Serialize(settings),
            }
        );
    }

    private void WriteDocuments(
        Dictionary<string, byte[]> documents
    )
    {
        Dictionary<string, byte[]?> originals =
            documents.Keys.ToDictionary(
                name => name,
                name =>
                    _files.FileExists(name)
                        ? _files.ReadBytes(name)
                        : null
            );
        List<string> written = [];
        try
        {
            foreach ((string name, byte[] content) in documents)
            {
                string temporary =
                    name
                    + ".tmp-"
                    + Guid.NewGuid().ToString("N");
                try
                {
                    _files.CreateDirectory(
                        Path.GetDirectoryName(name)!
                    );
                    _files.WriteBytes(temporary, content);
                    written.Add(name);
                    _files.MoveFile(
                        temporary,
                        name,
                        overwrite: true
                    );
                }
                finally
                {
                    if (_files.FileExists(temporary))
                    {
                        _files.DeleteFile(temporary);
                    }
                }
            }
        }
        catch (Exception failure)
            when (failure
                    is IOException
                        or UnauthorizedAccessException
            )
        {
            Rollback(originals, written, failure);
            throw;
        }
    }

    private void Rollback(
        Dictionary<string, byte[]?> originals,
        List<string> written,
        Exception failure
    )
    {
        List<Exception> failures = [failure];
        foreach (string name in written.AsEnumerable().Reverse())
        {
            try
            {
                string path = name;
                if (originals[name] is { } bytes)
                {
                    _files.WriteBytes(path, bytes);
                }
                else
                {
                    _files.DeleteFile(path);
                }
            }
            catch (Exception restore)
                when (restore
                        is IOException
                            or UnauthorizedAccessException
                )
            {
                failures.Add(restore);
            }
        }
        if (failures.Count > 1)
        {
            throw new AggregateException(failures);
        }
    }

    private static Dictionary<string, byte[]> Documents(
        string workspace,
        ConfigurationSnapshot snapshot
    ) =>
        new()
        {
            [Path.Combine(workspace, "folders.xml")] = LegacyWorkspaceXml.Serialize(
                LegacyWorkspaceXml.Create(
                    new XElement(
                        "folders", "",
                        snapshot.Folders.Select(FolderElement)
                    )
                )
            ),
            [Path.Combine(workspace, "groups.xml")] = LegacyWorkspaceXml.Serialize(
                snapshot.Groups
            ),
            [Path.Combine(workspace, "shortcuts.xml")] =
                LegacyWorkspaceXml.Serialize(
                    LegacyWorkspaceXml.Create(
                        new XElement(
                            "shortcuts", "",
                            snapshot.Shortcuts.Select(
                                shortcut => new XElement(
                                    "shortcut",
                                    new XAttribute(
                                        "type",
                                        shortcut.Type
                                    ),
                                    shortcut.Type == 2
                                        ? new XAttribute(
                                            "path",
                                            shortcut.Path
                                        )
                                        : null,
                                    shortcut.Name
                                )
                            )
                        )
                    )
                ),
        };

    private static XElement FolderElement(
        MediaSourceFolder folder
    )
    {
        XElement node = folder.Original is null
            ? new("folder")
            : new(folder.Original);
        node.SetElementValue("path", folder.Path);
        node.SetElementValue("type", folder.Type);
        node.SetElementValue("dunepath", folder.DunePath);
        return node;
    }
}
