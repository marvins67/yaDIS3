using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

/// <summary>
/// Reprend le mode basique de ProcessForm : interface auprès des
/// fichiers vidéo.
/// </summary>
internal sealed class LegacyBasicDuneWriter(
    DuneTemplate template,
    RenderOptions options,
    IExportFileSystem? fileSystem = null
)
{
    private readonly IExportFileSystem _files =
        fileSystem ?? new ExportFileSystem();

    public void Write(
        MediaContext media,
        string rendered,
        CancellationToken cancellationToken
    )
    {
        string parent = media.Header.ValueAt("path");
        string source = Path.Combine(
            parent,
            media.Header.ValueAt("filename")
        );
        if (
            !_files.DirectoryExists(source)
            || media.Header.ValueAt("folder") != "yes"
        )
        {
            throw new InvalidDataException(
                "Le mode basique nécessite un dossier par film ou série : "
                    + source
            );
        }
        string target = media.IsSeries
            ? Path.Combine(source, "_interface")
            : source;
        CopyTree(rendered, target, cancellationToken);
        if (media.IsSeries)
        {
            WriteSeries(media, source, target);
        }
        else
        {
            WriteMovie(media, source);
        }
        WriteParent(parent, media.IsSeries);
    }

    private void WriteParent(string parent, bool series)
    {
        using TemplateRenderer renderer = new(template, options);
        renderer.RenderBasicBackground(
            parent,
            series ? "tv" : "movie"
        );
        LegacyDuneDefinitions definitions = new(template);
        // Le chemin JPEG des séries est une particularité du mode basique de yaDIS2, même en AAI.
        string content = series
            ? definitions
                .SeriesListing()
                .Replace(
                    "background.aai",
                    "background.jpg",
                    StringComparison.Ordinal
                )
            : definitions
                .MovieListing()
                .Replace(
                    "../00/background.aai",
                    "background.aai",
                    StringComparison.Ordinal
                );
        Save(parent, content);
    }

    private void WriteMovie(MediaContext media, string source)
    {
        string[] urls = MovieFiles(media, source);
        if (urls.Length == 0)
        {
            throw new InvalidDataException(
                "Aucune vidéo dans le dossier : " + source
            );
        }
        new LegacyDuneMediaWriter(
            template,
            options,
            _files
        ).Write(
            media,
            source,
            urls,
            new Dictionary<XElement, string>()
        );
        string content = _files
            .ReadText(Path.Combine(source, "dune_folder.txt"))
            .Replace(
                "system_files=*.aai,*.jpg",
                "system_files=*",
                StringComparison.Ordinal
            );
        _files.WriteBytes(
            Path.Combine(source, "dune_folder.txt"),
            System
                .Text.Encoding.UTF8.GetPreamble()
                .Concat(
                    System.Text.Encoding.UTF8.GetBytes(content)
                )
                .ToArray()
        );
    }

    private string[] MovieFiles(
        MediaContext media,
        string source
    )
    {
        if (
            _files.DirectoryExists(Path.Combine(source, "BDMV"))
            || _files.DirectoryExists(
                Path.Combine(source, "VIDEO_TS")
            )
        )
        {
            return ["../" + media.Header.ValueAt("filename")];
        }
        return _files
            .EnumerateFiles(source)
            .Where(LegacyVideoFiles.IsVideo)
            .Order(StringComparer.CurrentCulture)
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToArray();
    }

    private void WriteSeries(
        MediaContext media,
        string source,
        string target
    )
    {
        Dictionary<XElement, string> episodes =
            media.Episodes.ToDictionary(
                episode => episode,
                episode =>
                    "../../.."
                    + episode
                        .ValueAt("file")
                        .Replace(
                            source,
                            "",
                            StringComparison.Ordinal
                        )
                        .Replace('\\', '/')
            );
        new LegacyDuneMediaWriter(
            template,
            options,
            _files
        ).Write(media, target, [], episodes);
        Save(
            source,
            "icon_path=_interface/icon.aai|icon_sel_path=_interface/icon.aai|icon_valign=center|icon_scale_factor="
                + template.Node("tv/iconview/icon").Attr("scale")
                + "|icon_sel_scale_factor=1|media_action=browse|media_url=_interface"
        );
    }

    private void Save(string directory, string content) =>
        _files.WriteBytes(
            Path.Combine(directory, "dune_folder.txt"),
            LegacyDuneEncoding.Encode(
                content,
                options.AaiBackgrounds
            )
        );

    private void CopyTree(
        string source,
        string target,
        CancellationToken cancellationToken
    )
    {
        _files.CreateDirectory(target);
        foreach (string path in _files.EnumerateFiles(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string name = Path.GetFileName(path);
            if (name is not ("preview.jpg" or "render.json"))
            {
                _files.CopyFile(
                    path,
                    Path.Combine(target, name),
                    true
                );
            }
        }
        foreach (
            string directory in _files.EnumerateDirectories(
                source
            )
        )
        {
            CopyTree(
                directory,
                Path.Combine(
                    target,
                    Path.GetFileName(directory)
                ),
                cancellationToken
            );
        }
    }
}
