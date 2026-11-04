using System.Xml.Linq;

namespace YaDIS3.Core;

public sealed partial class MediaCard
{
    /// <summary>
    /// Catalogue historique ; accepte aussi les anciens fichiers
    /// du portage contenant une seule URL par type.
    /// </summary>
    public IReadOnlyList<ArtworkReference> MovieArtwork =>
        IsSeries || Info?.Element("images") is not { } images
            ? []
            : ReadMovieArtwork(images).ToArray();

    private static IEnumerable<ArtworkReference> ReadMovieArtwork(
        XElement images
    )
    {
        foreach (XElement image in images.Elements())
        {
            string kind = image.Name == "image"
                ? (string?)image.Attribute("type") ?? ""
                : image.Name.LocalName;
            string url = image.Name == "image"
                ? (string?)image.Attribute("url") ?? ""
                : image.Value;
            if (kind is "poster" or "fanart"
                && !string.IsNullOrWhiteSpace(url))
            {
                yield return new(
                    kind == "poster"
                        ? ArtworkKind.Poster
                        : ArtworkKind.Fanart,
                    url
                );
            }
        }
    }
}
