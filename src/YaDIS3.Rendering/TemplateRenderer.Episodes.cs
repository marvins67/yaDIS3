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
                XElement episodeSpec = _template.Node(
                    "tv/synopsis/episode/episodeicon"
                );
                using SKBitmap overlay = LoadOrRenderEpisodeIcon(
                    media, episode, framed, episodeSpec, output
                );
                using SKBitmap full = episodeBackground.Copy();
                using (var canvas = new SKCanvas(full))
                {
                    DrawImage(
                        canvas,
                        overlay,
                        ReadRectangle(episodeSpec.Element("area"))
                    );
                }

                string preview = Path.Combine(output, "preview.jpg");
                SaveJpeg(full, preview, _options.JpegQuality);
                episodePreviews[key] = preview;
            }
        }
    }

    private SKBitmap LoadOrRenderEpisodeIcon(
        MediaContext media,
        XElement episode,
        SKBitmap framed,
        XElement specification,
        string output
    )
    {
        string icon = Path.Combine(output, "icon.aai");
        if (File.Exists(icon))
        {
            try
            {
                return AaiCodec.Load(icon);
            }
            catch (Exception exception)
                when (exception is InvalidDataException or EndOfStreamException)
            {
                // Une icône incomplète du cache doit être redessinée.
            }
        }

        Directory.CreateDirectory(output);
        XElement? area = specification.Element("area");
        using SKBitmap thumbnail = CreateEpisodeThumbnail(media, episode);
        SKBitmap overlay = CreateBitmap(area.Int("width"), area.Int("height"));
        try
        {
            using (var canvas = new SKCanvas(overlay))
            {
                DrawScene(canvas, specification, media, episode, framed, thumbnail);
            }
            AaiCodec.Save(overlay, icon);
            return overlay;
        }
        catch
        {
            overlay.Dispose();
            throw;
        }
    }
}
