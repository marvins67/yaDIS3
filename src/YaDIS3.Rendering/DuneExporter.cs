using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

/// <summary>
/// Builds a standalone catalog without modifying media
/// files.
/// </summary>
public sealed partial class DuneExporter(
    DuneTemplate template,
    RenderOptions options,
    IReadOnlyList<PathMapping> mappings,
    IExportFileSystem? fileSystem = null,
    LegacyCatalogOptions? catalogOptions = null,
    TimeProvider? timeProvider = null,
    ILogger<DuneExporter>? logger = null
)
{
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
    /// Renders and replaces the managed Dune catalog with
    /// rollback support.
    /// </summary>
    public ExportResult Export(
        IReadOnlyList<MediaContext> media,
        string parentDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
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

            WriteIndexes(
                renderer,
                plans.Select(item => item.Media).ToArray(),
                stage,
                cancellationToken
            );
            int count = plans.Sum(item => item.Episodes.Count);
            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(destination))
            {
                if (
                    !Owned(destination)
                        && Directory
                            .EnumerateFileSystemEntries(
                                destination
                            )
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

    private void WriteMedia(Playback plan, string output)
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
            _catalogOptions
        ).Write(renderer, media, stage, cancellationToken);
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
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
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
                progress,
                cancellationToken
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
