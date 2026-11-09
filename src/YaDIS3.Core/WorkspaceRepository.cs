using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>
/// Charge les fiches et sauvegarde leurs métadonnées et
/// illustrations.
/// </summary>
public sealed class WorkspaceRepository
    : IWorkspaceRepository,
        IDisposable
{
    private readonly SemaphoreSlim _writer = new(1, 1);

    internal static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    internal static string FileHash(string path)
    {
        return File.Exists(path)
            ? Hash(File.ReadAllBytes(path))
            : "absent";
    }

    internal static XDocument Parse(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using XmlReader reader = XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 32 * 1024 * 1024,
            }
        );
        return XDocument.Load(
            reader,
            LoadOptions.PreserveWhitespace
        );
    }

    /// <summary>
    /// Accepte la racine du workspace ou son dossier data.
    /// </summary>
    public static string ResolveDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(path)
        );
        if (
            Path.GetFileName(path)
                .Equals(
                    "data",
                    StringComparison.OrdinalIgnoreCase
                )
        )
        {
            path = Path.GetDirectoryName(path)!;
        }

        return path;
    }

    /// <summary>
    /// Charge les fiches lisibles et signale les fichiers en
    /// échec.
    /// </summary>
    public Workspace Open(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        path = ResolveDirectory(path);
        string data = Path.Combine(path, "data");
        if (!Directory.Exists(data))
        {
            throw new DirectoryNotFoundException(
                "Choisissez le workspace yaDIS3 contenant le dossier data."
            );
        }

        var cards = new List<MediaCard>();
        var warnings = new List<string>();
        // MainForm charge la collection d'export dans l'ordre du système de fichiers.
        foreach (string folder in Directory.EnumerateDirectories(data))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string file = Path.Combine(folder, "data.xml");
            if (!File.Exists(file))
            {
                continue;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(file);
                cards.Add(
                    new MediaCard(
                        file,
                        Parse(bytes),
                        Hash(bytes)
                    )
                );
            }
            catch (Exception exception)
                when (exception
                        is IOException
                            or UnauthorizedAccessException
                            or XmlException
                            or InvalidDataException
                )
            {
                warnings.Add(
                    $"{Path.GetFileName(folder)} : {exception.Message}"
                );
            }
        }

        return new Workspace(path, cards, warnings);
    }

    /// <summary>
    /// Crée le dossier de données d'un nouveau workspace.
    /// </summary>
    public void Create(string path)
    {
        path = Path.GetFullPath(path);
        foreach (
            string directory in new[]
            {
                "data",
                "cache",
                "interface",
            }
        )
        {
            Directory.CreateDirectory(
                Path.Combine(path, directory)
            );
        }
        CreateDocument(
            path,
            "folders.xml",
            new XElement("folders")
        );
        CreateDocument(
            path,
            "groups.xml",
            new XElement(
                "groups",
                new XElement("sagas"),
                new XElement("selections")
            )
        );
    }

    private static void CreateDocument(
        string path,
        string name,
        XElement root
    )
    {
        string file = Path.Combine(path, name);
        if (!File.Exists(file))
        {
            new XDocument(root).Save(file);
        }
    }

    /// <summary>
    /// Sauvegarde une fiche et ses illustrations avec contrôles
    /// de conflit et sauvegardes.
    /// </summary>
    public async Task SaveAsync(
        MediaCard card,
        CancellationToken cancellationToken = default
    )
    {
        await _writer.WaitAsync(cancellationToken);
        string? temporary = null;
        var changed = new List<(string Target, bool Existed)>();
        try
        {
            (XDocument Document, long Revision) snapshot =
                card.Snapshot();
            IReadOnlyList<StagedArtwork> images =
                card.PendingArtwork();
            CheckXmlUnchanged(card);
            ValidateArtwork(images);

            Directory.CreateDirectory(card.DataDirectory);
            byte[] bytes = SerializeMetadata(snapshot.Document);
            temporary =
                card.FilePath
                + "."
                + Guid.NewGuid().ToString("N")
                + ".tmp";
            await File.WriteAllBytesAsync(
                temporary,
                bytes,
                cancellationToken
            );
            CheckXmlUnchanged(card);
            CommitArtwork(images, changed, cancellationToken);
            CheckXmlUnchanged(card);
            if (File.Exists(card.FilePath))
            {
                File.Copy(
                    card.FilePath,
                    card.FilePath + ".yadis3-modern.bak",
                    true
                );
            }

            File.Move(temporary, card.FilePath, true);
            temporary = null;
            changed.Clear();
            card.CompleteArtwork(images);
            card.MarkSaved(snapshot.Revision, Hash(bytes));
        }
        // Restaurer les illustrations déjà remplacées avant de propager l’erreur.
        catch (Exception saveError)
        {
            RestoreArtwork(changed, saveError);

            throw;
        }
        finally
        {
            if (temporary is not null)
            {
                MediaCard.TryDelete(temporary);
            }

            _writer.Release();
        }
    }

    private static void CheckXmlUnchanged(MediaCard card)
    {
        if (FileHash(card.FilePath) != card.SourceHash)
        {
            throw new IOException(
                "Cette fiche a changé sur le disque. Rouvrez la bibliothèque avant d'enregistrer."
            );
        }
    }

    private static void ValidateArtwork(
        IReadOnlyList<StagedArtwork> images
    )
    {
        foreach (StagedArtwork image in images)
        {
            if (FileHash(image.Target) != image.OriginalHash)
            {
                throw new IOException(
                    "Une illustration a été modifiée par un autre programme. Rouvrez la bibliothèque."
                );
            }
        }
    }

    private static byte[] SerializeMetadata(XDocument document)
    {
        using MemoryStream stream = new();
        using (
            XmlWriter writer = XmlWriter.Create(
                stream,
                new XmlWriterSettings
                {
                    Encoding = new UTF8Encoding(false),
                    Indent = false,
                    CloseOutput = false,
                }
            )
        )
        {
            document.Save(writer);
        }
        byte[] bytes = stream.ToArray();
        Parse(bytes);
        return bytes;
    }

    private static void CommitArtwork(
        IReadOnlyList<StagedArtwork> images,
        List<(string Target, bool Existed)> changed,
        CancellationToken cancellationToken
    )
    {
        foreach (StagedArtwork image in images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FileHash(image.Target) != image.OriginalHash)
            {
                throw new IOException(
                    "Une illustration a changé pendant l'enregistrement."
                );
            }
            bool existed = File.Exists(image.Target);
            if (existed)
            {
                File.Copy(
                    image.Target,
                    image.Target + ".yadis3-modern.bak",
                    true
                );
            }
            CommitArtworkFile(image);
            changed.Add((image.Target, existed));
        }
    }

    private static void CommitArtworkFile(StagedArtwork image)
    {
        string temporary =
            image.Target
            + "."
            + Guid.NewGuid().ToString("N")
            + ".tmp";
        try
        {
            File.Copy(image.Temporary, temporary);
            File.Move(temporary, image.Target, true);
        }
        finally
        {
            MediaCard.TryDelete(temporary);
        }
    }

    private static void RestoreArtwork(
        List<(string Target, bool Existed)> changed,
        Exception saveError
    )
    {
        List<Exception> errors = [];
        foreach (
            (string target, bool existed) in changed
                .AsEnumerable()
                .Reverse()
        )
        {
            try
            {
                if (existed)
                {
                    File.Copy(
                        target + ".yadis3-modern.bak",
                        target,
                        true
                    );
                }
                else
                {
                    File.Delete(target);
                }
            }
            catch (Exception exception)
                when (exception
                        is IOException
                            or UnauthorizedAccessException
                )
            {
                errors.Add(exception);
            }
        }
        if (errors.Count > 0)
        {
            throw new AggregateException(
                "Sauvegarde et restauration échouées. Les originaux restent dans les fichiers .yadis3-modern.bak.",
                new[] { saveError }.Concat(errors)
            );
        }
    }

    /// <summary>
    /// Crée une fiche de série éditable à partir d'un dossier
    /// vidéo local.
    /// </summary>
    public MediaCard CreateManualSeries(
        Workspace workspace,
        string videoDirectory,
        IReadOnlyList<VideoEpisode>? detected = null
    )
    {
        string videoPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(videoDirectory)
        );
        string title = Path.GetFileName(videoPath);
        var safe = new string(
            title
                .Select(character =>
                    char.IsControl(character)
                    || "<>:\"/\\|?*".Contains(character)
                        ? '_'
                        : character
                )
                .ToArray()
        )
            .Trim()
            .TrimEnd('.');
        if (safe.Length == 0)
        {
            safe = "Série";
        }

        if (safe.Length > 100)
        {
            safe = safe[..100];
        }

        string directory = Path.Combine(
            workspace.Directory,
            "data",
            safe
        );
        if (Directory.Exists(directory))
        {
            throw new IOException(
                "Une fiche de ce nom existe déjà."
            );
        }

        var document = new XDocument(
            new XElement(
                "data",
                new XElement(
                    "item",
                    new XElement("filename", title),
                    new XElement(
                        "path",
                        Path.GetDirectoryName(videoPath) ?? ""
                    ),
                    new XElement("dunepath", ""),
                    new XElement("folder", "yes"),
                    new XElement("type", "1"),
                    new XElement("status", "3"),
                    new XElement("date", ""),
                    new XElement("selection", "no")
                )
            )
        );
        var card = new MediaCard(
            Path.Combine(directory, "data.xml"),
            document,
            "absent"
        );
        card.Update(() => card.EnsureInfo());
        EpisodeScanner.Synchronize(
            card,
            detected ?? EpisodeScanner.Detect(videoPath)
        );
        return card;
    }

    /// <summary>
    /// Libère le verrou d'écriture lorsque le workspace est
    /// fermé.
    /// </summary>
    public void Dispose()
    {
        _writer.Dispose();
    }
}
