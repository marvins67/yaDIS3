using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
namespace YaDIS3.Rendering;

public sealed record ExportResult(string Directory, int MediaCount, int EpisodeCount, IReadOnlyList<string> Warnings);
public sealed record ProcessingResult(string PreviewPath, string? EpisodePreviewPath);

/// <summary>Builds a standalone managed catalog. Media files and other directories are never modified.</summary>
public sealed class DuneExporter(DuneTemplate template, RenderOptions options, IReadOnlyList<PathMapping> mappings)
{
    private const string Marker = "yadis3-export.json";
    private sealed record Manifest(string Producer, int Version, string Template, int MediaCount, int EpisodeCount);
    private sealed record Playback(MediaContext Media, IReadOnlyList<string> MovieUrls, IReadOnlyDictionary<XElement, string> Episodes);
    private string Extension => options.AaiBackgrounds ? ".aai" : ".jpg";
    private static string Relative(MediaContext media) => (media.IsSeries ? "01/" : "00/99/") + media.Id;
    private static string SafeCaption(string text) => new(text.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
    private static bool Owned(string directory)
    {
        try { var marker = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Path.Combine(directory, Marker))); return marker is { Producer: "yaDIS3.Modern", Version: 1 }; }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return false; }
    }
    private Playback Plan(MediaContext media, List<string> warnings)
    {
        string Resolve(string path) => DunePaths.Resolve(path, mappings, media.Header.ValueAt("path"), media.Header.ValueAt("dunepath"));
        if (media.IsSeries)
        {
            if (media.Episodes.Count == 0) warnings.Add($"{SafeCaption(media.Title)} : série sans épisode local.");
            return new(media, [], media.Episodes.ToDictionary(e => e, e => Resolve(e.ValueAt("file"))));
        }
        var local = DunePaths.JoinLocal(media.Header.ValueAt("path"), media.Header.ValueAt("filename"));
        if (media.Header.ValueAt("folder") != "yes") return new(media, [Resolve(local)], new Dictionary<XElement, string>());
        if (Directory.Exists(local))
        {
            if (Directory.Exists(Path.Combine(local, "BDMV")) || Directory.Exists(Path.Combine(local, "VIDEO_TS"))) return new(media, [Resolve(local)], new Dictionary<XElement, string>());
            var extensions = new HashSet<string>([".mkv", ".mk3d", ".mp4", ".m4v", ".avi", ".iso", ".m2ts", ".mts", ".ts", ".ps", ".mpg", ".mpeg", ".vob", ".mov", ".qt", ".asf", ".wmv", ".divx", ".ogg", ".ogm"], StringComparer.OrdinalIgnoreCase);
            var files = Directory.EnumerateFiles(local).Where(p => extensions.Contains(Path.GetExtension(p))).Order(StringComparer.Ordinal).ToArray();
            if (files.Length > 0) return new(media, files.Select(Resolve).ToArray(), new Dictionary<XElement, string>());
        }
        warnings.Add($"{SafeCaption(media.Title)} : dossier vidéo non analysable ; le Dune ouvrira son contenu en lecture.");
        return new(media, [Resolve(local)], new Dictionary<XElement, string>());
    }
    public ExportResult Export(IReadOnlyList<MediaContext> media, string parentDirectory, CancellationToken cancellationToken = default, IProgress<string>? progress = null)
    {
        if (media.Count == 0) throw new InvalidOperationException("La bibliothèque est vide.");
        var warnings = new List<string>(); var plans = media.Select(m => Plan(m, warnings)).ToArray();
        if (media.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count() != media.Count) throw new InvalidDataException("Deux fiches ont le même identifiant d'export.");
        var parent = Path.GetFullPath(parentDirectory); var destination = Path.Combine(parent, "yaDIS3-modern");
        if (Directory.Exists(destination) && (!Owned(destination) || (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)) throw new IOException("Le dossier yaDIS3-modern existe mais n'est pas un export reconnu de cette application.");
        Directory.CreateDirectory(parent);
        using var exportLock = new FileStream(Path.Combine(parent, ".yadis3-export.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var stage = Path.Combine(parent, ".yadis3-stage-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
        var backup = destination + ".previous";
        try
        {
            using var renderer = new TemplateRenderer(template, options);
            foreach (var plan in plans)
            {
                cancellationToken.ThrowIfCancellationRequested(); progress?.Report("Traitement : " + SafeCaption(plan.Media.Title));
                var output = Path.Combine(stage, Relative(plan.Media).Replace('/', Path.DirectorySeparatorChar));
                renderer.Render(plan.Media, output, cancellationToken, progress); WriteMedia(plan, output);
            }
            WriteIndexes(renderer, plans.Select(p => p.Media).ToArray(), stage, cancellationToken);
            var count = plans.Sum(p => p.Episodes.Count);
            File.WriteAllText(Path.Combine(stage, Marker), JsonSerializer.Serialize(new Manifest("yaDIS3.Modern", 1, template.Name, media.Count, count), new JsonSerializerOptions { WriteIndented = true }));
            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(destination))
            {
                if (!Owned(destination) || (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0) throw new IOException("Le dossier de destination a changé pendant l'export.");
                if (Directory.Exists(backup))
                {
                    if (!Owned(backup) || (File.GetAttributes(backup) & FileAttributes.ReparsePoint) != 0) backup += "-" + Guid.NewGuid().ToString("N");
                    else Directory.Delete(backup, true);
                }
                Directory.Move(destination, backup);
            }
            try { Directory.Move(stage, destination); }
            catch { if (Directory.Exists(backup) && !Directory.Exists(destination)) Directory.Move(backup, destination); throw; }
            return new(destination, media.Count, count, warnings);
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
    }
    private void WriteMedia(Playback plan, string output)
    {
        var media = plan.Media; var definition = new DuneFile().Screen("background" + Extension).Icon("icon.aai", template.Node(media.IsSeries ? "tv/iconview/icon" : "movie/iconview/icon").Attr("scale", "1"));
        if (!media.IsSeries)
        {
            var url = plan.MovieUrls[0];
            if (plan.MovieUrls.Count > 1)
            { File.WriteAllText(Path.Combine(output, "playlist.m3u"), "#EXTM3U\n" + string.Join("\n", plan.MovieUrls) + "\n", new UTF8Encoding(false)); url = "playlist.m3u"; }
            definition.Set("paint_icons", "no").Item(0, options.Language == "fr" ? "Lire" : "Play", url, play: true).Save(output); return;
        }
        var seasons = template.Node("tv/synopsis/season/seasonicon"); var area = seasons.Element("area")!;
        definition.Grid(area, IntElement(area, "columns", 5), IntElement(area, "rows", 2));
        var index = 0;
        foreach (var group in media.Episodes.GroupBy(MediaContext.ViewingSeason))
        {
            var season = group.Key; var directory = Path.Combine(output, season);
            definition.Item(index++, season == "00" ? (options.Language == "fr" ? "Spéciaux" : "Specials") : (options.Language == "fr" ? "Saison " : "Season ") + season.TrimStart('0'), season, season + "/icon.aai", scale: seasons.Element("icon").Attr("scale", "1"));
            var episodes = template.Node("tv/synopsis/episode/episodeicon"); var episodeArea = episodes.Element("area")!;
            var seasonFile = new DuneFile().Screen("../background2" + Extension).Icon("icon.aai", seasons.Element("icon").Attr("scale", "1")).Grid(episodeArea, 1, 1);
            var eindex = 0;
            foreach (var episode in group)
            {
                var folder = MediaContext.EpisodeFolder(episode);
                seasonFile.Item(eindex++, SafeCaption(episode.ValueAt("title")), plan.Episodes[episode], folder + "/icon.aai", play: true);
                new DuneFile().Icon("icon.aai").Set("media_url", plan.Episodes[episode]).Set("media_action", "play").Save(Path.Combine(directory, folder));
            }
            seasonFile.Save(directory);
        }
        definition.Save(output);
    }
    private static int IntElement(XElement node, string field, int fallback) => int.TryParse(node.ValueAt(field), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0 && n <= 100 ? n : fallback;
    private void Listing(TemplateRenderer renderer, IReadOnlyList<MediaContext> cards, string stage, string relative, bool series)
    {
        var output = Path.Combine(stage, relative.Replace('/', Path.DirectorySeparatorChar)); var kind = series ? "tv" : "movie"; renderer.RenderMenu(output, kind);
        var view = template.Node(kind + "/iconview"); var area = view.Element("area")!;
        var file = new DuneFile().Screen("background" + Extension).Grid(area, IntElement(area, "columns", 5), IntElement(area, "rows", 3));
        var index = 0;
        foreach (var card in cards.OrderBy(m => m.Info.ValueAt("sort_title") is { Length: > 0 } sort ? sort : m.Title, StringComparer.InvariantCultureIgnoreCase))
        {
            var to = Path.GetRelativePath(output, Path.Combine(stage, Relative(card).Replace('/', Path.DirectorySeparatorChar))).Replace('\\', '/');
            file.Item(index++, SafeCaption(card.Title), to, to + "/icon.aai", scale: view.Element("icon").Attr("scale", "1"));
        }
        file.Save(output);
    }
    private void Navigation(TemplateRenderer renderer, string stage, string relative, IReadOnlyList<(string Label, string Target)> entries, XElement? panel = null)
    {
        var output = Path.Combine(stage, relative.Replace('/', Path.DirectorySeparatorChar)); renderer.RenderMenu(output, "menu", panel);
        var area = panel ?? template.Node("menu/main/area"); var columns = panel is null ? Math.Max(1, entries.Count) : IntElement(panel, "columns", 3); var rows = panel is null ? 1 : IntElement(panel, "rows", 8);
        var width = Math.Max(1, area.Int("width") / columns); var height = Math.Max(1, area.Int("height") / rows);
        var file = new DuneFile().Screen("background" + Extension).Grid(area, columns, rows);
        for (var i = 0; i < entries.Count; i++)
        {
            var icon = $"nav-{i}.aai"; var selected = $"nav-{i}-sel.aai";
            renderer.MenuLabel(Path.Combine(output, icon), entries[i].Label, width, height, panel is not null, false);
            renderer.MenuLabel(Path.Combine(output, selected), entries[i].Label, width, height, panel is not null, true);
            var target = Path.GetRelativePath(output, Path.Combine(stage, entries[i].Target.Replace('/', Path.DirectorySeparatorChar))).Replace('\\', '/');
            file.Item(i, SafeCaption(entries[i].Label), target, icon, selected);
        }
        file.Save(output);
    }
    private void WriteIndexes(TemplateRenderer renderer, IReadOnlyList<MediaContext> media, string stage, CancellationToken cancellationToken)
    {
        bool fr = options.Language == "fr";
        var top = new List<(string, string)>();
        if (media.Any(m => !m.IsSeries)) { Listing(renderer, media.Where(m => !m.IsSeries).ToArray(), stage, "00", false); top.Add((fr ? "Films" : "Movies", "00")); }
        if (media.Any(m => m.IsSeries)) { Listing(renderer, media.Where(m => m.IsSeries).ToArray(), stage, "01", true); top.Add((fr ? "Séries" : "Series", "01")); }
        var indexes = new[] { ("genres", fr ? "Genres" : "Genres"), ("years", fr ? "Années" : "Years"), ("ratings", fr ? "Notes" : "Ratings"), ("alphabet", "A–Z") };
        foreach (var (kind, label) in indexes)
        {
            cancellationToken.ThrowIfCancellationRequested(); var panel = template.Root.XPath("menu/index/" + kind + "/area"); if (panel is null) continue;
            IEnumerable<(string Key, MediaContext Card)> Group(MediaContext card)
            {
                if (kind == "genres") return card.Info.Descendants("genre").Select(g => (g.Value.Trim(), card)).Where(p => p.Item1.Length > 0);
                var value = kind switch
                {
                    "years" => card.Info.ValueAt("year") is { Length: >= 4 } year ? year[..4] : "?",
                    "ratings" => double.TryParse(card.Info.ValueAt("rating").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && n is >= 0 and <= 10 ? ((int)n).ToString(CultureInfo.InvariantCulture) + "/10" : "?",
                    _ => Alphabet(card.Title)
                };
                return [(value, card)];
            }
            var groups = media.SelectMany(Group).GroupBy(p => p.Key, StringComparer.InvariantCultureIgnoreCase).OrderBy(g => g.Key, StringComparer.InvariantCultureIgnoreCase);
            var nav = new List<(string, string)>();
            foreach (var group in groups)
            {
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(group.Key.Normalize().ToUpperInvariant())).AsSpan(0, 8)).ToLowerInvariant();
                var relative = kind + "/" + hash; var types = new List<(string, string)>();
                foreach (var series in new[] { false, true })
                {
                    var cards = group.Select(p => p.Card).Where(m => m.IsSeries == series).DistinctBy(m => m.Id).ToArray(); if (cards.Length == 0) continue;
                    var listing = relative + (series ? "/01" : "/00"); Listing(renderer, cards, stage, listing, series); types.Add((series ? (fr ? "Séries" : "Series") : (fr ? "Films" : "Movies"), listing));
                }
                if (types.Count == 1) nav.Add((group.Key, types[0].Item2)); else { Navigation(renderer, stage, relative, types); nav.Add((group.Key, relative)); }
            }
            if (nav.Count == 0) continue;
            Navigation(renderer, stage, kind, nav, panel); top.Add((label, kind));
        }
        Navigation(renderer, stage, "", top);
    }
    private static string Alphabet(string title)
    {
        var plain = new string(title.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).TrimStart();
        if (plain.Length == 0) return "#"; var c = char.ToUpperInvariant(plain[0]); return char.IsLetter(c) ? c.ToString() : char.IsDigit(c) ? "0–9" : "#";
    }
    public static ProcessingResult Process(MediaContext media, DuneTemplate template, RenderOptions options, string workspace, string? episodeKey = null, CancellationToken cancellationToken = default, IProgress<string>? progress = null)
    {
        var parent = Path.Combine(workspace, "interface-yaDIS3"); Directory.CreateDirectory(parent);
        var stage = Path.Combine(parent, ".stage-" + Guid.NewGuid().ToString("N")); var target = Path.Combine(parent, media.Id);
        bool RenderCacheOwned(string path)
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(path, "render.json")));
                return document.RootElement.TryGetProperty("Producer", out var name) && name.GetString() == "yaDIS3.Modern" && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return false; }
        }
        if (Directory.Exists(target) && !RenderCacheOwned(target)) throw new IOException("Le dossier de cache existe mais n'est pas un rendu reconnu de cette application.");
        using var processingLock = new FileStream(Path.Combine(parent, media.Id + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            using var renderer = new TemplateRenderer(template, options); var rendered = renderer.Render(media, stage, cancellationToken, progress);
            File.WriteAllText(Path.Combine(stage, "render.json"), JsonSerializer.Serialize(new { Producer = "yaDIS3.Modern", Version = 1, MediaId = media.Id, Template = template.Name }));
            cancellationToken.ThrowIfCancellationRequested();
            var backup = target + ".previous";
            if (Directory.Exists(target))
            {
                if (!RenderCacheOwned(target)) throw new IOException("Le cache a changé pendant le rendu.");
                if (Directory.Exists(backup))
                {
                    if (RenderCacheOwned(backup)) Directory.Delete(backup, true); else backup += "-" + Guid.NewGuid().ToString("N");
                }
                Directory.Move(target, backup);
            }
            try { Directory.Move(stage, target); }
            catch { if (Directory.Exists(backup) && !Directory.Exists(target)) Directory.Move(backup, target); throw; }
            var preview = Path.Combine(media.DataDirectory, "preview.jpg"); var temp = preview + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.Copy(Path.Combine(target, "preview.jpg"), temp); File.Move(temp, preview, true); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            string? selected = episodeKey is not null && rendered.EpisodePreviews.TryGetValue(episodeKey, out var path) ? Path.Combine(target, Path.GetRelativePath(stage, path)) : null;
            return new(preview, selected);
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
    }
}
