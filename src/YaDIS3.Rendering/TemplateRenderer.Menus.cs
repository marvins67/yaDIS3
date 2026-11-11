using System.Xml.Linq;
using SkiaSharp;

namespace YaDIS3.Rendering;

public sealed partial class TemplateRenderer
{
    /// <summary>
    /// Dessine le fond d'un menu du catalogue avec le template
    /// sélectionné.
    /// </summary>
    public void RenderMenu(
        string directory,
        string kind,
        XElement? panel = null
    )
    {
        XElement? snapshot = panel is null ? null : new(panel);
        Directory.CreateDirectory(directory);
        RenderMenuBackground(
            Path.Combine(directory, "background"),
            canvas => DrawMenuBackground(canvas, kind, snapshot)
        );
    }

    private readonly Dictionary<string, Action<SKCanvas>> _menuBackgrounds = new(
        StringComparer.Ordinal
    );

    private void RenderMenuBackground(string path, Action<SKCanvas> draw)
    {
        using SKBitmap image = CreateBitmap(CanvasWidth, CanvasHeight, true);
        using (SKCanvas canvas = new(image))
        {
            draw(canvas);
        }
        SaveBackground(image, path);
        // Conserver la recette, pas un JPEG décodé : chaque composition repart
        // des sources sans pertes, comme le bitmap de ProcessForm.
        _menuBackgrounds[path] = draw;
    }

    internal void CopyMenuBackground(string directory, string destinationName)
    {
        if (_menuBackgrounds.TryGetValue(
            Path.Combine(directory, "background"), out Action<SKCanvas>? draw))
        {
            _menuBackgrounds[Path.Combine(directory, destinationName)] = draw;
        }
    }

    private void DrawMenuBackground(SKCanvas canvas, string kind, XElement? panel)
    {
        string background = _template
            .Root.Element(kind)
            .Attr(
                "background",
                _template.Root.Element("menu").Attr("background")
            );
        DrawImage(
            canvas,
            ReadImage(_template.Asset(background, true), true),
            SKRect.Create(CanvasWidth, CanvasHeight)
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
                        ReadElementKind(nodeValue) == "static"
                    )
            )
            {
                DrawImage(
                    canvas,
                    ReadImage(
                        _template.Asset(node.Value, true),
                        true
                    ),
                    ReadRectangle(node)
                );
            }
        }

        DrawMenuTexts(canvas, scene);

        XElement? area =
            panel
            ?? (
                kind == "menu"
                    ? _template.Root.XPath("menu/main/area")
                    : null
            );
        if (area?.Attribute("image") is { } file)
        {
            DrawImage(
                canvas,
                ReadImage(
                    _template.Asset(file.Value, true),
                    true
                ),
                ReadRectangle(area)
            );
        }


    }

    private void DrawMenuTexts(SKCanvas canvas, XElement? scene)
    {
        if (scene is null)
        {
            return;
        }
        foreach (XElement text in scene.Elements("text"))
        {
            if (ReadElementKind(text) == "static"
                && string.Equals(text.Attr("language"), _options.Language,
                    StringComparison.OrdinalIgnoreCase))
            {
                DrawText(canvas, text, text.Value);
            }
        }
    }

    /// <summary>
    /// Produit le fond brut du dossier parent en mode basique.
    /// </summary>
    internal void RenderBasicBackground(
        string directory,
        string kind
    )
    {
        using SKBitmap image = ReadImage(
                _template.Asset(
                    _template.Node(kind).Attr("background"),
                    true
                ),
                true
            )!
            .Copy();
        SaveBackground(
            image,
            Path.Combine(directory, "background")
        );
        string obsolete = Path.Combine(
            directory,
            _options.AaiBackgrounds
                ? "background.jpg"
                : "background.aai"
        );
        if (File.Exists(obsolete))
        {
            File.Delete(obsolete);
        }
    }

    /// <summary>
    /// Conserve l'icône d'application utilisée à la racine de
    /// yaDIS2.
    /// </summary>
    public void SaveApplicationIcon(string path)
    {
        using SKBitmap icon =
            ReadImage(_template.Asset("icon.png", true), true)
                ?.Copy()
            ?? CreateBitmap(32, 32);
        AaiCodec.Save(icon, path);
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
        using SKBitmap image = CreateBitmap(width, height);
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

    private void SaveBackground(SKBitmap image, string path)
    {
        if (_options.AaiBackgrounds)
        {
            AaiCodec.Save(image, path + ".aai");
            if (_treatmentPreview is not null)
            {
                File.Delete(path + ".jpg");
            }
        }
        else
        {
            SaveJpeg(image, path + ".jpg", _options.JpegQuality);
            if (_treatmentPreview is not null)
            {
                File.Delete(path + ".aai");
            }
        }
    }

    /// <summary>
    /// Enregistre un JPEG avec la qualité choisie, 95 par
    /// défaut.
    /// </summary>
    public static void SaveJpeg(
        SKBitmap image,
        string path,
        int quality = 95
    )
    {
        using SKImage encoded = SKImage.FromBitmap(image);
        using SKData data = encoded.Encode(
            SKEncodedImageFormat.Jpeg,
            Math.Clamp(quality, 1, 100)
        );
        using FileStream file = File.Create(path);
        data.SaveTo(file);
    }
}
