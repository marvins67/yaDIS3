using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace YaDIS3.Core;

public sealed record Workspace(string Directory, IReadOnlyList<MediaCard> Cards, IReadOnlyList<string> Warnings);
public sealed class WorkspaceRepository
{
    private readonly SemaphoreSlim writer = new(1, 1);
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    internal static string FileHash(string path) => File.Exists(path) ? Hash(File.ReadAllBytes(path)) : "absent";
    internal static XDocument Parse(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32 * 1024 * 1024 });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }
    public Workspace Open(string path, CancellationToken cancellationToken = default)
    {
        path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (Path.GetFileName(path).Equals("data", StringComparison.OrdinalIgnoreCase)) path = Path.GetDirectoryName(path)!;
        var data = Path.Combine(path, "data");
        if (!Directory.Exists(data)) throw new DirectoryNotFoundException("Choisissez le workspace yaDIS3 contenant le dossier data.");
        var cards = new List<MediaCard>(); var warnings = new List<string>();
        foreach (var folder in Directory.EnumerateDirectories(data).Order(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = Path.Combine(folder, "data.xml"); if (!File.Exists(file)) continue;
            try { var bytes = File.ReadAllBytes(file); cards.Add(new MediaCard(file, Parse(bytes), Hash(bytes))); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or XmlException or InvalidDataException)
            { warnings.Add($"{Path.GetFileName(folder)} : {e.Message}"); }
        }
        return new Workspace(path, cards, warnings);
    }
    public void Create(string path) => Directory.CreateDirectory(Path.Combine(Path.GetFullPath(path), "data"));
    public async Task SaveAsync(MediaCard card, CancellationToken cancellationToken = default)
    {
        await writer.WaitAsync(cancellationToken);
        string? temporary = null;
        var changed = new List<(string Target, bool Existed)>();
        try
        {
            var snapshot = card.Snapshot(); var images = card.PendingArtwork();
            void CheckXml()
            {
                if (FileHash(card.FilePath) != card.SourceHash) throw new IOException("Cette fiche a changé sur le disque. Rouvrez la bibliothèque avant d'enregistrer.");
            }
            CheckXml();
            foreach (var image in images)
                if (FileHash(image.Target) != image.OriginalHash) throw new IOException("Une illustration a été modifiée par un autre programme. Rouvrez la bibliothèque.");
            Directory.CreateDirectory(card.DataDirectory);
            using var stream = new MemoryStream();
            using (var xml = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, CloseOutput = false })) snapshot.Document.Save(xml);
            var bytes = stream.ToArray(); Parse(bytes);
            temporary = card.FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
            CheckXml();
            foreach (var image in images)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (FileHash(image.Target) != image.OriginalHash) throw new IOException("Une illustration a changé pendant l'enregistrement.");
                var existed = File.Exists(image.Target);
                if (existed) File.Copy(image.Target, image.Target + ".yadis3-modern.bak", true);
                var copy = image.Target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try { File.Copy(image.Temporary, copy); File.Move(copy, image.Target, true); }
                finally { MediaCard.TryDelete(copy); }
                changed.Add((image.Target, existed));
            }
            CheckXml();
            if (File.Exists(card.FilePath)) File.Copy(card.FilePath, card.FilePath + ".yadis3-modern.bak", true);
            File.Move(temporary, card.FilePath, true); temporary = null;
            changed.Clear(); card.CompleteArtwork(images); card.MarkSaved(snapshot.Revision, Hash(bytes));
        }
        catch (Exception saveError)
        {
            var errors = new List<Exception>();
            foreach (var entry in changed.AsEnumerable().Reverse())
                try { if (entry.Existed) File.Copy(entry.Target + ".yadis3-modern.bak", entry.Target, true); else File.Delete(entry.Target); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { errors.Add(e); }
            if (errors.Count > 0) throw new AggregateException("Sauvegarde et restauration échouées. Les originaux restent dans les fichiers .yadis3-modern.bak.", new[] { saveError }.Concat(errors));
            throw;
        }
        finally { if (temporary is not null) MediaCard.TryDelete(temporary); writer.Release(); }
    }
    public MediaCard CreateManualSeries(Workspace workspace, string videoDirectory, IReadOnlyList<VideoEpisode>? detected = null)
    {
        var videoPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(videoDirectory));
        var title = Path.GetFileName(videoPath);
        var safe = new string(title.Select(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        if (safe.Length == 0) safe = "Série";
        if (safe.Length > 100) safe = safe[..100];
        var directory = Path.Combine(workspace.Directory, "data", safe);
        if (Directory.Exists(directory)) throw new IOException("Une fiche de ce nom existe déjà.");
        var doc = new XDocument(new XElement("data", new XElement("item",
            new XElement("filename", title), new XElement("path", Path.GetDirectoryName(videoPath) ?? ""), new XElement("dunepath", ""),
            new XElement("folder", "yes"), new XElement("type", "1"), new XElement("status", "3"), new XElement("date", ""), new XElement("selection", "no"))));
        var card = new MediaCard(Path.Combine(directory, "data.xml"), doc, "absent");
        card.Update(() => card.EnsureInfo());
        EpisodeScanner.Synchronize(card, detected ?? EpisodeScanner.Detect(videoPath)); return card;
    }
}
