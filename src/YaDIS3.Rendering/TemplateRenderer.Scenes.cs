using System.Globalization;
using System.Xml.Linq;
using SkiaSharp;

namespace YaDIS3.Rendering;

public sealed partial class TemplateRenderer
{
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
            string type = ReadElementKind(image);
            SKRect rectangleValue = ReadRectangle(image);
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
                DrawImage(canvas, framed, rectangleValue);
                continue;
            }

            if (type == "thumbnail")
            {
                DrawImage(canvas, thumbnail, rectangleValue);
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

                    DrawImage(
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
            DrawImage(
                canvas,
                ReadImage(
                    path,
                    type != "fanart",
                    allowEmpty: type == "fanart"
                ),
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
                ReadElementKind(text) == "special"
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
                ResolveTextValue(text, media, episode)
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

    private string ResolveTextValue(
        XElement specification,
        MediaContext media,
        XElement? episode
    )
    {
        string type = ReadElementKind(specification);
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

    // Arrondir les épisodes seulement lorsque les secondes entières dépassent 30,
    // sans réécrire la durée MediaInfo enregistrée.

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
                DrawImage(
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
}
