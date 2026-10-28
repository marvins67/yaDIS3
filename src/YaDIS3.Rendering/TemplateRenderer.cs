using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SkiaSharp;

namespace YaDIS3.Rendering;

/// <summary>
/// Configures template language, background format, and
/// movie cases.
/// </summary>
public sealed record RenderOptions(
    string Language = "fr",
    bool AaiBackgrounds = false,
    bool UseCases = false
);

/// <summary>
/// Identifies generated artwork and episode preview files.
/// </summary>
public sealed record RenderedCard(
    string Directory,
    string Preview,
    IReadOnlyDictionary<string, string> EpisodePreviews
);

/// <summary>
/// Renders Dune template layers, fonts, masks, and
/// metadata.
/// </summary>
public sealed class TemplateRenderer : IDisposable
{
    private readonly DuneTemplate _template;

    private readonly RenderOptions _options;

    private readonly Dictionary<string, SKTypeface> _fonts = new(
        StringComparer.OrdinalIgnoreCase
    );

    private readonly Dictionary<string, SKBitmap> _assets = new(
        StringComparer.Ordinal
    );

    private readonly Dictionary<string, SKBitmap> _mediaImages =
        new(StringComparer.Ordinal);

    private CancellationToken _cancellation;

    /// <summary>
    /// Initializes render options and loads the template fonts.
    /// </summary>
    public TemplateRenderer(
        DuneTemplate template,
        RenderOptions options
    )
    {
        _template = template;
        _options = options;
        foreach (string path in template.Fonts)
        {
            SKTypeface face = SKTypeface.FromFile(path);
            if (face is null)
            {
                continue;
            }

            string fileKey = FontKey(
                Path.GetFileNameWithoutExtension(path)
            );
            _fonts.TryAdd(fileKey, face);
            _fonts.TryAdd(FontKey(face.FamilyName), face);
        }
    }

    private static string FontKey(string name)
    {
        return Regex
            .Replace(name, "[^a-zA-Z0-9]", "")
            .ToLowerInvariant();
    }

    private static SKBitmap New(
        int width,
        int height,
        bool opaque = false
    )
    {
        AaiCodec.CheckSize(width, height);
        var image = new SKBitmap(width, height);
        using var canvas = new SKCanvas(image);
        canvas.Clear(
            opaque ? SKColors.Black : SKColors.Transparent
        );
        return image;
    }

    private SKBitmap? ReadImage(string? path, bool commonAsset)
    {
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        Dictionary<string, SKBitmap> cache = commonAsset
            ? _assets
            : _mediaImages;
        if (cache.TryGetValue(path, out var found))
        {
            return found;
        }

        using FileStream input = File.OpenRead(path);
        using SKCodec codec = SKCodec.Create(input);
        if (codec is null)
        {
            throw new InvalidDataException(
                $"Image illisible : {Path.GetFileName(path)}."
            );
        }

        AaiCodec.CheckSize(codec.Info.Width, codec.Info.Height);
        SKBitmap bitmap =
            SKBitmap.Decode(codec)
            ?? throw new InvalidDataException(
                "Décodage d'image impossible."
            );
        cache[path] = bitmap;
        return bitmap;
    }

    private void Image(
        SKCanvas canvas,
        SKBitmap? image,
        SKRect rectangle
    )
    {
        _cancellation.ThrowIfCancellationRequested();
        if (
            image is null
            || rectangle.Width <= 0
            || rectangle.Height <= 0
        )
        {
            return;
        }

        using SKImage source = SKImage.FromBitmap(image);
        canvas.DrawImage(
            source,
            rectangle,
            new SKSamplingOptions(
                SKFilterMode.Linear,
                SKMipmapMode.Linear
            )
        );
    }

    private static SKRect Rect(XElement? node)
    {
        return SKRect.Create(
            node.Float("x"),
            node.Float("y"),
            node.Float("width"),
            node.Float("height")
        );
    }

    private static string Kind(XElement node)
    {
        return node.Attr("type").ToLowerInvariant();
    }

    private SKBitmap Framed(MediaContext media)
    {
        XElement part = media.IsSeries
            ? _template.Root.XPath("tv/banner")
                ?? _template.Node("tv/poster")
            : _template.Node(
                _options.UseCases
                    ? "movie/poster/case"
                    : "movie/poster/standard"
            );
        XElement? size = part.Element("size");
        XElement? inner = part.Element("inner") ?? size;
        int width = inner.Int("width");
        int height = inner.Int("height");
        using SKBitmap content = New(width, height);
        using (var canvas = new SKCanvas(content))
        {
            Image(
                canvas,
                ReadImage(
                    media.Artwork(
                        media.IsSeries
                        && _template.Root.XPath("tv/banner")
                            is not null
                            ? "banner.jpg"
                            : "poster.jpg"
                    ),
                    false
                ),
                SKRect.Create(width, height)
            );
        }

        if (part.Element("mask") is { } maskNode)
        {
            ApplyMask(
                content,
                ReadImage(
                    _template.Asset(maskNode.Value, true),
                    true
                )!
            );
        }

        int border = size.Int("border");
        if (border < 0 || border > 256)
        {
            throw new InvalidDataException(
                "Bordure de template invalide."
            );
        }

        SKBitmap output = New(
            checked(size.Int("width") + 2 * border),
            checked(size.Int("height") + 2 * border)
        );
        using var draw = new SKCanvas(output);
        Image(
            draw,
            content,
            SKRect.Create(
                inner.Int("x") + border,
                inner.Int("y") + border,
                width,
                height
            )
        );
        string? frame = part.Element("frame")?.Value;
        if (!media.IsSeries && _options.UseCases)
        {
            frame =
                "case_"
                + media.Info.ValueAt("mediainfo/source")
                + ".png";
        }

        string? framePath = frame is null
            ? null
            : _template.Asset(frame);
        if (
            framePath is null
            && !media.IsSeries
            && _options.UseCases
        )
        {
            framePath = _template.Asset(
                "case_default.png",
                true
            );
        }

        Image(
            draw,
            ReadImage(framePath, true),
            SKRect.Create(
                border,
                border,
                size.Int("width"),
                size.Int("height")
            )
        );
        return output;
    }

    internal static void ApplyMask(SKBitmap image, SKBitmap mask)
    {
        using SKBitmap scaled = New(image.Width, image.Height);
        using (var canvas = new SKCanvas(scaled))
        {
            canvas.DrawBitmap(
                mask,
                SKRect.Create(image.Width, image.Height)
            );
        }

        // The old renderer takes min(alpha(image), alpha(mask)),
        // not multiplication.
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                SKColor color = image.GetPixel(x, y);
                byte alpha = Math.Min(
                    color.Alpha,
                    scaled.GetPixel(x, y).Alpha
                );
                if (alpha != color.Alpha)
                {
                    image.SetPixel(x, y, color.WithAlpha(alpha));
                }
            }
        }
    }

    private SKBitmap Thumbnail(
        MediaContext media,
        XElement episode
    )
    {
        XElement part = _template.Node("tv/episode");
        XElement? size = part.Element("size");
        SKBitmap image = New(
            size.Int("width"),
            size.Int("height")
        );
        using var canvas = new SKCanvas(image);
        Image(
            canvas,
            ReadImage(
                media.Artwork(
                    $"{MediaContext.Season(episode)}x{MediaContext.Number(episode)}.jpg"
                ),
                false
            ),
            SKRect.Create(image.Width, image.Height)
        );
        if (part.Element("mask") is { } mask)
        {
            ApplyMask(
                image,
                ReadImage(
                    _template.Asset(mask.Value, true),
                    true
                )!
            );
        }

        if (part.Element("frame") is { } frame)
        {
            Image(
                canvas,
                ReadImage(
                    _template.Asset(frame.Value, true),
                    true
                ),
                SKRect.Create(image.Width, image.Height)
            );
        }

        return image;
    }

    /// <summary>
    /// Generates card, season, and episode artwork from one
    /// template.
    /// </summary>
    public RenderedCard Render(
        MediaContext media,
        string directory,
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null
    )
    {
        _cancellation = cancellationToken;
        Directory.CreateDirectory(directory);
        try
        {
            using SKBitmap framed = Framed(media);
            XElement iconNode = _template.Node(
                media.IsSeries
                    ? "tv/iconview/icon"
                    : "movie/iconview/icon"
            );
            using (
                SKBitmap icon = New(
                    iconNode.Int("width"),
                    iconNode.Int("height")
                )
            )
            {
                using var canvas = new SKCanvas(icon);
                Image(
                    canvas,
                    framed,
                    SKRect.Create(icon.Width, icon.Height)
                );
                AaiCodec.Save(
                    icon,
                    Path.Combine(directory, "icon.aai")
                );
            }

            using SKBitmap common = New(1920, 1080, true);
            using (var canvas = new SKCanvas(common))
            {
                XElement parent = _template.Node(
                    media.IsSeries ? "tv" : "movie"
                );
                Image(
                    canvas,
                    ReadImage(
                        _template.Asset(
                            parent.Attr("background"),
                            true
                        ),
                        true
                    ),
                    SKRect.Create(1920, 1080)
                );
                DrawScene(
                    canvas,
                    _template.Node(
                        media.IsSeries
                            ? "tv/synopsis/common"
                            : "movie/synopsis"
                    ),
                    media,
                    null,
                    framed,
                    null
                );
            }

            string preview = Path.Combine(
                directory,
                "preview.jpg"
            );
            var episodePreviews = new Dictionary<string, string>(
                StringComparer.Ordinal
            );
            if (!media.IsSeries)
            {
                SaveBackground(
                    common,
                    Path.Combine(directory, "background")
                );
                SaveJpeg(common, preview);
                return new(directory, preview, episodePreviews);
            }

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
                SaveJpeg(season, preview);
            }

            using SKBitmap episodeBackground = common.Copy();
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

            SaveBackground(
                episodeBackground,
                Path.Combine(directory, "background2")
            );
            var seen = new HashSet<string>(
                StringComparer.Ordinal
            );
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
                XElement specification = _template.Node(
                    "tv/synopsis/season/seasonicon"
                );
                XElement? iconSpec = specification.Element(
                    "icon"
                );
                using SKBitmap seasonIcon = New(
                    iconSpec.Int("width"),
                    iconSpec.Int("height")
                );
                using (var canvas = new SKCanvas(seasonIcon))
                {
                    Image(
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
                        group.Key.TrimStart('0')
                            is { Length: > 0 } segment
                            ? segment
                            : "SP"
                    );
                }

                AaiCodec.Save(
                    seasonIcon,
                    Path.Combine(seasonDirectory, "icon.aai")
                );
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
                    Directory.CreateDirectory(output);
                    XElement episodeSpec = _template.Node(
                        "tv/synopsis/episode/episodeicon"
                    );
                    XElement? area = episodeSpec.Element("area");
                    using SKBitmap thumbnail = Thumbnail(
                        media,
                        episode
                    );
                    using SKBitmap overlay = New(
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
                    using SKBitmap full =
                        episodeBackground.Copy();
                    using (var canvas = new SKCanvas(full))
                    {
                        Image(canvas, overlay, Rect(area));
                    }

                    string episodePreview = Path.Combine(
                        output,
                        "preview.jpg"
                    );
                    SaveJpeg(full, episodePreview);
                    episodePreviews[key] = episodePreview;
                }
            }

            return new(directory, preview, episodePreviews);
        }
        finally
        {
            foreach (SKBitmap image in _mediaImages.Values)
            {
                image.Dispose();
            }

            _mediaImages.Clear();
        }
    }

    private void DrawScene(
        SKCanvas canvas,
        XElement scene,
        MediaContext media,
        XElement? episode,
        SKBitmap framed,
        SKBitmap? thumbnail
    )
    {
        foreach (XElement image in scene.Elements("image"))
        {
            string type = Kind(image);
            SKRect rectangleValue = Rect(image);
            if (
                type == "special"
                && (
                    episode is null
                    || MediaContext.Season(episode) != 0
                )
            )
            {
                continue;
            }

            if (type is "poster" or "banner")
            {
                Image(canvas, framed, rectangleValue);
                continue;
            }

            if (type == "thumbnail")
            {
                Image(canvas, thumbnail, rectangleValue);
                continue;
            }

            if (type is "country" or "countries")
            {
                var countries = media
                    .Info.Descendants("country")
                    .Select(item => item.Value)
                    .Take(image.Int("count", 99));
                var index = 0;
                foreach (string country in countries)
                {
                    SKRect shifted = rectangleValue;
                    if (image.Attr("direction") == "vertical")
                    {
                        shifted.Offset(
                            0,
                            index * image.Float("spacing")
                        );
                    }
                    else
                    {
                        shifted.Offset(
                            index * image.Float("spacing"),
                            0
                        );
                    }

                    Image(
                        canvas,
                        ReadImage(
                            _template.Asset(
                                "flag_" + country.Trim() + ".png"
                            ),
                            true
                        ),
                        shifted
                    );
                    index++;
                }

                continue;
            }

            XElement data = episode ?? media.Info;
            string? path = type switch
            {
                "fanart" => media.Artwork("fanart.jpg"),
                "static" or "special" => _template.Asset(
                    image.Value.Trim(),
                    true
                ),
                "vcodec" => Badge(
                    data.ValueAt("mediainfo/video/codec")
                ),
                "acodec" => Badge(
                    data.ValueAt("mediainfo/audio/codec")
                ),
                "format" => Badge(
                    data.ValueAt("mediainfo/format")
                ),
                "channels" => Badge(
                    data.ValueAt("mediainfo/audio/channels")
                ),
                "resolution" => Badge(
                    data.ValueAt("mediainfo/video/resolution")
                ),
                "aspect" => Badge(
                    data.ValueAt("mediainfo/video/aspectratio")
                        .Replace(":", "_")
                        .Replace("_1", "")
                ),
                "framerate" => Badge(
                    data.ValueAt("mediainfo/video/framerate")
                ),
                "frequency" => Badge(Frequency(data) + "hz"),
                "certification" => Badge(
                    (
                        _options.Language == "en"
                            ? "gb"
                            : _options.Language
                    )
                        + "_"
                        + media
                            .Info.ValueAt("certification")
                            .Trim('-')
                ),
                "rating" => double.TryParse(
                    data.ValueAt("rating").Replace(',', '.'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var rating
                )
                    ? Badge(
                        Math.Round(rating)
                            .ToString(
                                CultureInfo.InvariantCulture
                            )
                    )
                    : null,
                _ => null,
            };
            Image(
                canvas,
                ReadImage(path, type != "fanart"),
                rectangleValue
            );
        }

        foreach (XElement text in scene.Elements("text"))
        {
            if (
                text.Attr("language") is { Length: > 0 } language
                && language != _options.Language
            )
            {
                continue;
            }

            if (
                Kind(text) == "special"
                && (
                    episode is null
                    || MediaContext.Season(episode) != 0
                )
            )
            {
                continue;
            }

            DrawText(
                canvas,
                text,
                TextValue(text, media, episode)
            );
        }

        if (scene.Element("languages") is { } languages)
        {
            DrawLanguages(
                canvas,
                languages,
                episode ?? media.Info
            );
        }
    }

    private string? Badge(string name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? null
            : _template.Asset(name.ToLowerInvariant() + ".png");
    }

    private static string Frequency(XElement data)
    {
        string rate = data.ValueAt("mediainfo/video/framerate");
        if (
            data.ValueAt("mediainfo/video/resolution")
                .EndsWith('i')
            && double.TryParse(
                rate.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number
            )
            && number is >= 25 and <= 30
        )
        {
            return (number * 2).ToString(
                "0.##",
                CultureInfo.InvariantCulture
            );
        }

        return rate;
    }

    private string TextValue(
        XElement specification,
        MediaContext media,
        XElement? episode
    )
    {
        string type = Kind(specification);
        XElement data = episode ?? media.Info;
        string Join(
            string singular,
            string field,
            string defaultSeparator
        )
        {
            var elements =
                media
                    .Info.XPath(field)
                    ?.Elements(singular)
                    .Select(element =>
                        element.Element("name")?.Value
                        ?? element.Value
                    )
                    .ToArray()
                ?? [];
            if (elements.Length == 0)
            {
                elements = data.ValueAt(singular)
                    .Split(
                        '|',
                        StringSplitOptions.RemoveEmptyEntries
                            | StringSplitOptions.TrimEntries
                    );
            }

            return string.Join(
                specification.Attr("direction") == "vertical"
                    ? "\n"
                    : specification.Attr(
                        "separator",
                        defaultSeparator
                    ),
                elements
            );
        }

        switch (type)
        {
            case "static":
            case "special":
                return specification.Value;
            case "title":
                string title =
                    episode?.ValueAt("title") ?? media.Title;
                if (
                    episode is not null
                    && specification.Attr("episodenumber")
                        == "yes"
                )
                {
                    title =
                        MediaContext.Number(episode)
                        + specification.Attr("separator", ". ")
                        + title;
                }

                if (
                    episode is not null
                    && specification.Attr("seasonnumber")
                        == "yes"
                )
                {
                    title =
                        MediaContext.Season(episode)
                        + specification.Attr("separator", ". ")
                        + title;
                }

                return title;
            case "originaltitle":
                string original = media.Info.ValueAt(
                    "original_title"
                );
                return
                    specification.Attr("permanent", "yes")
                        == "no"
                    && original == media.Title
                    ? ""
                    : original;
            case "genres":
                return Join("genre", "genres", " / ");
            case "cast":
                return Join("actor", "actors", ", ");
            case "director":
                return episode is null
                    ? Join("director", "directors", ", ")
                    : string.Join(
                        ", ",
                        data.ValueAt("director")
                            .Split(
                                '|',
                                StringSplitOptions.RemoveEmptyEntries
                            )
                    );
            case "studios":
                return Join("studio", "studios", ", ");
            case "runtime":
                string value = episode is null
                    ? media.Info.ValueAt("runtime")
                    : data.ValueAt("mediainfo/duration");
                if (
                    !double.TryParse(
                        value.Replace(',', '.'),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var duration
                    )
                    || duration <= 0
                    || duration > 1e10
                )
                {
                    return "";
                }

                return FormatRuntime(
                    duration,
                    episode is not null
                );
            case "season":
                return episode is null
                    ? ""
                    : MediaContext
                        .Season(episode)
                        .ToString(CultureInfo.InvariantCulture);
            case "aired":
                return
                    _options.Language == "fr"
                    && DateOnly.TryParseExact(
                        data.ValueAt("aired"),
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var aired
                    )
                    ? aired.ToString(
                        "dd-MM-yyyy",
                        CultureInfo.InvariantCulture
                    )
                    : data.ValueAt("aired");
            case "resolution":
                string resolution = data.ValueAt(
                    "mediainfo/video/resolution"
                );
                return
                    specification.Attr("scantype", "yes") == "no"
                    ? resolution.TrimEnd('p', 'i')
                    : resolution;
            case "channels":
                return data.ValueAt("mediainfo/audio/channels");
            case "aspect":
                return data.ValueAt(
                    "mediainfo/video/aspectratio"
                );
            case "framerate":
                return data.ValueAt("mediainfo/video/framerate");
            case "frequency":
                return Frequency(data);
            default:
                return data.ValueAt(type);
        }
    }

    // Preserve the original episode rule: round up only when
    // whole seconds exceed 30.
    // Do not round or rewrite the stored MediaInfo duration.

    internal static string FormatRuntime(
        double duration,
        bool episode
    )
    {
        if (
            !double.IsFinite(duration)
            || duration <= 0
            || duration > 1e10
        )
        {
            return "";
        }

        TimeSpan span = episode
            ? TimeSpan.FromMilliseconds(duration)
            : TimeSpan.FromMinutes(duration);
        if (episode && span.Seconds > 30)
        {
            span = span.Add(TimeSpan.FromMinutes(1));
        }

        if (span.TotalMinutes < 1)
        {
            return "";
        }

        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}h{span.Minutes:00}"
            : $"{span.Minutes}mn";
    }

    private void DrawLanguages(
        SKCanvas canvas,
        XElement specification,
        XElement data
    )
    {
        XElement? back = specification.Element("backimage");
        if (back is null)
        {
            return;
        }

        SKBitmap? image = ReadImage(
            _template.Asset(back.Value, true),
            true
        );
        foreach (string field in new[] { "audio", "subtitles" })
        {
            XElement? position = specification.Element(field);
            if (position is null)
            {
                continue;
            }

            var values = data.ValueAt(
                    "mediainfo/" + field + "/languages"
                )
                .Split(
                    ['|', ',', ';'],
                    StringSplitOptions.RemoveEmptyEntries
                        | StringSplitOptions.TrimEntries
                )
                .Select(segment =>
                    segment.Length > 2 ? segment[..2] : segment
                )
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(position.Int("count", 3));
            var index = 0;
            foreach (string language in values)
            {
                int x =
                    position.Int("x")
                    + (
                        position.Attr("direction") == "vertical"
                            ? 0
                            : index * back.Int("spacing")
                    );
                int y =
                    position.Int("y")
                    + (
                        position.Attr("direction") == "vertical"
                            ? index * back.Int("spacing")
                            : 0
                    );
                Image(
                    canvas,
                    image,
                    SKRect.Create(
                        x,
                        y,
                        back.Int("width"),
                        back.Int("height")
                    )
                );
                var text = new XElement(
                    specification.Element("text")!
                );
                text.SetAttributeValue("x", x);
                text.SetAttributeValue(
                    "y",
                    y + text.Int("offset")
                );
                text.SetAttributeValue(
                    "width",
                    back.Int("width")
                );
                text.SetAttributeValue(
                    "height",
                    back.Int("height")
                );
                text.SetAttributeValue("align", "center");
                text.SetAttributeValue("valign", "center");
                DrawText(canvas, text, language);
                index++;
            }
        }
    }

    internal void DrawText(
        SKCanvas canvas,
        XElement specification,
        string text
    )
    {
        _cancellation.ThrowIfCancellationRequested();
        if (text.Length == 0)
        {
            return;
        }

        text = specification.Attr("case") switch
        {
            "upper" => text.ToUpperInvariant(),
            "lower" => text.ToLowerInvariant(),
            _ => text,
        };
        SKRect rectangleValue = Rect(specification);
        if (
            rectangleValue.Width <= 0
            || rectangleValue.Height <= 0
        )
        {
            return;
        }

        string family = specification.Attr("font");
        _fonts.TryGetValue(FontKey(family), out var face);
        using var font = new SKFont(
            face ?? SKTypeface.Default,
            specification.Float("size", 24) * 96 / 72
        )
        {
            Edging = SKFontEdging.Antialias,

            Subpixel = true,

            LinearMetrics = true,

            Embolden =
                specification
                    .Attr("style")
                    .Contains(
                        "bold",
                        StringComparison.OrdinalIgnoreCase
                    )
                && (face?.FontStyle.Weight ?? 400) < 700,

            SkewX =
                specification
                    .Attr("style")
                    .Contains(
                        "italic",
                        StringComparison.OrdinalIgnoreCase
                    )
                && face?.IsItalic != true
                    ? -0.25f
                    : 0,
        };
        using var paint = new SKPaint
        {
            IsAntialias = true,

            Color = SKColor.TryParse(
                specification.Attr("color", "#ffffff"),
                out var color
            )
                ? color
                : SKColors.White,
        };
        // Graphics.DrawString's default layout reserves an em/6
        // overhang margin.
        // At both sides. Use it for alignment and wrapping as
        // well as drawing.
        float inset = font.Size / 6;
        float layoutWidth = Math.Max(
            1,
            rectangleValue.Width - 2 * inset
        );
        SKFontMetrics metrics = font.Metrics;
        float step = specification.Float(
            "spacing",
            font.Spacing
        );
        if (step <= 0)
        {
            step = font.Spacing;
        }

        int maxLines = Math.Max(
            1,
            (int)Math.Floor(rectangleValue.Height / step)
        );
        List<string> lines = Wrap(
            text.Replace("\r", ""),
            layoutWidth,
            font,
            paint,
            maxLines
        );
        float baseline = rectangleValue.Top - metrics.Ascent;
        float blockHeight =
            (lines.Count - 1) * step
            + metrics.Descent
            - metrics.Ascent;
        baseline += specification.Attr("valign") switch
        {
            "center" => Math.Max(
                0,
                (rectangleValue.Height - blockHeight) / 2
            ),
            "bottom" => Math.Max(
                0,
                rectangleValue.Height - blockHeight
            ),
            _ => 0,
        };
        canvas.Save();
        canvas.ClipRect(rectangleValue);
        foreach (string line in lines)
        {
            float width = LegacyTextWidth(line, font, paint);
            float x =
                rectangleValue.Left
                + inset
                + (
                    specification.Attr("align") switch
                    {
                        "right" => layoutWidth - width,
                        "center" => (layoutWidth - width) / 2,
                        _ => 0,
                    }
                );
            using SKTextBlob? blob = SKTextBlob.CreatePositioned(
                line,
                font,
                LegacyGlyphPositions(line, font, paint)
            );
            if (blob is not null)
            {
                canvas.DrawText(blob, x, baseline, paint);
            }

            paint.StrokeWidth = Math.Max(1, font.Size / 18);
            if (
                specification
                    .Attr("style")
                    .Contains(
                        "underline",
                        StringComparison.OrdinalIgnoreCase
                    )
            )
            {
                canvas.DrawLine(
                    x,
                    baseline + font.Size / 12,
                    x + width,
                    baseline + font.Size / 12,
                    paint
                );
            }

            if (
                specification
                    .Attr("style")
                    .Contains(
                        "strikeout",
                        StringComparison.OrdinalIgnoreCase
                    )
            )
            {
                canvas.DrawLine(
                    x,
                    baseline - font.Size / 3,
                    x + width,
                    baseline - font.Size / 3,
                    paint
                );
            }

            baseline += step;
        }

        canvas.Restore();
    }

    // GDI's legacy unshaped Latin layout uses integer character
    // advances.
    // Keep the glyph outlines intact rather than stretching the
    // rendered string.
    // In particular, the supplied ScoutCond 48pt title advances
    // by 653px, not 638px.

    internal static SKPoint[] LegacyGlyphPositions(
        string text,
        SKFont font,
        SKPaint paint
    )
    {
        float[] widths = font.GetGlyphWidths(text, paint);
        var positions = new SKPoint[widths.Length];
        float x = 0;
        for (var i = 0; i < widths.Length; i++)
        {
            positions[i] = new SKPoint(x, 0);
            x += MathF.Ceiling(widths[i]);
        }

        return positions;
    }

    internal static float LegacyTextWidth(
        string text,
        SKFont font,
        SKPaint paint
    )
    {
        return font.GetGlyphWidths(text, paint)
            .Sum(MathF.Ceiling);
    }

    private static List<string> Wrap(
        string text,
        float width,
        SKFont font,
        SKPaint paint,
        int maximum
    )
    {
        var lines = new List<string>();
        string[] paragraphs = text.Split('\n');
        for (
            var paragraphIndex = 0;
            paragraphIndex < paragraphs.Length;
            paragraphIndex++
        )
        {
            string remaining = paragraphs[paragraphIndex].Trim();
            if (remaining.Length == 0)
            {
                lines.Add("");
                if (lines.Count >= maximum)
                {
                    break;
                }

                continue;
            }

            while (remaining.Length > 0 && lines.Count < maximum)
            {
                int fitted = Math.Min(remaining.Length, 4096);
                if (
                    LegacyTextWidth(
                        remaining[..fitted],
                        font,
                        paint
                    ) > width
                )
                {
                    var lowerBound = 0;
                    int upperBound = fitted;
                    while (lowerBound + 1 < upperBound)
                    {
                        int middle =
                            (lowerBound + upperBound) / 2;
                        if (
                            LegacyTextWidth(
                                remaining[..middle],
                                font,
                                paint
                            ) <= width
                        )
                        {
                            lowerBound = middle;
                        }
                        else
                        {
                            upperBound = middle;
                        }
                    }

                    fitted = Math.Max(1, lowerBound);
                    if (
                        fitted < remaining.Length
                        && char.IsLowSurrogate(remaining[fitted])
                    )
                    {
                        fitted = Math.Max(1, fitted - 1);
                    }

                    int space = remaining.LastIndexOf(
                        ' ',
                        fitted - 1,
                        fitted
                    );
                    if (space > 0)
                    {
                        fitted = space;
                    }
                }

                string line = remaining[..fitted].TrimEnd();
                remaining = remaining[fitted..].TrimStart();
                if (
                    lines.Count == maximum - 1
                    && (
                        remaining.Length > 0
                        || paragraphIndex < paragraphs.Length - 1
                    )
                )
                {
                    while (
                        line.Length > 0
                        && LegacyTextWidth(
                            line + "…",
                            font,
                            paint
                        ) > width
                    )
                    {
                        line = line[..^1];
                    }

                    line += "…";
                }

                lines.Add(line);
            }

            if (lines.Count >= maximum)
            {
                break;
            }
        }

        return lines;
    }

    /// <summary>
    /// Renders a catalog menu background from the selected
    /// template.
    /// </summary>
    public void RenderMenu(
        string directory,
        string kind,
        XElement? panel = null
    )
    {
        using SKBitmap image = New(1920, 1080, true);
        using var canvas = new SKCanvas(image);
        string background = _template
            .Root.Element(kind)
            .Attr(
                "background",
                _template.Root.Element("menu").Attr("background")
            );
        Image(
            canvas,
            ReadImage(_template.Asset(background, true), true),
            SKRect.Create(1920, 1080)
        );
        XElement? scene = _template.Root.XPath(
            kind + "/iconview/background"
        );
        if (scene is not null)
        {
            foreach (
                XElement node in scene
                    .Elements("image")
                    .Where(nodeValue =>
                        Kind(nodeValue) == "static"
                    )
            )
            {
                Image(
                    canvas,
                    ReadImage(
                        _template.Asset(node.Value, true),
                        true
                    ),
                    Rect(node)
                );
            }
        }

        XElement? area =
            panel
            ?? (
                kind == "menu"
                    ? _template.Root.XPath("menu/main/area")
                    : null
            );
        if (area?.Attribute("image") is { } file)
        {
            Image(
                canvas,
                ReadImage(
                    _template.Asset(file.Value, true),
                    true
                ),
                Rect(area)
            );
        }

        Directory.CreateDirectory(directory);
        SaveBackground(
            image,
            Path.Combine(directory, "background")
        );
    }

    /// <summary>
    /// Renders a normal or selected navigation label as an AAI
    /// image.
    /// </summary>
    public void MenuLabel(
        string path,
        string caption,
        int width,
        int height,
        bool small = false,
        bool selected = true
    )
    {
        using SKBitmap image = New(width, height);
        using var canvas = new SKCanvas(image);
        var specification = new XElement(
            _template.Root.XPath(
                small ? "menu/index/text" : "menu/main/text"
            )
                ?? new XElement(
                    "text",
                    new XAttribute("size", "40")
                )
        );
        specification.SetAttributeValue("x", 0);
        specification.SetAttributeValue("y", 0);
        specification.SetAttributeValue("width", width);
        specification.SetAttributeValue("height", height);
        specification.SetAttributeValue("align", "center");
        specification.SetAttributeValue("valign", "center");
        specification.SetAttributeValue(
            "color",
            specification.Attr(
                selected ? "selectedcolor" : "color",
                "#ffffff"
            )
        );
        DrawText(canvas, specification, caption);
        AaiCodec.Save(image, path);
    }

    /// <summary>
    /// Gets the filename extension for the selected background
    /// format.
    /// </summary>
    public string BackgroundExtension =>
        _options.AaiBackgrounds ? ".aai" : ".jpg";

    private void SaveBackground(SKBitmap image, string path)
    {
        if (_options.AaiBackgrounds)
        {
            AaiCodec.Save(image, path + ".aai");
        }
        else
        {
            SaveJpeg(image, path + ".jpg");
        }
    }

    /// <summary>
    /// Writes a bitmap as a JPEG preview at quality 95.
    /// </summary>
    public static void SaveJpeg(SKBitmap image, string path)
    {
        using SKImage encoded = SKImage.FromBitmap(image);
        using SKData data = encoded.Encode(
            SKEncodedImageFormat.Jpeg,
            95
        );
        using FileStream file = File.Create(path);
        data.SaveTo(file);
    }

    /// <summary>
    /// Releases owned resources and temporary artwork, where
    /// applicable.
    /// </summary>
    public void Dispose()
    {
        foreach (SKTypeface face in _fonts.Values.Distinct())
        {
            face.Dispose();
        }

        foreach (
            SKBitmap image in _assets.Values.Concat(
                _mediaImages.Values
            )
        )
        {
            image.Dispose();
        }
    }
}
