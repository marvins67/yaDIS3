using System.Text.Json;

namespace YaDIS3.Core;

/// <summary>
/// Clé d'API et éventuel PIN d'abonné TVDB.
/// </summary>
public sealed record TvdbSettings(string ApiKey, string Pin)
{
    /// <summary>
    /// Lit la clé d'API et le PIN facultatif dans un fichier
    /// JSON TVDB.
    /// </summary>
    public static TvdbSettings Load(string file)
    {
        using JsonDocument json = JsonDocument.Parse(
            File.ReadAllText(file)
        );
        string key = json.RootElement.TryGetProperty(
            "apikey",
            out var keyElement
        )
            ? keyElement.GetString() ?? ""
            : "";
        string pin = json.RootElement.TryGetProperty(
            "pin",
            out var itemValue
        )
            ? itemValue.GetString() ?? ""
            : "";
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidDataException(
                "Renseignez apikey dans votre fichier tvdb-v4.json."
            );
        }

        return new TvdbSettings(key, pin);
    }
}
