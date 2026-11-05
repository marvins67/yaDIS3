using System.Xml.Linq;
using SkiaSharp;

namespace YaDIS3.Rendering;

public sealed partial class TemplateRenderer
{
    private static SKBitmap CreateBitmap(
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

    private SKBitmap? ReadImage(
        string? path,
        bool commonAsset,
        bool allowEmpty = false
    )
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

        try
        {
            using FileStream input = File.OpenRead(path);
            // Un fanart vide représente une image absente dans les anciens workspaces.
            if (allowEmpty && input.Length == 0)
            {
                return null;
            }

            using SKCodec codec = SKCodec.Create(input);
            if (codec is null)
            {
                throw new InvalidDataException(
                    "Format d'image illisible."
                );
            }

            AaiCodec.CheckSize(
                codec.Info.Width,
                codec.Info.Height
            );
            SKBitmap bitmap =
                SKBitmap.Decode(codec)
                ?? throw new InvalidDataException(
                    "Décodage d'image impossible."
                );
            cache[path] = bitmap;
            return bitmap;
        }
        catch (Exception exception)
            when (_treatmentPreview is not null && !commonAsset
                && IsRenderFailure(exception))
        {
            // yaDIS2 ignore une illustration locale illisible et poursuit le rendu.
            s_skippedArtwork(_logger, path, exception);
            return null;
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or InvalidDataException
                        or UnauthorizedAccessException
                        or ArgumentException
                        or InvalidOperationException
                        or NotSupportedException
            )
        {
            throw new InvalidDataException(
                $"Image illisible : {Path.GetFullPath(path)}. "
                    + exception.Message,
                exception
            );
        }
    }

    private void DrawImage(
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

    private static SKRect ReadRectangle(XElement? node)
    {
        return SKRect.Create(
            node.Float("x"),
            node.Float("y"),
            node.Float("width"),
            node.Float("height")
        );
    }

    private static string ReadElementKind(XElement node)
    {
        return node.Attr("type").ToLowerInvariant();
    }

    private SKBitmap CreateFramedArtwork(MediaContext media)
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
        using SKBitmap content = CreateBitmap(width, height);
        using (var canvas = new SKCanvas(content))
        {
            DrawImage(
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

        SKBitmap output = CreateBitmap(
            checked(size.Int("width") + 2 * border),
            checked(size.Int("height") + 2 * border)
        );
        using var draw = new SKCanvas(output);
        DrawImage(
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
            // yaDIS2 poursuit le rendu si le boîtier par défaut manque.
            framePath = _template.Asset("case_default.png");
        }

        DrawImage(
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
        using SKBitmap scaled = CreateBitmap(
            image.Width,
            image.Height
        );
        using (var canvas = new SKCanvas(scaled))
        {
            canvas.DrawBitmap(
                mask,
                SKRect.Create(image.Width, image.Height)
            );
        }

        // Le moteur historique conserve le minimum des deux alphas sans les multiplier.
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

    private SKBitmap CreateEpisodeThumbnail(
        MediaContext media,
        XElement episode
    )
    {
        XElement part = _template.Node("tv/episode");
        XElement? size = part.Element("size");
        SKBitmap image = CreateBitmap(
            size.Int("width"),
            size.Int("height")
        );
        using var canvas = new SKCanvas(image);
        DrawImage(
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
            DrawImage(
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

    private void RenderMediaIcon(
        MediaContext media,
        SKBitmap framed,
        string directory
    )
    {
        XElement iconNode = _template.Node(
            media.IsSeries
                ? "tv/iconview/icon"
                : "movie/iconview/icon"
        );

        using (
            SKBitmap icon = CreateBitmap(
                iconNode.Int("width"),
                iconNode.Int("height")
            )
        )
        {
            using var canvas = new SKCanvas(icon);
            DrawImage(
                canvas,
                framed,
                SKRect.Create(icon.Width, icon.Height)
            );
            AaiCodec.Save(
                icon,
                Path.Combine(directory, "icon.aai")
            );
        }
    }

    private void DrawCommonBackground(
        MediaContext media,
        SKBitmap framed,
        SKBitmap common
    )
    {
        using (var canvas = new SKCanvas(common))
        {
            XElement parent = _template.Node(
                media.IsSeries ? "tv" : "movie"
            );
            DrawImage(
                canvas,
                ReadImage(
                    _template.Asset(
                        parent.Attr("background"),
                        true
                    ),
                    true
                ),
                SKRect.Create(CanvasWidth, CanvasHeight)
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
    }
}
