using System.Xml.Linq;
using SkiaSharp;

namespace YaDIS3.Rendering;

public sealed partial class TemplateRenderer
{
    private void RenderSeriesOverview(
        MediaContext media,
        SKBitmap framed,
        SKBitmap common,
        string directory,
        string preview
    )
    {
        using (SKBitmap season = common.Copy())
        {
            using var canvas = new SKCanvas(season);
            DrawScene(
                canvas,
                _template.Node("tv/synopsis/season"),
                media,
                null,
                framed,
                null
            );
            SaveBackground(
                season,
                Path.Combine(directory, "background")
            );
            SaveJpeg(season, preview, _options.JpegQuality);
        }
    }

    private void DrawEpisodeBackground(
        MediaContext media,
        SKBitmap framed,
        SKBitmap episodeBackground
    )
    {
        using (var canvas = new SKCanvas(episodeBackground))
        {
            DrawScene(
                canvas,
                _template.Node("tv/synopsis/episode"),
                media,
                null,
                framed,
                null
            );
        }
    }

    private void RenderSeriesEpisodes(
        MediaContext media,
        SKBitmap framed,
        SKBitmap episodeBackground,
        string directory,
        Dictionary<string, string> episodePreviews,
        IProgress<string>? progress
    )
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (
            IGrouping<
                string,
                XElement
            > group in media.Episodes.GroupBy(
                MediaContext.ViewingSeason
            )
        )
        {
            string seasonDirectory = Path.Combine(
                directory,
                group.Key
            );
            Directory.CreateDirectory(seasonDirectory);
            // ProcessForm ne dessine une icône que pour season > 0 ;
            // le dernier épisode numéroté écrase les précédents dans ce dossier.
            XElement? numberedEpisode = media
                .Info.Elements("episode")
                .LastOrDefault(episode =>
                    MediaContext.ViewingSeason(episode)
                        == group.Key
                    && MediaContext.Season(episode) > 0
                );
            if (numberedEpisode is not null)
            {
                XElement specification = _template.Node(
                    "tv/synopsis/season/seasonicon"
                );
                XElement? iconSpec = specification.Element(
                    "icon"
                );
                using SKBitmap seasonIcon = CreateBitmap(
                    iconSpec.Int("width"),
                    iconSpec.Int("height")
                );
                using (var canvas = new SKCanvas(seasonIcon))
                {
                    DrawImage(
                        canvas,
                        ReadImage(
                            _template.Asset(
                                iconSpec!.Value,
                                true
                            ),
                            true
                        ),
                        SKRect.Create(
                            seasonIcon.Width,
                            seasonIcon.Height
                        )
                    );
                    var text = new XElement(
                        specification.Element("text")!
                    );
                    text.SetAttributeValue("x", 0);
                    text.SetAttributeValue(
                        "y",
                        text.Int("offset")
                    );
                    text.SetAttributeValue(
                        "width",
                        seasonIcon.Width
                    );
                    text.SetAttributeValue(
                        "height",
                        seasonIcon.Height
                    );
                    text.SetAttributeValue("align", "center");
                    text.SetAttributeValue("valign", "center");
                    DrawText(
                        canvas,
                        text,
                        numberedEpisode.Attr("season")
                    );
                }

                AaiCodec.Save(
                    seasonIcon,
                    Path.Combine(seasonDirectory, "icon.aai")
                );
            }
            foreach (XElement episode in group)
            {
                _cancellation.ThrowIfCancellationRequested();
                progress?.Report(
                    $"{media.Title} · S{MediaContext.Season(episode):00}E{MediaContext.Number(episode):00}"
                );
                string key =
                    group.Key
                    + "/"
                    + MediaContext.EpisodeFolder(episode);
                if (!seen.Add(key))
                {
                    throw new InvalidDataException(
                        "Deux épisodes ont la même clé de navigation Dune."
                    );
                }

                string output = Path.Combine(
                    seasonDirectory,
                    MediaContext.EpisodeFolder(episode)
                );
                string cachedPreview = Path.Combine(
                    output,
                    "preview.jpg"
                );
                if (
                    File.Exists(cachedPreview)
                    && File.Exists(
                        Path.Combine(output, "icon.aai")
                    )
                )
                {
                    episodePreviews[key] = cachedPreview;
                    continue;
                }
                Directory.CreateDirectory(output);
                XElement episodeSpec = _template.Node(
                    "tv/synopsis/episode/episodeicon"
                );
                XElement? area = episodeSpec.Element("area");
                using SKBitmap thumbnail =
                    CreateEpisodeThumbnail(media, episode);
                using SKBitmap overlay = CreateBitmap(
                    area.Int("width"),
                    area.Int("height")
                );
                using (var canvas = new SKCanvas(overlay))
                {
                    DrawScene(
                        canvas,
                        episodeSpec,
                        media,
                        episode,
                        framed,
                        thumbnail
                    );
                }

                AaiCodec.Save(
                    overlay,
                    Path.Combine(output, "icon.aai")
                );
                using SKBitmap full = episodeBackground.Copy();
                using (var canvas = new SKCanvas(full))
                {
                    DrawImage(
                        canvas,
                        overlay,
                        ReadRectangle(area)
                    );
                }

                string episodePreview = Path.Combine(
                    output,
                    "preview.jpg"
                );
                SaveJpeg(
                    full,
                    episodePreview,
                    _options.JpegQuality
                );
                episodePreviews[key] = episodePreview;
            }
        }
    }
}
