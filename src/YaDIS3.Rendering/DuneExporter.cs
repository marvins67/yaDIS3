using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace YaDIS3.Rendering;

/// <summary>
/// Reports the exported catalog location, counts, and
/// warnings.
/// </summary>
public sealed record ExportResult(
    string Directory,
    int MediaCount,
    int EpisodeCount,
    IReadOnlyList<string> Warnings
);

/// <summary>
/// Identifies the generated card and episode previews.
/// </summary>
public sealed record ProcessingResult(
    string PreviewPath,
    string? EpisodePreviewPath
);

/// <summary>
/// Builds a standalone catalog without modifying media
/// files.
/// </summary>
public sealed class DuneExporter(
    DuneTemplate template,
    RenderOptions options,
    IReadOnlyList<PathMapping> mappings
)
{
    private const string Marker = "yadis3-export.json";

    private sealed record Manifest(
        string Producer,
        int Version,
        string Template,
        int MediaCount,
        int EpisodeCount
    );

    private sealed record Playback(
        MediaContext Media,
        IReadOnlyList<string> MovieUrls,
        IReadOnlyDictionary<XElement, string> Episodes
    );

    private string Extension =>
        options.AaiBackgrounds ? ".aai" : ".jpg";

    private static string Relative(MediaContext media)
    {
        return (media.IsSeries ? "01/" : "00/99/") + media.Id;
    }

    private static string SafeCaption(string text)
    {
        return new(
            text.Select(character =>
                    char.IsControl(character) ? ' ' : character
                )
                .ToArray()
        );
    }

    private static bool Owned(string directory)
    {
        try
        {
            Manifest? marker =
                JsonSerializer.Deserialize<Manifest>(
                    File.ReadAllText(
                        Path.Combine(directory, Marker)
                    )
                );
            return marker
                is { Producer: "yaDIS3.Modern", Version: 1 };
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or JsonException
                        or UnauthorizedAccessException
            )
        {
            return false;
        }
    }

    private Playback Plan(
        MediaContext media,
        List<string> warnings
    )
    {
        string Resolve(string path) =>
            DunePaths.Resolve(
                path,
                mappings,
                media.Header.ValueAt("path"),
                media.Header.ValueAt("dunepath")
            );
        if (media.IsSeries)
        {
            if (media.Episodes.Count == 0)
            {
                warnings.Add(
                    $"{SafeCaption(media.Title)} : série sans épisode local."
                );
            }

            return new(
                media,
                [],
                media.Episodes.ToDictionary(
                    element => element,
                    element => Resolve(element.ValueAt("file"))
                )
            );
        }

        string local = DunePaths.JoinLocal(
            media.Header.ValueAt("path"),
            media.Header.ValueAt("filename")
        );
        if (media.Header.ValueAt("folder") != "yes")
        {
            return new(
                media,
                [Resolve(local)],
                new Dictionary<XElement, string>()
            );
        }

        if (Directory.Exists(local))
        {
            if (
                Directory.Exists(Path.Combine(local, "BDMV"))
                || Directory.Exists(
                    Path.Combine(local, "VIDEO_TS")
                )
            )
            {
                return new(
                    media,
                    [Resolve(local)],
                    new Dictionary<XElement, string>()
                );
            }

            var extensions = new HashSet<string>(
                [
                    ".mkv",
                    ".mk3d",
                    ".mp4",
                    ".m4v",
                    ".avi",
                    ".iso",
                    ".m2ts",
                    ".mts",
                    ".ts",
                    ".ps",
                    ".mpg",
                    ".mpeg",
                    ".vob",
                    ".mov",
                    ".qt",
                    ".asf",
                    ".wmv",
                    ".divx",
                    ".ogg",
                    ".ogm",
                ],
                StringComparer.OrdinalIgnoreCase
            );
            var files = Directory
                .EnumerateFiles(local)
                .Where(pathValue =>
                    extensions.Contains(
                        Path.GetExtension(pathValue)
                    )
                )
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (files.Length > 0)
            {
                return new(
                    media,
                    files.Select(Resolve).ToArray(),
                    new Dictionary<XElement, string>()
                );
            }
        }

        warnings.Add(
            $"{SafeCaption(media.Title)} : dossier vidéo non analysable ; le Dune ouvrira son contenu en lecture."
        );
        return new(
            media,
            [Resolve(local)],
            new Dictionary<XElement, string>()
        );
    }

    /// <summary>
    /// Renders and replaces the managed Dune catalog with
    /// rollback support.
    /// </summary>
    public ExportResult Export(
        IReadOnlyList<MediaContext> media,
        string parentDirectory,
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null
    )
    {
        if (media.Count == 0)
        {
            throw new InvalidOperationException(
                "La bibliothèque est vide."
            );
        }

        var warnings = new List<string>();
        var plans = media
            .Select(mediaValue => Plan(mediaValue, warnings))
            .ToArray();
        if (
            media
                .Select(mediaValue => mediaValue.Id)
                .Distinct(StringComparer.Ordinal)
                .Count() != media.Count
        )
        {
            throw new InvalidDataException(
                "Deux fiches ont le même identifiant d'export."
            );
        }

        string parent = Path.GetFullPath(parentDirectory);
        string destination = Path.Combine(
            parent,
            "yaDIS3-modern"
        );
        if (
            Directory.Exists(destination)
            && (
                !Owned(destination)
                || (
                    File.GetAttributes(destination)
                    & FileAttributes.ReparsePoint
                ) != 0
            )
        )
        {
            throw new IOException(
                "Le dossier yaDIS3-modern existe mais n'est pas un export reconnu de cette application."
            );
        }

        Directory.CreateDirectory(parent);
        using var exportLock = new FileStream(
            Path.Combine(parent, ".yadis3-export.lock"),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None
        );
        string stage = Path.Combine(
            parent,
            ".yadis3-stage-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(stage);
        string backup = destination + ".previous";
        try
        {
            using var renderer = new TemplateRenderer(
                template,
                options
            );
            foreach (Playback plan in plans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(
                    "Traitement : "
                        + SafeCaption(plan.Media.Title)
                );
                string output = Path.Combine(
                    stage,
                    Relative(plan.Media)
                        .Replace(
                            '/',
                            Path.DirectorySeparatorChar
                        )
                );
                renderer.Render(
                    plan.Media,
                    output,
                    cancellationToken,
                    progress
                );
                WriteMedia(plan, output);
            }

            WriteIndexes(
                renderer,
                plans.Select(item => item.Media).ToArray(),
                stage,
                cancellationToken
            );
            int count = plans.Sum(item => item.Episodes.Count);
            File.WriteAllText(
                Path.Combine(stage, Marker),
                JsonSerializer.Serialize(
                    new Manifest(
                        "yaDIS3.Modern",
                        1,
                        template.Name,
                        media.Count,
                        count
                    ),
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                    }
                )
            );
            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(destination))
            {
                if (
                    !Owned(destination)
                    || (
                        File.GetAttributes(destination)
                        & FileAttributes.ReparsePoint
                    ) != 0
                )
                {
                    throw new IOException(
                        "Le dossier de destination a changé pendant l'export."
                    );
                }

                if (Directory.Exists(backup))
                {
                    if (
                        !Owned(backup)
                        || (
                            File.GetAttributes(backup)
                            & FileAttributes.ReparsePoint
                        ) != 0
                    )
                    {
                        backup +=
                            "-" + Guid.NewGuid().ToString("N");
                    }
                    else
                    {
                        Directory.Delete(backup, true);
                    }
                }

                Directory.Move(destination, backup);
            }

            try
            {
                Directory.Move(stage, destination);
            }
            catch
            {
                if (
                    Directory.Exists(backup)
                    && !Directory.Exists(destination)
                )
                {
                    Directory.Move(backup, destination);
                }

                throw;
            }

            return new(
                destination,
                media.Count,
                count,
                warnings
            );
        }
        finally
        {
            if (Directory.Exists(stage))
            {
                Directory.Delete(stage, true);
            }
        }
    }

    private void WriteMedia(Playback plan, string output)
    {
        MediaContext media = plan.Media;
        DuneFile definition = new DuneFile()
            .Screen("background" + Extension)
            .Icon(
                "icon.aai",
                template
                    .Node(
                        media.IsSeries
                            ? "tv/iconview/icon"
                            : "movie/iconview/icon"
                    )
                    .Attr("scale", "1")
            );
        if (!media.IsSeries)
        {
            string url = plan.MovieUrls[0];
            if (plan.MovieUrls.Count > 1)
            {
                File.WriteAllText(
                    Path.Combine(output, "playlist.m3u"),
                    "#EXTM3U\n"
                        + string.Join("\n", plan.MovieUrls)
                        + "\n",
                    new UTF8Encoding(false)
                );
                url = "playlist.m3u";
            }

            definition
                .Set("paint_icons", "no")
                .Item(
                    0,
                    options.Language == "fr" ? "Lire" : "Play",
                    url,
                    play: true
                )
                .Save(output);
            return;
        }

        XElement seasons = template.Node(
            "tv/synopsis/season/seasonicon"
        );
        XElement area = seasons.Element("area")!;
        definition.Grid(
            area,
            IntElement(area, "columns", 5),
            IntElement(area, "rows", 2)
        );
        var index = 0;
        foreach (
            IGrouping<
                string,
                XElement
            > group in media.Episodes.GroupBy(
                MediaContext.ViewingSeason
            )
        )
        {
            string season = group.Key;
            string directory = Path.Combine(output, season);
            definition.Item(
                index++,
                season == "00"
                    ? (
                        options.Language == "fr"
                            ? "Spéciaux"
                            : "Specials"
                    )
                    : (
                        options.Language == "fr"
                            ? "Saison "
                            : "Season "
                    ) + season.TrimStart('0'),
                season,
                season + "/icon.aai",
                scale: seasons.Element("icon").Attr("scale", "1")
            );
            XElement episodes = template.Node(
                "tv/synopsis/episode/episodeicon"
            );
            XElement episodeArea = episodes.Element("area")!;
            DuneFile seasonFile = new DuneFile()
                .Screen("../background2" + Extension)
                .Icon(
                    "icon.aai",
                    seasons.Element("icon").Attr("scale", "1")
                )
                .Grid(episodeArea, 1, 1);
            var episodeIndex = 0;
            foreach (XElement episode in group)
            {
                string folder = MediaContext.EpisodeFolder(
                    episode
                );
                seasonFile.Item(
                    episodeIndex++,
                    SafeCaption(episode.ValueAt("title")),
                    plan.Episodes[episode],
                    folder + "/icon.aai",
                    play: true
                );
                new DuneFile()
                    .Icon("icon.aai")
                    .Set("media_url", plan.Episodes[episode])
                    .Set("media_action", "play")
                    .Save(Path.Combine(directory, folder));
            }

            seasonFile.Save(directory);
        }

        definition.Save(output);
    }

    private static int IntElement(
        XElement node,
        string field,
        int fallback
    )
    {
        return
            int.TryParse(
                node.ValueAt(field),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var number
            )
            && number > 0
            && number <= 100
            ? number
            : fallback;
    }

    private void Listing(
        TemplateRenderer renderer,
        IReadOnlyList<MediaContext> cards,
        string stage,
        string relative,
        bool series
    )
    {
        string output = Path.Combine(
            stage,
            relative.Replace('/', Path.DirectorySeparatorChar)
        );
        string kind = series ? "tv" : "movie";
        renderer.RenderMenu(output, kind);
        XElement view = template.Node(kind + "/iconview");
        XElement area = view.Element("area")!;
        DuneFile file = new DuneFile()
            .Screen("background" + Extension)
            .Grid(
                area,
                IntElement(area, "columns", 5),
                IntElement(area, "rows", 3)
            );
        var index = 0;
        foreach (
            MediaContext card in cards.OrderBy(
                mediaValue =>
                    mediaValue.Info.ValueAt("sort_title")
                        is { Length: > 0 } sort
                        ? sort
                        : mediaValue.Title,
                StringComparer.InvariantCultureIgnoreCase
            )
        )
        {
            string to = Path.GetRelativePath(
                    output,
                    Path.Combine(
                        stage,
                        Relative(card)
                            .Replace(
                                '/',
                                Path.DirectorySeparatorChar
                            )
                    )
                )
                .Replace('\\', '/');
            file.Item(
                index++,
                SafeCaption(card.Title),
                to,
                to + "/icon.aai",
                scale: view.Element("icon").Attr("scale", "1")
            );
        }

        file.Save(output);
    }

    private void Navigation(
        TemplateRenderer renderer,
        string stage,
        string relative,
        IReadOnlyList<(string Label, string Target)> entries,
        XElement? panel = null
    )
    {
        string output = Path.Combine(
            stage,
            relative.Replace('/', Path.DirectorySeparatorChar)
        );
        renderer.RenderMenu(output, "menu", panel);
        XElement area = panel ?? template.Node("menu/main/area");
        int columns = panel is null
            ? Math.Max(1, entries.Count)
            : IntElement(panel, "columns", 3);
        int rows = panel is null
            ? 1
            : IntElement(panel, "rows", 8);
        int width = Math.Max(1, area.Int("width") / columns);
        int height = Math.Max(1, area.Int("height") / rows);
        DuneFile file = new DuneFile()
            .Screen("background" + Extension)
            .Grid(area, columns, rows);
        for (var i = 0; i < entries.Count; i++)
        {
            string icon = $"nav-{i}.aai";
            string selected = $"nav-{i}-sel.aai";
            renderer.MenuLabel(
                Path.Combine(output, icon),
                entries[i].Label,
                width,
                height,
                panel is not null,
                false
            );
            renderer.MenuLabel(
                Path.Combine(output, selected),
                entries[i].Label,
                width,
                height,
                panel is not null,
                true
            );
            string target = Path.GetRelativePath(
                    output,
                    Path.Combine(
                        stage,
                        entries[i]
                            .Target.Replace(
                                '/',
                                Path.DirectorySeparatorChar
                            )
                    )
                )
                .Replace('\\', '/');
            file.Item(
                i,
                SafeCaption(entries[i].Label),
                target,
                icon,
                selected
            );
        }

        file.Save(output);
    }

    private void WriteIndexes(
        TemplateRenderer renderer,
        IReadOnlyList<MediaContext> media,
        string stage,
        CancellationToken cancellationToken
    )
    {
        bool isFrench = options.Language == "fr";
        var top = new List<(string, string)>();
        if (media.Any(mediaValue => !mediaValue.IsSeries))
        {
            Listing(
                renderer,
                media
                    .Where(mediaValue => !mediaValue.IsSeries)
                    .ToArray(),
                stage,
                "00",
                false
            );
            top.Add((isFrench ? "Films" : "Movies", "00"));
        }

        if (media.Any(mediaValue => mediaValue.IsSeries))
        {
            Listing(
                renderer,
                media
                    .Where(mediaValue => mediaValue.IsSeries)
                    .ToArray(),
                stage,
                "01",
                true
            );
            top.Add((isFrench ? "Séries" : "Series", "01"));
        }

        (string, string)[] indexes = new[]
        {
            ("genres", isFrench ? "Genres" : "Genres"),
            ("years", isFrench ? "Années" : "Years"),
            ("ratings", isFrench ? "Notes" : "Ratings"),
            ("alphabet", "A–Z"),
        };
        foreach (var (kind, label) in indexes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            XElement? panel = template.Root.XPath(
                "menu/index/" + kind + "/area"
            );
            if (panel is null)
            {
                continue;
            }

            IEnumerable<(string Key, MediaContext Card)> Group(
                MediaContext card
            )
            {
                if (kind == "genres")
                {
                    return card
                        .Info.Descendants("genre")
                        .Select(groupValue =>
                            (groupValue.Value.Trim(), card)
                        )
                        .Where(item => item.Item1.Length > 0);
                }

                string value = kind switch
                {
                    "years" => card.Info.ValueAt("year")
                        is { Length: >= 4 } year
                        ? year[..4]
                        : "?",
                    "ratings" => double.TryParse(
                        card.Info.ValueAt("rating")
                            .Replace(',', '.'),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var number
                    ) && number is >= 0 and <= 10
                        ? ((int)number).ToString(
                            CultureInfo.InvariantCulture
                        ) + "/10"
                        : "?",
                    _ => Alphabet(card.Title),
                };
                return [(value, card)];
            }

            var groups = media
                .SelectMany(Group)
                .GroupBy(
                    item => item.Key,
                    StringComparer.InvariantCultureIgnoreCase
                )
                .OrderBy(
                    groupValue => groupValue.Key,
                    StringComparer.InvariantCultureIgnoreCase
                );
            var nav = new List<(string, string)>();
            foreach (
                IGrouping<
                    string,
                    (string Key, MediaContext Card)
                > group in groups
            )
            {
                string hash = Convert
                    .ToHexString(
                        SHA256
                            .HashData(
                                Encoding.UTF8.GetBytes(
                                    group
                                        .Key.Normalize()
                                        .ToUpperInvariant()
                                )
                            )
                            .AsSpan(0, 8)
                    )
                    .ToLowerInvariant();
                string relative = kind + "/" + hash;
                var types = new List<(string, string)>();
                foreach (bool series in new[] { false, true })
                {
                    var cards = group
                        .Select(item => item.Card)
                        .Where(mediaValue =>
                            mediaValue.IsSeries == series
                        )
                        .DistinctBy(mediaValue => mediaValue.Id)
                        .ToArray();
                    if (cards.Length == 0)
                    {
                        continue;
                    }

                    string listing =
                        relative + (series ? "/01" : "/00");
                    Listing(
                        renderer,
                        cards,
                        stage,
                        listing,
                        series
                    );
                    types.Add(
                        (
                            series
                                ? (
                                    isFrench
                                        ? "Séries"
                                        : "Series"
                                )
                                : (
                                    isFrench ? "Films" : "Movies"
                                ),
                            listing
                        )
                    );
                }

                if (types.Count == 1)
                {
                    nav.Add((group.Key, types[0].Item2));
                }
                else
                {
                    Navigation(renderer, stage, relative, types);
                    nav.Add((group.Key, relative));
                }
            }

            if (nav.Count == 0)
            {
                continue;
            }

            Navigation(renderer, stage, kind, nav, panel);
            top.Add((label, kind));
        }

        Navigation(renderer, stage, "", top);
    }

    private static string Alphabet(string title)
    {
        var plain = new string(
            title
                .Normalize(NormalizationForm.FormD)
                .Where(character =>
                    CharUnicodeInfo.GetUnicodeCategory(character)
                    != UnicodeCategory.NonSpacingMark
                )
                .ToArray()
        ).TrimStart();
        if (plain.Length == 0)
        {
            return "#";
        }

        char character = char.ToUpperInvariant(plain[0]);
        return char.IsLetter(character) ? character.ToString()
            : char.IsDigit(character) ? "0–9"
            : "#";
    }

    /// <summary>
    /// Updates a card's render cache and copies its generated
    /// preview.
    /// </summary>
    public static ProcessingResult Process(
        MediaContext media,
        DuneTemplate template,
        RenderOptions options,
        string workspace,
        string? episodeKey = null,
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null
    )
    {
        string parent = Path.Combine(
            workspace,
            "interface-yaDIS3"
        );
        Directory.CreateDirectory(parent);
        string stage = Path.Combine(
            parent,
            ".stage-" + Guid.NewGuid().ToString("N")
        );
        string target = Path.Combine(parent, media.Id);
        bool RenderCacheOwned(string path)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(
                    File.ReadAllText(
                        Path.Combine(path, "render.json")
                    )
                );
                return document.RootElement.TryGetProperty(
                        "Producer",
                        out var name
                    )
                    && name.GetString() == "yaDIS3.Modern"
                    && (
                        File.GetAttributes(path)
                        & FileAttributes.ReparsePoint
                    ) == 0;
            }
            catch (Exception exception)
                when (exception
                        is IOException
                            or JsonException
                            or UnauthorizedAccessException
                )
            {
                return false;
            }
        }

        if (
            Directory.Exists(target) && !RenderCacheOwned(target)
        )
        {
            throw new IOException(
                "Le dossier de cache existe mais n'est pas un rendu reconnu de cette application."
            );
        }

        using var processingLock = new FileStream(
            Path.Combine(parent, media.Id + ".lock"),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None
        );
        try
        {
            using var renderer = new TemplateRenderer(
                template,
                options
            );
            RenderedCard rendered = renderer.Render(
                media,
                stage,
                cancellationToken,
                progress
            );
            File.WriteAllText(
                Path.Combine(stage, "render.json"),
                JsonSerializer.Serialize(
                    new
                    {
                        Producer = "yaDIS3.Modern",
                        Version = 1,
                        MediaId = media.Id,
                        Template = template.Name,
                    }
                )
            );
            cancellationToken.ThrowIfCancellationRequested();
            string backup = target + ".previous";
            if (Directory.Exists(target))
            {
                if (!RenderCacheOwned(target))
                {
                    throw new IOException(
                        "Le cache a changé pendant le rendu."
                    );
                }

                if (Directory.Exists(backup))
                {
                    if (RenderCacheOwned(backup))
                    {
                        Directory.Delete(backup, true);
                    }
                    else
                    {
                        backup +=
                            "-" + Guid.NewGuid().ToString("N");
                    }
                }

                Directory.Move(target, backup);
            }

            try
            {
                Directory.Move(stage, target);
            }
            catch
            {
                if (
                    Directory.Exists(backup)
                    && !Directory.Exists(target)
                )
                {
                    Directory.Move(backup, target);
                }

                throw;
            }

            string preview = Path.Combine(
                media.DataDirectory,
                "preview.jpg"
            );
            string temporary =
                preview
                + "."
                + Guid.NewGuid().ToString("N")
                + ".tmp";
            try
            {
                File.Copy(
                    Path.Combine(target, "preview.jpg"),
                    temporary
                );
                File.Move(temporary, preview, true);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }

            string? selected =
                episodeKey is not null
                && rendered.EpisodePreviews.TryGetValue(
                    episodeKey,
                    out var path
                )
                    ? Path.Combine(
                        target,
                        Path.GetRelativePath(stage, path)
                    )
                    : null;
            return new(preview, selected);
        }
        finally
        {
            if (Directory.Exists(stage))
            {
                Directory.Delete(stage, true);
            }
        }
    }
}
