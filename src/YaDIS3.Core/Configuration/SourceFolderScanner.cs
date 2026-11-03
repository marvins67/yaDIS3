using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>
/// Découvre les enfants directs des sources selon les règles de
/// MainForm dans yaDIS2.
/// </summary>
public sealed class SourceFolderScanner(
    IExportFileSystem? fileSystem = null
)
{
    private readonly IExportFileSystem _files =
        fileSystem ?? new ExportFileSystem();

    /// <summary>
    /// Crée les fiches absentes sans supprimer les informations
    /// existantes.
    /// </summary>
    public void Scan(
        string workspace,
        IReadOnlyList<MediaSourceFolder> folders,
        bool basicMode,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        ArgumentNullException.ThrowIfNull(folders);
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
        bool exists = _files.FileExists(path);
        XDocument document = exists
            ? WorkspaceRepository.Parse(_files.ReadBytes(path))
            : NewDocument(
                folder,
                name,
                _files.DirectoryExists(source)
            );
        XElement item = Item(document, path);
        XElement original = new(item);
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
        if (!exists || !XNode.DeepEquals(original, item))
        {
            _files.CreateDirectory(directory);
            Write(path, document);
        }
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

    /// <summary>
    /// Applique l'indisponibilité à la session, sans remplacer
    /// le statut enregistré par le dernier traitement.
    /// </summary>
    public void CheckAvailability(
        MediaCard card,
        IReadOnlyList<MediaSourceFolder> folders
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(folders);
        string parent = card.SourceDirectory;
        string source = Path.Combine(parent, card.FolderName);
        card.SourceUnavailable =
            folders.Any(folder => folder.Path == parent)
            && !_files.FileExists(source)
            && !_files.DirectoryExists(source);
    }

    internal void ValidateCards(
        string workspace,
        CancellationToken cancellationToken
    )
    {
        foreach (
            string directory in _files.EnumerateDirectories(
                Path.Combine(workspace, "data")
            )
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = Path.Combine(directory, "data.xml");
            if (_files.FileExists(path))
            {
                _ = Item(
                    WorkspaceRepository.Parse(
                        _files.ReadBytes(path)
                    ),
                    path
                );
            }
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
