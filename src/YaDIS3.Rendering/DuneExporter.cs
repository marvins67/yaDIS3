using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

/// <summary>
/// Construit un catalogue autonome sans modifier les fichiers
/// vidéo.
/// </summary>
public sealed partial class DuneExporter(
    DuneTemplate template,
    RenderOptions options,
    IReadOnlyList<PathMapping> mappings,
    IExportFileSystem? fileSystem = null,
    LegacyCatalogOptions? catalogOptions = null,
    TimeProvider? timeProvider = null,
    ILogger<DuneExporter>? logger = null,
    LegacyLanguageCatalog? texts = null
)
{
    private readonly LegacyLanguageCatalog _texts =
        texts ?? LegacyLanguageCatalog.Load(options.Language);

    private readonly ILogger<DuneExporter> _logger =
        logger ?? NullLogger<DuneExporter>.Instance;
    private readonly IExportFileSystem _fileSystem =
        fileSystem ?? new ExportFileSystem();
    private readonly LegacyCatalogOptions _catalogOptions =
        catalogOptions ?? new();
    private readonly TimeProvider _timeProvider =
        timeProvider ?? TimeProvider.System;

    private static readonly JsonSerializerOptions s_manifestOptions =
        new() { WriteIndented = true };

    private const string Marker = "yadis3-export.json";

    private string Extension =>
        options.AaiBackgrounds ? ".aai" : ".jpg";

    private static string Relative(MediaContext media)
    {
        return LegacyExportPaths.Media(
            media.Header.ValueAt("filename"),
            media.IsSeries
        );
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
            if (!File.Exists(Path.Combine(directory, Marker)))
            {
                return LegacyCatalogOwned(directory);
            }

            ExportManifest? marker =
                JsonSerializer.Deserialize<ExportManifest>(
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

    private static bool LegacyCatalogOwned(string directory)
    {
        string path = Path.Combine(directory, "dune_folder.txt");
        if (!File.Exists(path))
        {
            return false;
        }

        string content = File.ReadAllText(
            path,
            System.Text.Encoding.UTF8
        );
        return content.StartsWith(
                LegacyDuneEncoding.Header,
                StringComparison.Ordinal
            )
            && content
                .Split('\n')
                .Any(line =>
                    line.TrimEnd('\r') == "storage_caption=yaDIS"
                );
    }

    private MediaPlaybackPlan Plan(
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
                    ".ogv",
                    ".img",
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
                .Order(StringComparer.CurrentCulture)
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
    /// Produit le catalogue Dune avec restauration de la
    /// destination en cas d'échec.
    /// </summary>
    public ExportResult Export(
        IReadOnlyList<MediaContext> media,
        string parentDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        if (options.BasicMode)
        {
            throw new InvalidOperationException(
                "En mode basique, utilisez Traiter pour écrire auprès des vidéos."
            );
        }
        if (media.Count == 0)
        {
            throw new InvalidOperationException(
                "La bibliothèque est vide."
            );
        }

        media = media
            .Where(card => card.Header.ValueAt("status") != "3")
            .ToArray();
        if (media.Count == 0)
        {
            throw new InvalidOperationException(
                "Aucune fiche traitée à exporter."
            );
        }

        var warnings = new List<string>();
        var plans = media
            .Select(mediaValue => Plan(mediaValue, warnings))
            .ToArray();
        if (
            media
                .Select(Relative)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != media.Count
        )
        {
            throw new InvalidDataException(
                "Deux fiches ont le même nom de dossier d'export."
            );
        }

        string destination = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(parentDirectory)
        );
        string parent =
            Path.GetDirectoryName(destination)
            ?? throw new InvalidDataException(
                "Choisissez un dossier d'export, pas la racine du volume."
            );
        if (
            Directory.Exists(destination)
            && (
                !Owned(destination)
                    && Directory
                        .EnumerateFileSystemEntries(destination)
                        .Any()
                || (
                    File.GetAttributes(destination)
                    & FileAttributes.ReparsePoint
                ) != 0
            )
        )
        {
            throw new IOException(
                "Le dossier de destination contient des fichiers qui ne sont pas un export reconnu de cette application."
            );
        }

        Directory.CreateDirectory(parent);
        using var exportLock = new FileStream(
            Path.Combine(
                parent,
                "."
                    + Path.GetFileName(destination)
                    + ".yadis3-export.lock"
            ),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None
        );
        string stage = Path.Combine(
            parent,
            "."
                + Path.GetFileName(destination)
                + ".yadis3-stage-"
                + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(stage);
        string backup = destination + ".previous";
        try
        {
            using var renderer = new TemplateRenderer(
                template,
                options
            );
            RenderExportMedia(
                renderer,
                plans,
                stage,
                progress,
                cancellationToken
            );
            WriteIndexes(
                renderer,
                plans.Select(item => item.Media).ToArray(),
                stage,
                cancellationToken
            );
            int count = plans.Sum(item => item.Episodes.Count);
            cancellationToken.ThrowIfCancellationRequested();
            CommitExportStage(stage, destination, backup);
            return new(
                destination,
                media.Count,
                count,
                warnings
            );
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            ExportFailed(destination, exception);
            throw;
        }
        finally
        {
            if (Directory.Exists(stage))
            {
                Directory.Delete(stage, true);
            }
        }
    }

    private void WriteMedia(
        MediaPlaybackPlan plan,
        string output
    )
    {
        new LegacyDuneMediaWriter(
            template,
            options,
            _fileSystem
        ).Write(
            plan.Media,
            output,
            plan.MovieUrls,
            plan.Episodes
        );
    }

    private void WriteIndexes(
        TemplateRenderer renderer,
        IReadOnlyList<MediaContext> media,
        string stage,
        CancellationToken cancellationToken
    )
    {
        new LegacyDuneCatalogWriter(
            template,
            options,
            _timeProvider,
            _fileSystem,
            _catalogOptions,
            _texts
        ).Write(renderer, media, stage, cancellationToken);
    }

    /// <summary>
    /// Actualise le cache de rendu d'une fiche et recopie son
    /// aperçu.
    /// </summary>
    public static ProcessingResult Process(
        MediaContext media,
        DuneTemplate template,
        RenderOptions options,
        string workspace,
        string? episodeKey = null,
        IProgress<string>? progress = null,
        bool useFirstEpisodePreview = false,
        IReadOnlySet<string>? reusableEpisodes = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default
    )
    {
        string parent = Path.Combine(
            workspace,
            "interface-yaDIS3"
        );
        Directory.CreateDirectory(parent);
        string target = Path.Combine(parent, media.Id);
        string stage = useFirstEpisodePreview
            ? target
            : Path.Combine(parent, ".stage-" + Guid.NewGuid().ToString("N"));

        using var processingLock = new FileStream(
            Path.Combine(parent, media.Id + ".lock"),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None
        );
        if (Directory.Exists(target) && !RenderCacheOwned(target))
        {
            throw new IOException(
                "Le dossier de cache existe mais n'est pas un rendu reconnu de cette application."
            );
        }

        string dataPreview = media.Artwork("preview.jpg");
        try
        {
            using var renderer = new TemplateRenderer(
                template,
                options,
                logger
            );
            RenderedCard rendered;
            if (useFirstEpisodePreview)
            {
                CheckRenderLinks(target);
                if (new FileInfo(dataPreview).LinkTarget is not null)
                {
                    throw new IOException($"L’aperçu est un lien : {dataPreview}");
                }
                bool reuse = CanReuseEpisodes(target, template, options, reusableEpisodes);
                if (!Directory.Exists(target))
                {
                    Directory.CreateDirectory(target);
                    WriteRenderManifest(target, media, template, options);
                }
                rendered = renderer.RenderTreatment(
                    media, target, dataPreview, reuse ? reusableEpisodes : null,
                    progress, cancellationToken);
            }
            else
            {
                ReuseEpisodes(target, stage, template, options, reusableEpisodes);
                rendered = renderer.Render(media, stage, progress, cancellationToken);
            }
            WriteRenderManifest(stage, media, template, options);
            cancellationToken.ThrowIfCancellationRequested();
            string preview = dataPreview;
            if (!useFirstEpisodePreview)
            {
                CommitRenderStage(stage, target);
                preview = CopyRenderedPreview(target, media);
            }
            if (options.BasicMode)
            {
                new LegacyBasicDuneWriter(
                    template,
                    options
                ).Write(media, target, cancellationToken);
            }
            new LegacyMovieIndexStore(template, options).Update(
                media,
                workspace,
                cancellationToken
            );
            if (!options.BasicMode)
            {
                var legacyExporter = new DuneExporter(
                    template,
                    options,
                    []
                );
                var warnings = new List<string>();
                MediaPlaybackPlan plan = legacyExporter.Plan(
                    media,
                    warnings
                );
                // Publier la définition avec le rendu, jamais avant ses images.
                legacyExporter.WriteMedia(plan, target);
                LegacyProcessedMediaPublisher.Publish(media, workspace, target);
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
            if (!useFirstEpisodePreview && Directory.Exists(stage))
            {
                Directory.Delete(stage, true);
            }
        }
    }

    private static void WriteRenderManifest(
        string directory, MediaContext media, DuneTemplate template, RenderOptions options
    )
    {
        string manifest = Path.Combine(directory, "render.json");
        string temporary = manifest + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new
            {
                Producer = "yaDIS3.Modern",
                Version = 1,
                MediaId = media.Id,
                Template = template.Name,
                TemplateDirectory = template.Directory,
                Options = options,
            }));
            File.Move(temporary, manifest, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static void CheckRenderLinks(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }
        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Le dossier de rendu contient un lien : {path}");
            }
            if ((attributes & FileAttributes.Directory) != 0)
            {
                CheckRenderLinks(path);
            }
        }
    }

    private static void ReuseEpisodes(
        string target,
        string stage,
        DuneTemplate template,
        RenderOptions options,
        IReadOnlySet<string>? keys
    )
    {
        if (keys is null || !CanReuseEpisodes(target, template, options, keys))
        {
            return;
        }
        foreach (string key in keys)
        {
            string old = Path.Combine(target, key);
            string next = Path.Combine(stage, key);
            string icon = Path.Combine(old, "icon.aai");
            if (File.Exists(icon))
            {
                Directory.CreateDirectory(next);
                File.Copy(icon, Path.Combine(next, "icon.aai"));
            }
        }
    }

    private static bool CanReuseEpisodes(
        string target, DuneTemplate template, RenderOptions options,
        IReadOnlySet<string>? keys
    )
    {
        if (
            keys is null
            || keys.Count == 0
            || !Directory.Exists(target)
        )
        {
            return false;
        }
        using JsonDocument manifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(target, "render.json"))
        );
        JsonElement root = manifest.RootElement;
        if (
            !root.TryGetProperty(
                "TemplateDirectory",
                out JsonElement folder
            )
            || folder.GetString() != template.Directory
            || !root.TryGetProperty(
                "Options",
                out JsonElement previous
            )
            || previous.Deserialize<RenderOptions>() != options
        )
        {
            return false;
        }
        return true;
    }

    private void RenderExportMedia(
        TemplateRenderer renderer,
        IReadOnlyList<MediaPlaybackPlan> plans,
        string stage,
        IProgress<string>? progress,
        CancellationToken cancellationToken
    )
    {
        foreach (MediaPlaybackPlan plan in plans)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(
                _texts["11"]
                    + " : "
                    + SafeCaption(plan.Media.Title)
            );
            string output = Path.Combine(
                stage,
                Relative(plan.Media)
                    .Replace('/', Path.DirectorySeparatorChar)
            );
            renderer.Render(
                plan.Media,
                output,
                progress,
                cancellationToken
            );
            WriteMedia(plan, output);
            // yaDIS2 garde les aperçus dans le workspace, pas dans l'export.
            foreach (
                string preview in Directory.EnumerateFiles(
                    output,
                    "preview.jpg",
                    SearchOption.AllDirectories
                )
            )
            {
                File.Delete(preview);
            }
        }
    }

    private static void CommitExportStage(
        string stage,
        string destination,
        string backup
    )
    {
        if (Directory.Exists(destination))
        {
            if (
                !Owned(destination)
                    && Directory
                        .EnumerateFileSystemEntries(destination)
                        .Any()
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
                    backup += "-" + Guid.NewGuid().ToString("N");
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
    }

    internal static bool RenderCacheOwned(string path)
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

    private static void CommitRenderStage(
        string stage,
        string target
    )
    {
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
                    backup += "-" + Guid.NewGuid().ToString("N");
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
    }

    private static string CopyRenderedPreview(
        string target,
        MediaContext media
    )
    {
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

        return preview;
    }
}
