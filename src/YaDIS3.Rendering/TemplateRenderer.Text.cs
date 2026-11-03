using System.Text.RegularExpressions;
using System.Xml.Linq;
using SkiaSharp;

namespace YaDIS3.Rendering;

public sealed partial class TemplateRenderer
{
    private static string NormalizeFontName(string name)
    {
        return Regex
            .Replace(name, "[^a-zA-Z0-9]", "")
            .ToLowerInvariant();
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
        SKRect rectangleValue = ReadRectangle(specification);
        if (
            rectangleValue.Width <= 0
            || rectangleValue.Height <= 0
        )
        {
            return;
        }

        string family = specification.Attr("font");
        _fonts.TryGetValue(
            NormalizeFontName(family),
            out var face
        );
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
        List<string> lines = WrapText(
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

    // GDI avance d'un nombre entier de pixels par caractère latin.
    // Conserver les contours sans étirer la chaîne : le titre ScoutCond 48pt
    // fourni occupe ainsi 653 pixels au lieu de 638.

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

    private static List<string> WrapText(
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
}
