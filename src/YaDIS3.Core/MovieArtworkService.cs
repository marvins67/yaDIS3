namespace YaDIS3.Core;

/// <summary>
/// Sépare le cache des vignettes du téléchargement des originaux.
/// Un choix ne remplace le brouillon qu'après son import réussi.
/// </summary>
public sealed class MovieArtworkService(
    HttpClient http,
    ImageImporter importer
)
{
    /// <summary>Reprend les tailles et noms du cache yaDIS2.</summary>
    public async Task<string> ThumbnailAsync(
        ArtworkReference image,
        string workspace,
        CancellationToken cancellationToken
    )
    {
        Uri original = OriginalUri(image);
        string name = Path.GetFileName(original.AbsolutePath);
        string prefix = image.Kind == ArtworkKind.Poster
            ? "poster-" : "fanart-";
        string cache = Path.Combine(workspace, "cache");
        Directory.CreateDirectory(cache);
        string target = Path.Combine(cache, prefix + name);
        if (File.Exists(target)
            && new FileInfo(target).Length > 0)
        {
            return target;
        }
        string size = image.Kind == ArtworkKind.Poster
            ? "/w154/" : "/w300/";
        Uri thumbnail = new(original.AbsoluteUri.Replace(
            "/original/", size, StringComparison.Ordinal
        ));
        string temporary = await DownloadAsync(
            thumbnail, cancellationToken
        );
        try
        {
            File.Move(temporary, target, true);
            return target;
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    /// <summary>Importe le choix sans modifier le catalogue.</summary>
    public async Task SelectAsync(
        MediaCard card,
        ArtworkReference image,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(image);
        if (!card.MovieArtwork.Contains(image))
        {
            throw new ArgumentException(
                image.Url, nameof(image)
            );
        }
        string temporary = await DownloadAsync(
            OriginalUri(image), cancellationToken
        );
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            importer.Import(card, image.Kind, temporary);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static Uri OriginalUri(ArtworkReference image)
    {
        ArgumentNullException.ThrowIfNull(image);
        Uri url = new(image.Url, UriKind.Absolute);
        if (image.Kind is not (ArtworkKind.Poster
                or ArtworkKind.Fanart)
            || url.Scheme is not ("https" or "http")
            || url.Host != "image.tmdb.org")
        {
            throw new ArgumentException(
                image.Url, nameof(image)
            );
        }
        return new UriBuilder(url)
        {
            Scheme = "https",
            Port = -1,
        }.Uri;
    }

    private async Task<string> DownloadAsync(
        Uri url,
        CancellationToken cancellationToken
    )
    {
        string temporary = Path.GetTempFileName();
        try
        {
            using Stream source = await http.GetStreamAsync(
                url, cancellationToken
            );
            await using (FileStream target =
                File.Create(temporary))
            {
                await source.CopyToAsync(
                    target, cancellationToken
                );
                if (target.Length == 0)
                {
                    throw new InvalidDataException(
                        url.AbsoluteUri
                    );
                }
            }
            return temporary;
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }
}
