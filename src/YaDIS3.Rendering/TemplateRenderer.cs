using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SkiaSharp;
namespace YaDIS3.Rendering;

public sealed record RenderOptions(string Language = "fr", bool AaiBackgrounds = false, bool UseCases = false);
public sealed record RenderedCard(string Directory, string Preview, IReadOnlyDictionary<string, string> EpisodePreviews);

/// <summary>Portable renderer for the Dune template XML, fonts, masks and layers.</summary>
public sealed class TemplateRenderer : IDisposable
{
    private readonly DuneTemplate template;
    private readonly RenderOptions options;
    private readonly Dictionary<string, SKTypeface> fonts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SKBitmap> assets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SKBitmap> mediaImages = new(StringComparer.Ordinal);
    private CancellationToken cancellation;
    public TemplateRenderer(DuneTemplate template, RenderOptions options)
    {
        this.template = template; this.options = options;
        foreach (var path in template.Fonts)
        {
            var face = SKTypeface.FromFile(path); if (face is null) continue;
            var fileKey = FontKey(Path.GetFileNameWithoutExtension(path));
            fonts.TryAdd(fileKey, face); fonts.TryAdd(FontKey(face.FamilyName), face);
        }
    }
    private static string FontKey(string name) => Regex.Replace(name, "[^a-zA-Z0-9]", "").ToLowerInvariant();
    private static SKBitmap New(int width, int height, bool opaque = false)
    {
        AaiCodec.CheckSize(width, height); var image = new SKBitmap(width, height);
        using var canvas = new SKCanvas(image); canvas.Clear(opaque ? SKColors.Black : SKColors.Transparent); return image;
    }
    private SKBitmap? ReadImage(string? path, bool commonAsset)
    {
        if (path is null || !File.Exists(path)) return null;
        var cache = commonAsset ? assets : mediaImages;
        if (cache.TryGetValue(path, out var found)) return found;
        using var input = File.OpenRead(path); using var codec = SKCodec.Create(input);
        if (codec is null) throw new InvalidDataException($"Image illisible : {Path.GetFileName(path)}.");
        AaiCodec.CheckSize(codec.Info.Width, codec.Info.Height);
        var bitmap = SKBitmap.Decode(codec) ?? throw new InvalidDataException("Décodage d'image impossible."); cache[path] = bitmap; return bitmap;
    }
    private void Image(SKCanvas canvas, SKBitmap? image, SKRect rectangle)
    {
        cancellation.ThrowIfCancellationRequested();
        if (image is null || rectangle.Width <= 0 || rectangle.Height <= 0) return;
        using var source = SKImage.FromBitmap(image); canvas.DrawImage(source, rectangle, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
    }
    private static SKRect Rect(XElement? node) => SKRect.Create(node.Float("x"), node.Float("y"), node.Float("width"), node.Float("height"));
    private static string Kind(XElement node) => node.Attr("type").ToLowerInvariant();
    private SKBitmap Framed(MediaContext media)
    {
        var part = media.IsSeries ? template.Root.XPath("tv/banner") ?? template.Node("tv/poster") : template.Node(options.UseCases ? "movie/poster/case" : "movie/poster/standard");
        var size = part.Element("size"); var inner = part.Element("inner") ?? size;
        var width = inner.Int("width"); var height = inner.Int("height");
        using var content = New(width, height); using (var canvas = new SKCanvas(content))
            Image(canvas, ReadImage(media.Artwork(media.IsSeries && template.Root.XPath("tv/banner") is not null ? "banner.jpg" : "poster.jpg"), false), SKRect.Create(width, height));
        if (part.Element("mask") is { } maskNode) ApplyMask(content, ReadImage(template.Asset(maskNode.Value, true), true)!);
        var border = size.Int("border"); if (border < 0 || border > 256) throw new InvalidDataException("Bordure de template invalide.");
        var output = New(checked(size.Int("width") + 2 * border), checked(size.Int("height") + 2 * border));
        using var draw = new SKCanvas(output);
        Image(draw, content, SKRect.Create(inner.Int("x") + border, inner.Int("y") + border, width, height));
        var frame = part.Element("frame")?.Value;
        if (!media.IsSeries && options.UseCases) frame = "case_" + media.Info.ValueAt("mediainfo/source") + ".png";
        var framePath = frame is null ? null : template.Asset(frame);
        if (framePath is null && !media.IsSeries && options.UseCases) framePath = template.Asset("case_default.png", true);
        Image(draw, ReadImage(framePath, true), SKRect.Create(border, border, size.Int("width"), size.Int("height"))); return output;
    }
    internal static void ApplyMask(SKBitmap image, SKBitmap mask)
    {
        using var scaled = New(image.Width, image.Height);
        using (var canvas = new SKCanvas(scaled)) canvas.DrawBitmap(mask, SKRect.Create(image.Width, image.Height));
        // The old renderer takes min(alpha(image), alpha(mask)), not multiplication.
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
            {
                var color = image.GetPixel(x, y); var alpha = Math.Min(color.Alpha, scaled.GetPixel(x, y).Alpha);
                if (alpha != color.Alpha) image.SetPixel(x, y, color.WithAlpha(alpha));
            }
    }
    private SKBitmap Thumbnail(MediaContext media, XElement episode)
    {
        var part = template.Node("tv/episode"); var size = part.Element("size"); var image = New(size.Int("width"), size.Int("height"));
        using var canvas = new SKCanvas(image);
        Image(canvas, ReadImage(media.Artwork($"{MediaContext.Season(episode)}x{MediaContext.Number(episode)}.jpg"), false), SKRect.Create(image.Width, image.Height));
        if (part.Element("mask") is { } mask) ApplyMask(image, ReadImage(template.Asset(mask.Value, true), true)!);
        if (part.Element("frame") is { } frame) Image(canvas, ReadImage(template.Asset(frame.Value, true), true), SKRect.Create(image.Width, image.Height));
        return image;
    }
    public RenderedCard Render(MediaContext media, string directory, CancellationToken cancellationToken = default, IProgress<string>? progress = null)
    {
        cancellation = cancellationToken; Directory.CreateDirectory(directory);
        try
        {
            using var framed = Framed(media);
            var iconNode = template.Node(media.IsSeries ? "tv/iconview/icon" : "movie/iconview/icon");
            using (var icon = New(iconNode.Int("width"), iconNode.Int("height")))
            { using var canvas = new SKCanvas(icon); Image(canvas, framed, SKRect.Create(icon.Width, icon.Height)); AaiCodec.Save(icon, Path.Combine(directory, "icon.aai")); }
            using var common = New(1920, 1080, true);
            using (var canvas = new SKCanvas(common))
            {
                var parent = template.Node(media.IsSeries ? "tv" : "movie");
                Image(canvas, ReadImage(template.Asset(parent.Attr("background"), true), true), SKRect.Create(1920, 1080));
                DrawScene(canvas, template.Node(media.IsSeries ? "tv/synopsis/common" : "movie/synopsis"), media, null, framed, null);
            }
            var preview = Path.Combine(directory, "preview.jpg"); var episodePreviews = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!media.IsSeries) { SaveBackground(common, Path.Combine(directory, "background")); SaveJpeg(common, preview); return new(directory, preview, episodePreviews); }
            using (var season = common.Copy())
            {
                using var canvas = new SKCanvas(season); DrawScene(canvas, template.Node("tv/synopsis/season"), media, null, framed, null);
                SaveBackground(season, Path.Combine(directory, "background")); SaveJpeg(season, preview);
            }
            using var episodeBackground = common.Copy();
            using (var canvas = new SKCanvas(episodeBackground)) DrawScene(canvas, template.Node("tv/synopsis/episode"), media, null, framed, null);
            SaveBackground(episodeBackground, Path.Combine(directory, "background2"));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in media.Episodes.GroupBy(MediaContext.ViewingSeason))
            {
                var seasonDirectory = Path.Combine(directory, group.Key); Directory.CreateDirectory(seasonDirectory);
                var spec = template.Node("tv/synopsis/season/seasonicon"); var iconSpec = spec.Element("icon");
                using var seasonIcon = New(iconSpec.Int("width"), iconSpec.Int("height"));
                using (var canvas = new SKCanvas(seasonIcon))
                {
                    Image(canvas, ReadImage(template.Asset(iconSpec!.Value, true), true), SKRect.Create(seasonIcon.Width, seasonIcon.Height));
                    var text = new XElement(spec.Element("text")!); text.SetAttributeValue("x", 0); text.SetAttributeValue("y", text.Int("offset"));
                    text.SetAttributeValue("width", seasonIcon.Width); text.SetAttributeValue("height", seasonIcon.Height); text.SetAttributeValue("align", "center"); text.SetAttributeValue("valign", "center");
                    DrawText(canvas, text, group.Key.TrimStart('0') is { Length: > 0 } s ? s : "SP");
                }
                AaiCodec.Save(seasonIcon, Path.Combine(seasonDirectory, "icon.aai"));
                foreach (var episode in group)
                {
                    cancellation.ThrowIfCancellationRequested(); progress?.Report($"{media.Title} · S{MediaContext.Season(episode):00}E{MediaContext.Number(episode):00}");
                    var key = group.Key + "/" + MediaContext.EpisodeFolder(episode);
                    if (!seen.Add(key)) throw new InvalidDataException("Deux épisodes ont la même clé de navigation Dune.");
                    var output = Path.Combine(seasonDirectory, MediaContext.EpisodeFolder(episode)); Directory.CreateDirectory(output);
                    var episodeSpec = template.Node("tv/synopsis/episode/episodeicon"); var area = episodeSpec.Element("area");
                    using var thumbnail = Thumbnail(media, episode); using var overlay = New(area.Int("width"), area.Int("height"));
                    using (var canvas = new SKCanvas(overlay)) DrawScene(canvas, episodeSpec, media, episode, framed, thumbnail);
                    AaiCodec.Save(overlay, Path.Combine(output, "icon.aai"));
                    using var full = episodeBackground.Copy(); using (var canvas = new SKCanvas(full)) Image(canvas, overlay, Rect(area));
                    var episodePreview = Path.Combine(output, "preview.jpg"); SaveJpeg(full, episodePreview); episodePreviews[key] = episodePreview;
                }
            }
            return new(directory, preview, episodePreviews);
        }
        finally { foreach (var image in mediaImages.Values) image.Dispose(); mediaImages.Clear(); }
    }
    private void DrawScene(SKCanvas canvas, XElement scene, MediaContext media, XElement? episode, SKBitmap framed, SKBitmap? thumbnail)
    {
        foreach (var image in scene.Elements("image"))
        {
            var type = Kind(image); var rect = Rect(image);
            if (type == "special" && (episode is null || MediaContext.Season(episode) != 0)) continue;
            if (type is "poster" or "banner") { Image(canvas, framed, rect); continue; }
            if (type == "thumbnail") { Image(canvas, thumbnail, rect); continue; }
            if (type is "country" or "countries")
            {
                var countries = media.Info.Descendants("country").Select(c => c.Value).Take(image.Int("count", 99)); var index = 0;
                foreach (var country in countries)
                {
                    var shifted = rect; if (image.Attr("direction") == "vertical") shifted.Offset(0, index * image.Float("spacing")); else shifted.Offset(index * image.Float("spacing"), 0);
                    Image(canvas, ReadImage(template.Asset("flag_" + country.Trim() + ".png"), true), shifted); index++;
                }
                continue;
            }
            var data = episode ?? media.Info;
            var path = type switch
            {
                "fanart" => media.Artwork("fanart.jpg"),
                "static" or "special" => template.Asset(image.Value.Trim(), true),
                "vcodec" => Badge(data.ValueAt("mediainfo/video/codec")), "acodec" => Badge(data.ValueAt("mediainfo/audio/codec")),
                "format" => Badge(data.ValueAt("mediainfo/format")), "channels" => Badge(data.ValueAt("mediainfo/audio/channels")),
                "resolution" => Badge(data.ValueAt("mediainfo/video/resolution")), "aspect" => Badge(data.ValueAt("mediainfo/video/aspectratio").Replace(":", "_").Replace("_1", "")),
                "framerate" => Badge(data.ValueAt("mediainfo/video/framerate")), "frequency" => Badge(Frequency(data) + "hz"),
                "certification" => Badge((options.Language == "en" ? "gb" : options.Language) + "_" + media.Info.ValueAt("certification").Trim('-')),
                "rating" => double.TryParse(data.ValueAt("rating").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var rating) ? Badge(Math.Round(rating).ToString(CultureInfo.InvariantCulture)) : null,
                _ => null
            };
            Image(canvas, ReadImage(path, type != "fanart"), rect);
        }
        foreach (var text in scene.Elements("text"))
        {
            if (text.Attr("language") is { Length: > 0 } language && language != options.Language) continue;
            if (Kind(text) == "special" && (episode is null || MediaContext.Season(episode) != 0)) continue;
            DrawText(canvas, text, TextValue(text, media, episode));
        }
        if (scene.Element("languages") is { } languages) DrawLanguages(canvas, languages, episode ?? media.Info);
    }
    private string? Badge(string name) => string.IsNullOrWhiteSpace(name) ? null : template.Asset(name.ToLowerInvariant() + ".png");
    private static string Frequency(XElement data)
    {
        var rate = data.ValueAt("mediainfo/video/framerate");
        if (data.ValueAt("mediainfo/video/resolution").EndsWith('i') && double.TryParse(rate.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && n is >= 25 and <= 30) return (n * 2).ToString("0.##", CultureInfo.InvariantCulture);
        return rate;
    }
    private string TextValue(XElement spec, MediaContext media, XElement? episode)
    {
        var type = Kind(spec); var data = episode ?? media.Info;
        string Join(string singular, string field, string defaultSeparator)
        {
            var elements = media.Info.XPath(field)?.Elements(singular).Select(x => x.Element("name")?.Value ?? x.Value).ToArray() ?? [];
            if (elements.Length == 0) elements = data.ValueAt(singular).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return string.Join(spec.Attr("direction") == "vertical" ? "\n" : spec.Attr("separator", defaultSeparator), elements);
        }
        switch (type)
        {
            case "static": case "special": return spec.Value;
            case "title":
                var title = episode?.ValueAt("title") ?? media.Title;
                if (episode is not null && spec.Attr("episodenumber") == "yes") title = MediaContext.Number(episode) + spec.Attr("separator", ". ") + title;
                if (episode is not null && spec.Attr("seasonnumber") == "yes") title = MediaContext.Season(episode) + spec.Attr("separator", ". ") + title;
                return title;
            case "originaltitle":
                var original = media.Info.ValueAt("original_title"); return spec.Attr("permanent", "yes") == "no" && original == media.Title ? "" : original;
            case "genres": return Join("genre", "genres", " / ");
            case "cast": return Join("actor", "actors", ", ");
            case "director": return episode is null ? Join("director", "directors", ", ") : string.Join(", ", data.ValueAt("director").Split('|', StringSplitOptions.RemoveEmptyEntries));
            case "studios": return Join("studio", "studios", ", ");
            case "runtime":
                var value = episode is null ? media.Info.ValueAt("runtime") : data.ValueAt("mediainfo/duration");
                if (!double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) || duration <= 0 || duration > 1e10) return "";
                return FormatRuntime(duration, episode is not null);
            case "season": return episode is null ? "" : MediaContext.Season(episode).ToString(CultureInfo.InvariantCulture);
            case "aired": return options.Language == "fr" && DateOnly.TryParseExact(data.ValueAt("aired"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var aired) ? aired.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) : data.ValueAt("aired");
            case "resolution": var resolution = data.ValueAt("mediainfo/video/resolution"); return spec.Attr("scantype", "yes") == "no" ? resolution.TrimEnd('p', 'i') : resolution;
            case "channels": return data.ValueAt("mediainfo/audio/channels");
            case "aspect": return data.ValueAt("mediainfo/video/aspectratio");
            case "framerate": return data.ValueAt("mediainfo/video/framerate");
            case "frequency": return Frequency(data);
            default: return data.ValueAt(type);
        }
    }
    // Preserve the original episode rule: round up only when whole seconds exceed 30.
    // Do not round or rewrite the stored MediaInfo duration.
    internal static string FormatRuntime(double duration, bool episode)
    {
        if (!double.IsFinite(duration) || duration <= 0 || duration > 1e10) return "";
        var span = episode ? TimeSpan.FromMilliseconds(duration) : TimeSpan.FromMinutes(duration);
        if (episode && span.Seconds > 30) span = span.Add(TimeSpan.FromMinutes(1));
        if (span.TotalMinutes < 1) return "";
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours}h{span.Minutes:00}" : $"{span.Minutes}mn";
    }
    private void DrawLanguages(SKCanvas canvas, XElement spec, XElement data)
    {
        var back = spec.Element("backimage"); if (back is null) return;
        var image = ReadImage(template.Asset(back.Value, true), true);
        foreach (var field in new[] { "audio", "subtitles" })
        {
            var position = spec.Element(field); if (position is null) continue;
            var values = data.ValueAt("mediainfo/" + field + "/languages").Split(['|', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.Length > 2 ? s[..2] : s).Distinct(StringComparer.OrdinalIgnoreCase).Take(position.Int("count", 3)); var index = 0;
            foreach (var language in values)
            {
                var x = position.Int("x") + (position.Attr("direction") == "vertical" ? 0 : index * back.Int("spacing"));
                var y = position.Int("y") + (position.Attr("direction") == "vertical" ? index * back.Int("spacing") : 0);
                Image(canvas, image, SKRect.Create(x, y, back.Int("width"), back.Int("height")));
                var text = new XElement(spec.Element("text")!); text.SetAttributeValue("x", x); text.SetAttributeValue("y", y + text.Int("offset"));
                text.SetAttributeValue("width", back.Int("width")); text.SetAttributeValue("height", back.Int("height")); text.SetAttributeValue("align", "center"); text.SetAttributeValue("valign", "center");
                DrawText(canvas, text, language); index++;
            }
        }
    }
    internal void DrawText(SKCanvas canvas, XElement spec, string text)
    {
        cancellation.ThrowIfCancellationRequested(); if (text.Length == 0) return;
        text = spec.Attr("case") switch { "upper" => text.ToUpperInvariant(), "lower" => text.ToLowerInvariant(), _ => text };
        var rect = Rect(spec); if (rect.Width <= 0 || rect.Height <= 0) return;
        var family = spec.Attr("font"); fonts.TryGetValue(FontKey(family), out var face);
        using var font = new SKFont(face ?? SKTypeface.Default, spec.Float("size", 24) * 96 / 72) { Edging = SKFontEdging.Antialias, Subpixel = true, LinearMetrics = true,
            Embolden = spec.Attr("style").Contains("bold", StringComparison.OrdinalIgnoreCase) && (face?.FontStyle.Weight ?? 400) < 700,
            SkewX = spec.Attr("style").Contains("italic", StringComparison.OrdinalIgnoreCase) && face?.IsItalic != true ? -0.25f : 0 };
        using var paint = new SKPaint { IsAntialias = true, Color = SKColor.TryParse(spec.Attr("color", "#ffffff"), out var color) ? color : SKColors.White };
        // Graphics.DrawString's default layout reserves an em/6 overhang margin
        // at both sides. Use it for alignment and wrapping as well as drawing.
        var inset = font.Size / 6;
        var layoutWidth = Math.Max(1, rect.Width - 2 * inset);
        var metrics = font.Metrics; var step = spec.Float("spacing", font.Spacing);
        if (step <= 0) step = font.Spacing;
        var maxLines = Math.Max(1, (int)Math.Floor(rect.Height / step));
        var lines = Wrap(text.Replace("\r", ""), layoutWidth, font, paint, maxLines);
        var baseline = rect.Top - metrics.Ascent;
        var blockHeight = (lines.Count - 1) * step + metrics.Descent - metrics.Ascent;
        baseline += spec.Attr("valign") switch { "center" => Math.Max(0, (rect.Height - blockHeight) / 2), "bottom" => Math.Max(0, rect.Height - blockHeight), _ => 0 };
        canvas.Save(); canvas.ClipRect(rect);
        foreach (var line in lines)
        {
            var width = LegacyTextWidth(line, font, paint); var x = rect.Left + inset + (spec.Attr("align") switch { "right" => layoutWidth - width, "center" => (layoutWidth - width) / 2, _ => 0 });
            using var blob = SKTextBlob.CreatePositioned(line, font, LegacyGlyphPositions(line, font, paint));
            if (blob is not null) canvas.DrawText(blob, x, baseline, paint);
            paint.StrokeWidth = Math.Max(1, font.Size / 18);
            if (spec.Attr("style").Contains("underline", StringComparison.OrdinalIgnoreCase)) canvas.DrawLine(x, baseline + font.Size / 12, x + width, baseline + font.Size / 12, paint);
            if (spec.Attr("style").Contains("strikeout", StringComparison.OrdinalIgnoreCase)) canvas.DrawLine(x, baseline - font.Size / 3, x + width, baseline - font.Size / 3, paint);
            baseline += step;
        }
        canvas.Restore();
    }
    // GDI's legacy unshaped Latin layout uses integer character advances.
    // Keep the glyph outlines intact rather than stretching the rendered string.
    // In particular, the supplied ScoutCond 48pt title advances by 653px, not 638px.
    internal static SKPoint[] LegacyGlyphPositions(string text, SKFont font, SKPaint paint)
    {
        var widths = font.GetGlyphWidths(text, paint); var positions = new SKPoint[widths.Length]; float x = 0;
        for (var i = 0; i < widths.Length; i++) { positions[i] = new SKPoint(x, 0); x += MathF.Ceiling(widths[i]); }
        return positions;
    }
    internal static float LegacyTextWidth(string text, SKFont font, SKPaint paint) => font.GetGlyphWidths(text, paint).Sum(MathF.Ceiling);
    private static List<string> Wrap(string text, float width, SKFont font, SKPaint paint, int maximum)
    {
        var lines = new List<string>(); var paragraphs = text.Split('\n');
        for (var p = 0; p < paragraphs.Length; p++)
        {
            var remaining = paragraphs[p].Trim(); if (remaining.Length == 0) { lines.Add(""); if (lines.Count >= maximum) break; continue; }
            while (remaining.Length > 0 && lines.Count < maximum)
            {
                var fitted = Math.Min(remaining.Length, 4096);
                if (LegacyTextWidth(remaining[..fitted], font, paint) > width)
                {
                    var lo = 0; var hi = fitted;
                    while (lo + 1 < hi) { var middle = (lo + hi) / 2; if (LegacyTextWidth(remaining[..middle], font, paint) <= width) lo = middle; else hi = middle; }
                    fitted = Math.Max(1, lo);
                    if (fitted < remaining.Length && char.IsLowSurrogate(remaining[fitted])) fitted = Math.Max(1, fitted - 1);
                    var space = remaining.LastIndexOf(' ', fitted - 1, fitted); if (space > 0) fitted = space;
                }
                var line = remaining[..fitted].TrimEnd(); remaining = remaining[fitted..].TrimStart();
                if (lines.Count == maximum - 1 && (remaining.Length > 0 || p < paragraphs.Length - 1))
                {
                    while (line.Length > 0 && LegacyTextWidth(line + "…", font, paint) > width) line = line[..^1];
                    line += "…";
                }
                lines.Add(line);
            }
            if (lines.Count >= maximum) break;
        }
        return lines;
    }
    public void RenderMenu(string directory, string kind, XElement? panel = null)
    {
        using var image = New(1920, 1080, true); using var canvas = new SKCanvas(image);
        var background = template.Root.Element(kind).Attr("background", template.Root.Element("menu").Attr("background")); Image(canvas, ReadImage(template.Asset(background, true), true), SKRect.Create(1920, 1080));
        var scene = template.Root.XPath(kind + "/iconview/background");
        if (scene is not null) foreach (var node in scene.Elements("image").Where(n => Kind(n) == "static")) Image(canvas, ReadImage(template.Asset(node.Value, true), true), Rect(node));
        var area = panel ?? (kind == "menu" ? template.Root.XPath("menu/main/area") : null);
        if (area?.Attribute("image") is { } file) Image(canvas, ReadImage(template.Asset(file.Value, true), true), Rect(area));
        Directory.CreateDirectory(directory); SaveBackground(image, Path.Combine(directory, "background"));
    }
    public void MenuLabel(string path, string caption, int width, int height, bool small = false, bool selected = true)
    {
        using var image = New(width, height); using var canvas = new SKCanvas(image);
        var spec = new XElement(template.Root.XPath(small ? "menu/index/text" : "menu/main/text") ?? new XElement("text", new XAttribute("size", "40")));
        spec.SetAttributeValue("x", 0); spec.SetAttributeValue("y", 0); spec.SetAttributeValue("width", width); spec.SetAttributeValue("height", height);
        spec.SetAttributeValue("align", "center"); spec.SetAttributeValue("valign", "center"); spec.SetAttributeValue("color", spec.Attr(selected ? "selectedcolor" : "color", "#ffffff"));
        DrawText(canvas, spec, caption); AaiCodec.Save(image, path);
    }
    public string BackgroundExtension => options.AaiBackgrounds ? ".aai" : ".jpg";
    private void SaveBackground(SKBitmap image, string path) { if (options.AaiBackgrounds) AaiCodec.Save(image, path + ".aai"); else SaveJpeg(image, path + ".jpg"); }
    public static void SaveJpeg(SKBitmap image, string path)
    { using var encoded = SKImage.FromBitmap(image); using var data = encoded.Encode(SKEncodedImageFormat.Jpeg, 95); using var file = File.Create(path); data.SaveTo(file); }
    public void Dispose()
    { foreach (var face in fonts.Values.Distinct()) face.Dispose(); foreach (var image in assets.Values.Concat(mediaImages.Values)) image.Dispose(); }
}
