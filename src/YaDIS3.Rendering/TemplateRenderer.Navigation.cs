using System.Xml.Linq;
using SkiaSharp;

namespace YaDIS3.Rendering;

public sealed partial class TemplateRenderer
{
    /// <summary>
    /// Conserve une icône de bandeau par dossier, sans fichiers
    /// nav-*.
    /// </summary>
    internal void RenderLegacyNavigation(
        IReadOnlyList<(
            string Directory,
            string Caption
        )> entries,
        string section
    )
    {
        XElement area = _template.Node(section + "/area");
        XElement text = _template.Node(section + "/text");
        int[] widths = entries
            .Select(entry =>
                NavigationWidth(entry.Caption, text)
            )
            .ToArray();
        for (
            int selected = 0;
            selected < entries.Count;
            selected++
        )
        {
            using SKBitmap icon = NavigationBar(
                entries,
                selected,
                widths,
                area,
                text
            );
            Directory.CreateDirectory(
                entries[selected].Directory
            );
            AaiCodec.Save(
                icon,
                Path.Combine(
                    entries[selected].Directory,
                    "icon.aai"
                )
            );
        }
    }

    private int NavigationWidth(string caption, XElement text)
    {
        _fonts.TryGetValue(
            NormalizeFontName(text.Attr("font")),
            out SKTypeface? face
        );
        // MeasureString emploie les avances ajustées aux pixels, pas une
        // avance fractionnaire arrondie séparément pour chaque glyphe.
        using SKFont font = new(
            face ?? SKTypeface.Default,
            text.Float("size", 24) * 96 / 72
        );
        using SKPaint paint = new();
        string value = text.Attr("case") switch
        {
            "upper" => caption.ToUpperInvariant(),
            "lower" => caption.ToLowerInvariant(),
            _ => caption,
        };
        int width = (int)
            Math.Round(
                LegacyTextWidth(value, font, paint)
                    + font.Size / 3
                    + 8
            );
        return width + width % 2;
    }

    private SKBitmap NavigationBar(
        IReadOnlyList<(
            string Directory,
            string Caption
        )> entries,
        int selected,
        int[] widths,
        XElement area,
        XElement text
    )
    {
        SKBitmap image = CreateBitmap(
            area.Int("width"),
            area.Int("height")
        );
        using SKCanvas canvas = new(image);
        DrawImage(
            canvas,
            ReadImage(
                _template.Asset(area.Attr("image"), true),
                true
            ),
            SKRect.Create(image.Width, image.Height)
        );
        using SKBitmap labels = CreateBitmap(
            image.Width,
            image.Height
        );
        using (SKCanvas labelCanvas = new(labels))
        {
            int x = (int)
                Math.Round(
                    (image.Width - widths[selected]) / 2.0
                );
            DrawNavigationLabels(
                labelCanvas,
                entries,
                widths,
                selected,
                x,
                text,
                image.Height
            );
        }

        SKBitmap? mask = ReadImage(
            _template.Asset(area.Attr("mask")),
            true
        );
        if (mask is not null)
        {
            ApplyMask(labels, mask);
        }

        DrawImage(
            canvas,
            labels,
            SKRect.Create(image.Width, image.Height)
        );
        return image;
    }

    private void DrawNavigationLabels(
        SKCanvas canvas,
        IReadOnlyList<(
            string Directory,
            string Caption
        )> entries,
        int[] widths,
        int selected,
        int selectedX,
        XElement source,
        int height
    )
    {
        int spacing = source.Int("spacing");
        int x = selectedX;
        for (
            int index = selected;
            index < entries.Count;
            index++
        )
        {
            DrawNavigationLabel(
                canvas,
                entries[index].Caption,
                source,
                x,
                widths[index],
                height,
                index == selected
            );
            x += widths[index] + spacing;
        }

        x = selectedX;
        for (int index = selected - 1; index >= 0; index--)
        {
            x -= widths[index] + spacing;
            DrawNavigationLabel(
                canvas,
                entries[index].Caption,
                source,
                x,
                widths[index],
                height,
                false
            );
        }
    }

    private void DrawNavigationLabel(
        SKCanvas canvas,
        string caption,
        XElement source,
        int x,
        int width,
        int height,
        bool selected
    )
    {
        XElement text = new(source);
        text.Attribute("spacing")?.Remove();
        text.SetAttributeValue("x", x);
        text.SetAttributeValue("y", source.Int("offset"));
        text.SetAttributeValue("width", width);
        text.SetAttributeValue("height", height);
        text.SetAttributeValue("align", "center");
        text.SetAttributeValue("valign", "center");
        text.SetAttributeValue(
            "color",
            source.Attr(selected ? "selectedcolor" : "color")
        );
        DrawText(canvas, text, caption);
    }

    /// <summary>
    /// Les bandeaux actifs font partie du fond dans l'export
    /// original.
    /// </summary>
    internal void OverlayNavigation(
        string directory,
        string iconPath,
        XElement area,
        string backgroundName = "background"
    )
    {
        string path = Path.Combine(
            directory,
            backgroundName + BackgroundExtension
        );
        using SKBitmap background = _options.AaiBackgrounds
            ? AaiCodec.Load(path)
            : ReadImage(path, false)!.Copy();
        using (SKCanvas canvas = new(background))
        {
            using SKBitmap icon = AaiCodec.Load(iconPath);
            DrawImage(canvas, icon, ReadRectangle(area));
        }

        SaveBackground(
            background,
            Path.Combine(directory, backgroundName)
        );
        // Les fonds sont réécrits après composition, leur cache doit être invalidé.
        if (_mediaImages.Remove(path, out SKBitmap? cached))
        {
            cached.Dispose();
        }
    }

    /// <summary>
    /// La saga reprend son fanart et son titre, pas le menu des
    /// films.
    /// </summary>
    internal void RenderSagaBackground(
        MediaContext? poster,
        string title,
        bool useFanart,
        string directory
    )
    {
        using SKBitmap background = CreateBitmap(
            CanvasWidth,
            CanvasHeight,
            true
        );
        using SKCanvas canvas = new(background);
        using SKBitmap? framed = poster is null
            ? null
            : CreateFramedArtwork(poster);
        XElement scene = new(
            _template.Node("movie/saga/iconview/background")
        );
        foreach (
            XElement text in scene
                .Elements("text")
                .Where(node => ReadElementKind(node) == "title")
        )
        {
            text.SetAttributeValue("type", "static");
            text.Value = title;
        }

        if (!useFanart)
        {
            scene
                .Elements("image")
                .Where(node => ReadElementKind(node) == "fanart")
                .Remove();
        }

        if (poster is null)
        {
            foreach (XElement image in scene.Elements("image")
                .Where(node => ReadElementKind(node) == "static"))
            {
                DrawImage(
                    canvas,
                    ReadImage(_template.Asset(image.Value, true), true),
                    ReadRectangle(image)
                );
            }
            foreach (XElement text in scene.Elements("text")
                .Where(node => ReadElementKind(node) == "static"))
            {
                DrawText(canvas, text, text.Value);
            }
        }
        else
        {
            DrawScene(canvas, scene, poster, null, framed!, null);
        }
        SaveBackground(
            background,
            Path.Combine(directory, "background")
        );
        foreach (SKBitmap image in _mediaImages.Values)
        {
            image.Dispose();
        }

        _mediaImages.Clear();
    }

    /// <summary>
    /// Ajoute l'estampille du template à l'icône du film choisi.
    /// </summary>
    internal void RenderSagaIcon(string source, string directory)
    {
        using SKBitmap icon = AaiCodec.Load(source);
        using SKCanvas canvas = new(icon);
        XElement stamp = _template.Node("movie/saga/stamp");
        DrawImage(
            canvas,
            ReadImage(_template.Asset(stamp.Attr("image"), true), true),
            ReadRectangle(stamp)
        );
        AaiCodec.Save(icon, Path.Combine(directory, "icon.aai"));
    }

    /// <summary>
    /// Conserve les dimensions des étiquettes et des drapeaux
    /// yaDIS2.
    /// </summary>
    internal void RenderLegacyIndexLabel(
        string directory,
        string caption,
        string kind
    )
    {
        XElement text = _template.Node(
            kind == "alphabet"
                ? "menu/index/text"
                : "menu/index/" + kind + "/text"
        );
        _fonts.TryGetValue(
            NormalizeFontName(text.Attr("font")),
            out SKTypeface? face
        );
        using SKFont font = new(
            face ?? SKTypeface.Default,
            text.Float("size", 24) * 96 / 72
        );
        int width =
            kind == "countries"
                ? 128
                : NavigationWidth(caption, text);
        int height =
            kind == "countries"
                ? 64
                : (int)Math.Round(font.Spacing);
        Directory.CreateDirectory(directory);
        SaveIndexLabel(
            directory,
            caption,
            text,
            width,
            height,
            kind == "countries",
            false
        );
        SaveIndexLabel(
            directory,
            caption,
            text,
            width,
            height,
            kind == "countries",
            true
        );
    }

    private void SaveIndexLabel(
        string directory,
        string caption,
        XElement source,
        int width,
        int height,
        bool country,
        bool selected
    )
    {
        using SKBitmap image = CreateBitmap(width, height);
        using SKCanvas canvas = new(image);
        XElement text = new(source);
        text.SetAttributeValue("x", country ? 64 : 0);
        text.SetAttributeValue("y", country ? 2 : 0);
        text.SetAttributeValue("width", country ? 64 : width);
        text.SetAttributeValue("height", height);
        text.SetAttributeValue(
            "align",
            country ? "right" : "center"
        );
        text.SetAttributeValue(
            "valign",
            country ? "center" : "top"
        );
        text.SetAttributeValue(
            "color",
            source.Attr(selected ? "selectedcolor" : "color")
        );
        if (country)
        {
            DrawImage(
                canvas,
                ReadImage(
                    _template.Asset("flag_" + caption + ".png"),
                    true
                ),
                SKRect.Create(64, 64)
            );
        }

        DrawText(canvas, text, caption);
        AaiCodec.Save(
            image,
            Path.Combine(
                directory,
                selected ? "icon_sel.aai" : "icon.aai"
            )
        );
    }
}
