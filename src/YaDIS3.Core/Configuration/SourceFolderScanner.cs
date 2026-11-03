using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>Découvre les enfants directs des sources selon les règles de MainForm dans yaDIS2.</summary>
public sealed class SourceFolderScanner(
    IExportFileSystem? fileSystem = null
)
{
    private readonly IExportFileSystem _files =
        fileSystem ?? new ExportFileSystem();

    /// <summary>Crée les fiches absentes sans supprimer les informations existantes.</summary>
    public void Scan(
        string workspace,
        IReadOnlyList<MediaSourceFolder> folders,
        bool basicMode,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (MediaSourceFolder folder in folders)
        {
            if (!_files.DirectoryExists(folder.Path))
            {
                continue;
            }
            foreach (string source in Sources(folder, basicMode))
            {
                cancellationToken.ThrowIfCancellationRequested();
                Update(workspace, folder, source);
            }
        }
        MarkUnavailable(workspace, folders, cancellationToken);
    }

    private IEnumerable<string> Sources(
        MediaSourceFolder folder,
        bool basicMode
    )
    {
        IEnumerable<string> directories =
            _files.EnumerateDirectories(folder.Path);
        IEnumerable<string> files =
            folder.Type == 0 && !basicMode
                ? _files
                    .EnumerateFiles(folder.Path)
                    .Where(LegacyVideoFiles.IsVideo)
                : [];
        return directories.Concat(files).Where(Visible);
    }

    private bool Visible(string path) =>
        !Path.GetFileName(path).StartsWith('_')
        && !Path.GetFileName(path).Contains('~')
        && (
            _files.GetAttributes(path)
            & (FileAttributes.Hidden | FileAttributes.System)
        ) == 0;

    private void Update(
        string workspace,
        MediaSourceFolder folder,
        string source
    )
    {
        string name = Path.GetFileName(source);
        string directory = Path.Combine(workspace, "data", name);
        string path = Path.Combine(directory, "data.xml");
        XDocument document = _files.FileExists(path)
            ? WorkspaceRepository.Parse(_files.ReadBytes(path))
            : NewDocument(
                folder,
                name,
                _files.DirectoryExists(source)
            );
        XElement item = Item(document, path);
        string type = folder.Type.ToString(
            System.Globalization.CultureInfo.InvariantCulture
        );
        if (
            item.Element("type")?.Value != type
            || item.Element("dunepath")?.Value != folder.DunePath
        )
        {
            item.SetElementValue("status", "3");
        }
        item.SetElementValue("path", folder.Path);
        item.SetElementValue("type", type);
        item.SetElementValue("dunepath", folder.DunePath);
        _files.CreateDirectory(directory);
        Write(path, document);
    }

    private static XDocument NewDocument(
        MediaSourceFolder folder,
        string name,
        bool isFolder
    ) =>
        new(
            new XElement(
                "data",
                new XElement(
                    "item",
                    new XElement("filename", name),
                    new XElement("path", folder.Path),
                    new XElement("dunepath", folder.DunePath),
                    new XElement(
                        "folder",
                        isFolder ? "yes" : "no"
                    ),
                    new XElement("type", folder.Type),
                    new XElement("status", "3"),
                    new XElement("date", ""),
                    new XElement("selection", "no")
                )
            )
        );

    private void MarkUnavailable(
        string workspace,
        IReadOnlyList<MediaSourceFolder> folders,
        CancellationToken cancellationToken
    )
    {
        string data = Path.Combine(workspace, "data");
        if (!_files.DirectoryExists(data))
        {
            return;
        }
        foreach (
            string directory in _files.EnumerateDirectories(data)
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = Path.Combine(directory, "data.xml");
            if (_files.FileExists(path))
            {
                CheckAvailability(path, folders);
            }
        }
    }

    private void CheckAvailability(
        string path,
        IReadOnlyList<MediaSourceFolder> folders
    )
    {
        XDocument document = WorkspaceRepository.Parse(
            _files.ReadBytes(path)
        );
        XElement item = Item(document, path);
        string parent = item.Element("path")?.Value ?? "";
        string source = Path.Combine(
            parent,
            item.Element("filename")?.Value ?? ""
        );
        if (
            folders.Any(folder => folder.Path == parent)
            && !_files.FileExists(source)
            && !_files.DirectoryExists(source)
        )
        {
            item.SetElementValue("status", "2");
            Write(path, document);
        }
    }

    private static XElement Item(
        XDocument document,
        string path
    ) =>
        (
            document.Root?.Name == "item"
                ? document.Root
                : document.Root?.Element("item")
        )
        ?? throw new InvalidDataException(
            "En-tête de fiche absent : " + path
        );

    private void Write(string path, XDocument document)
    {
        using MemoryStream stream = new();
        document.Save(stream);
        _files.WriteBytes(path, stream.ToArray());
    }
}
