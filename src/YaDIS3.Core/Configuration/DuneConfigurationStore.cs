using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>Relit les préférences du portage et les documents XML de yaDIS2.</summary>
public sealed class DuneConfigurationStore(
    IExportFileSystem? fileSystem = null
)
{
    /// <summary>Nom conservé pour les workspaces déjà utilisés dans yaDIS3.</summary>
    public const string FileName = "yadis3-modern-export.json";
    private readonly IExportFileSystem _files =
        fileSystem ?? new ExportFileSystem();
    private static readonly JsonSerializerOptions s_json = new()
    {
        WriteIndented = true,
    };

    /// <summary>Charge une copie sans créer ni modifier de fichier.</summary>
    public ConfigurationSnapshot Load(string workspace)
    {
        string path = Path.Combine(workspace, FileName);
        DuneConfiguration settings = _files.FileExists(path)
            ? JsonSerializer.Deserialize<DuneConfiguration>(
                _files.ReadText(path)
            ) ?? new()
            : new();
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
            return new(new XElement(root));
        }
        using StringReader text = new(_files.ReadText(path));
        using XmlReader reader = XmlReader.Create(
            text,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            }
        );
        return XDocument.Load(
            reader,
            LoadOptions.PreserveWhitespace
        );
    }

    /// <summary>Refuse les valeurs invalides avant la première écriture.</summary>
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
            snapshot.Settings.Categories.Length != 10
            || snapshot.Settings.Categories.Any(value =>
                value is not ('0' or '1')
            )
        )
        {
            throw new InvalidDataException(
                "Les dix catégories Dune doivent être définies."
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

    /// <summary>Enregistre ensemble réglages, dossiers, sélections et raccourcis.</summary>
    public void Save(
        string workspace,
        ConfigurationSnapshot snapshot
    )
    {
        Validate(snapshot);
        _files.CreateDirectory(workspace);
        WriteDocuments(workspace, Documents(snapshot));
    }

    /// <summary>Préserve les autres documents lors d'un traitement ou d'un export.</summary>
    public void SaveSettings(
        string workspace,
        DuneConfiguration settings
    )
    {
        string path = Path.Combine(workspace, FileName);
        string temporary =
            path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            _files.WriteBytes(
                temporary,
                JsonSerializer.SerializeToUtf8Bytes(
                    settings,
                    s_json
                )
            );
            _files.MoveFile(temporary, path, true);
        }
        finally
        {
            if (_files.FileExists(temporary))
            {
                _files.DeleteFile(temporary);
            }
        }
    }

    private void WriteDocuments(
        string workspace,
        Dictionary<string, byte[]> documents
    )
    {
        Dictionary<string, byte[]?> originals =
            documents.Keys.ToDictionary(
                name => name,
                name =>
                    _files.FileExists(
                        Path.Combine(workspace, name)
                    )
                        ? _files.ReadBytes(
                            Path.Combine(workspace, name)
                        )
                        : null
            );
        List<string> written = [];
        try
        {
            foreach ((string name, byte[] content) in documents)
            {
                written.Add(name);
                _files.WriteBytes(
                    Path.Combine(workspace, name),
                    content
                );
            }
        }
        catch (Exception failure)
            when (failure
                    is IOException
                        or UnauthorizedAccessException
            )
        {
            Rollback(workspace, originals, written, failure);
            throw;
        }
    }

    private void Rollback(
        string workspace,
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
                string path = Path.Combine(workspace, name);
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
        ConfigurationSnapshot snapshot
    ) =>
        new()
        {
            [FileName] = JsonSerializer.SerializeToUtf8Bytes(
                snapshot.Settings,
                s_json
            ),
            ["folders.xml"] = XmlBytes(
                new(
                    new XElement(
                        "folders",
                        snapshot.Folders.Select(FolderElement)
                    )
                )
            ),
            ["groups.xml"] = XmlBytes(snapshot.Groups),
            ["shortcuts.xml"] = XmlBytes(
                new(
                    new XElement(
                        "shortcuts",
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

    private static byte[] XmlBytes(XDocument document)
    {
        using MemoryStream stream = new();
        using (
            XmlWriter writer = XmlWriter.Create(
                stream,
                new XmlWriterSettings
                {
                    Encoding = new UTF8Encoding(false),
                    Indent = true,
                }
            )
        )
        {
            document.Save(writer);
        }
        return stream.ToArray();
    }
}
