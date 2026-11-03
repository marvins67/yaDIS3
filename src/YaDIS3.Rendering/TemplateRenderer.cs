using SkiaSharp;

namespace YaDIS3.Rendering;

/// <summary>
/// Dessine les calques, les polices, les masques et les
/// métadonnées du template Dune.
/// </summary>
public sealed partial class TemplateRenderer : IDisposable
{
    private const int CanvasWidth = 1920;
    private const int CanvasHeight = 1080;

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
    /// Initialise les options de rendu et charge les polices du
    /// template.
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

            string fileKey = NormalizeFontName(
                Path.GetFileNameWithoutExtension(path)
            );
            _fonts.TryAdd(fileKey, face);
            _fonts.TryAdd(
                NormalizeFontName(face.FamilyName),
                face
            );
        }
    }

    /// <summary>
    /// Génère les illustrations d'une fiche, de ses saisons et
    /// de ses épisodes.
    /// </summary>
    public RenderedCard Render(
        MediaContext media,
        string directory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        _cancellation = cancellationToken;
        Directory.CreateDirectory(directory);
        try
        {
            return RenderCard(media, directory, progress);
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
                $"Échec du rendu de la fiche « {media.Title} » "
                    + $"({Path.GetFullPath(media.DataDirectory)}). "
                    + exception.Message,
                exception
            );
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

    /// <summary>
    /// Extension du format de fond sélectionné.
    /// </summary>
    public string BackgroundExtension =>
        _options.AaiBackgrounds ? ".aai" : ".jpg";

    /// <summary>
    /// Libère les ressources possédées et les illustrations
    /// temporaires.
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

    private RenderedCard RenderCard(
        MediaContext media,
        string directory,
        IProgress<string>? progress
    )
    {
        using SKBitmap framed = CreateFramedArtwork(media);

        RenderMediaIcon(media, framed, directory);

        using SKBitmap common = CreateBitmap(
            CanvasWidth,
            CanvasHeight,
            true
        );

        DrawCommonBackground(media, framed, common);

        string preview = Path.Combine(directory, "preview.jpg");

        var episodePreviews = new Dictionary<string, string>(
            StringComparer.Ordinal
        );

        if (!media.IsSeries)
        {
            SaveBackground(
                common,
                Path.Combine(directory, "background")
            );
            SaveJpeg(common, preview, _options.JpegQuality);
            return new(directory, preview, episodePreviews);
        }

        RenderSeriesOverview(
            media,
            framed,
            common,
            directory,
            preview
        );

        using SKBitmap episodeBackground = common.Copy();

        DrawEpisodeBackground(media, framed, episodeBackground);

        SaveBackground(
            episodeBackground,
            Path.Combine(directory, "background2")
        );

        RenderSeriesEpisodes(
            media,
            framed,
            episodeBackground,
            directory,
            episodePreviews,
            progress
        );

        return new(directory, preview, episodePreviews);
    }
}
