namespace YaDIS3.Core;

/// <summary>Recherche des images voisines comme GetMovie de yaDIS2.</summary>
internal static class LocalMovieArtwork
{
    internal static string? Find(MediaCard card, ArtworkKind kind)
    {
        string source = card.TreatmentSource;
        bool folder = Directory.Exists(source);
        string? directory = folder ? source : Path.GetDirectoryName(source);
        if (directory is null || !Directory.Exists(directory))
        {
            return null;
        }
        string prefix = folder ? "" : Path.GetFileNameWithoutExtension(source);
        string[] suffixes = kind == ArtworkKind.Poster
            ? ["cover.jpg", "poster.jpg"] : ["fanart.jpg"];
        foreach (string suffix in suffixes)
        {
            string? match = Directory.EnumerateFiles(directory)
                .FirstOrDefault(file =>
                    Path.GetFileName(file).StartsWith(prefix,
                        StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(file).EndsWith(suffix,
                        StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }
        return null;
    }
}
