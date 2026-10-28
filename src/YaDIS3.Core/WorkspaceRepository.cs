using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>
/// Contains the cards and warnings loaded from a workspace.
/// </summary>
public sealed record Workspace(
    string Directory,
    IReadOnlyList<MediaCard> Cards,
    IReadOnlyList<string> Warnings
);

/// <summary>
/// Loads cards and saves metadata and artwork with backups.
/// </summary>
public sealed class WorkspaceRepository
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
    /// Loads readable workspace cards and reports individual
    /// file failures.
    /// </summary>
    public Workspace Open(
        string path,
        CancellationToken cancellationToken = default
    )
    {
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

        string data = Path.Combine(path, "data");
        if (!Directory.Exists(data))
        {
            throw new DirectoryNotFoundException(
                "Choisissez le workspace yaDIS3 contenant le dossier data."
            );
        }

        var cards = new List<MediaCard>();
        var warnings = new List<string>();
        foreach (
            string folder in Directory
                .EnumerateDirectories(data)
                .Order(StringComparer.OrdinalIgnoreCase)
        )
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
    /// Creates the data directory for a new workspace.
    /// </summary>
    public void Create(string path)
    {
        Directory.CreateDirectory(
            Path.Combine(Path.GetFullPath(path), "data")
        );
    }

    /// <summary>
    /// Saves a card and its staged artwork with conflict checks
    /// and backups.
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
            void CheckXml()
            {
                if (FileHash(card.FilePath) != card.SourceHash)
                {
                    throw new IOException(
                        "Cette fiche a changé sur le disque. Rouvrez la bibliothèque avant d'enregistrer."
                    );
                }
            }

            CheckXml();
            foreach (StagedArtwork image in images)
            {
                if (FileHash(image.Target) != image.OriginalHash)
                {
                    throw new IOException(
                        "Une illustration a été modifiée par un autre programme. Rouvrez la bibliothèque."
                    );
                }
            }

            Directory.CreateDirectory(card.DataDirectory);
            using var stream = new MemoryStream();
            using (
                XmlWriter xml = XmlWriter.Create(
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
                snapshot.Document.Save(xml);
            }

            byte[] bytes = stream.ToArray();
            Parse(bytes);
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
            CheckXml();
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

                string copy =
                    image.Target
                    + "."
                    + Guid.NewGuid().ToString("N")
                    + ".tmp";
                try
                {
                    File.Copy(image.Temporary, copy);
                    File.Move(copy, image.Target, true);
                }
                finally
                {
                    MediaCard.TryDelete(copy);
                }

                changed.Add((image.Target, existed));
            }

            CheckXml();
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
        // Restore committed artwork, then propagate the failure.
        catch (Exception saveError)
        {
            var errors = new List<Exception>();
            foreach (
                (string Target, bool Existed) entry in changed
                    .AsEnumerable()
                    .Reverse()
            )
            {
                try
                {
                    if (entry.Existed)
                    {
                        File.Copy(
                            entry.Target + ".yadis3-modern.bak",
                            entry.Target,
                            true
                        );
                    }
                    else
                    {
                        File.Delete(entry.Target);
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

    /// <summary>
    /// Creates an editable series card from a local video
    /// directory.
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
}
