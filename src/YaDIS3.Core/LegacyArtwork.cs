using System.Security.Cryptography;
using System.Text;

namespace YaDIS3.Core;

/// <summary>
/// Encode les URL des illustrations selon le format du cache
/// existant.
/// </summary>
public static class LegacyArtwork
{
    // Encodage réversible identique au pont TVDB de la version 2.4.

    /// <summary>
    /// Encode une URL d'illustration selon le cache historique
    /// des épisodes.
    /// </summary>
    public static string Encode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        Uri url = Uri.TryCreate(
            value,
            UriKind.Absolute,
            out var absolute
        )
            ? absolute
            : new Uri(
                new Uri("https://artworks.thetvdb.com/"),
                value.TrimStart('/')
            );
        if (url.Scheme is not ("http" or "https"))
        {
            throw new InvalidDataException(
                "URL d'image invalide."
            );
        }

        byte[] bytes = Encoding.UTF8.GetBytes(url.AbsoluteUri);
        return $"episodes/v4url_{Convert.ToHexString(bytes)}/{Convert.ToHexString(SHA256.HashData(bytes).AsSpan(0, 8))}-1.jpg";
    }
}
