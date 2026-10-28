using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YaDIS3.Core;

/// <summary>
/// Contains the API key and optional TVDB subscriber PIN.
/// </summary>
public sealed record TvdbSettings(string ApiKey, string Pin)
{
    /// <summary>
    /// Reads the API key and optional PIN from a TVDB JSON
    /// file.
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

/// <summary>
/// Describes one series returned by a TVDB search.
/// </summary>
public sealed record SeriesMatch(
    long Id,
    string Name,
    string Overview,
    string Year
)
{
    /// <summary>
    /// Gets the display label for this selection.
    /// </summary>
    public string Label =>
        string.IsNullOrEmpty(Year) ? Name : $"{Name} ({Year})";
}

/// <summary>
/// Identifies an episode order offered by TVDB.
/// </summary>
public sealed record EpisodeOrder(
    string Id,
    string Code,
    string Name
);

/// <summary>
/// Contains the metadata for one episode in the chosen
/// order.
/// </summary>
public sealed record TvdbEpisode(
    int Season,
    int Number,
    string Name,
    string Overview,
    string Aired,
    string Image
);

/// <summary>
/// Contains series metadata and episodes returned by TVDB.
/// </summary>
public sealed record SeriesDetails(
    long Id,
    string Name,
    string Overview,
    string FirstAired,
    string Network,
    IReadOnlyList<TvdbEpisode> Episodes
);

/// <summary>
/// Queries TVDB v4 without logging credentials or bearer
/// tokens.
/// </summary>
public sealed class TvdbClient(
    HttpClient http,
    TvdbSettings settings
)
{
    private static readonly Uri s_api = new(
        "https://api4.thetvdb.com/v4/"
    );

    private readonly SemaphoreSlim _login = new(1, 1);

    private string? _token;

    private DateTimeOffset _expires;

    private IReadOnlyList<EpisodeOrder>? _types;

    private static JsonElement Child(
        JsonElement node,
        string key
    )
    {
        return
            node.ValueKind == JsonValueKind.Object
            && node.TryGetProperty(key, out var value)
            ? value
            : default;
    }

    private static string Text(JsonElement node, string key)
    {
        JsonElement value = Child(node, key);
        return
            value.ValueKind
                is JsonValueKind.Undefined
                    or JsonValueKind.Null
            ? ""
            : value.ToString();
    }

    private static IEnumerable<JsonElement> Items(
        JsonElement node
    )
    {
        return node.ValueKind == JsonValueKind.Array
            ? node.EnumerateArray()
            : [];
    }

    private static int Number(JsonElement node, string key)
    {
        return int.TryParse(Text(node, key), out var number)
            ? number
            : 0;
    }

    private static string Localized(
        JsonElement node,
        string field,
        string language
    )
    {
        return Items(
                    Child(
                        Child(node, "translations"),
                        field == "name"
                            ? "nameTranslations"
                            : "overviewTranslations"
                    )
                )
                .Where(translation =>
                    Text(translation, "language") == language
                )
                .Select(translation => Text(translation, field))
                .FirstOrDefault(itemValue =>
                    itemValue.Length > 0
                )
            ?? Text(node, field);
    }

    /// <summary>
    /// Rejects invalid TVDB episode-order identifiers.
    /// </summary>
    public static void ValidateOrder(string value)
    {
        if (
            !Regex.IsMatch(
                value,
                "^[a-z0-9_-]+$",
                RegexOptions.CultureInvariant
            )
        )
        {
            throw new ArgumentException("Ordre TVDB invalide.");
        }
    }

    private static void Language(string value)
    {
        if (
            !Regex.IsMatch(
                value,
                "^[a-z]{3}$",
                RegexOptions.CultureInvariant
            )
        )
        {
            throw new ArgumentException(
                "Langue TVDB invalide, par exemple fra."
            );
        }
    }

    private async Task<string> TokenAsync(
        CancellationToken cancellationToken
    )
    {
        await _login.WaitAsync(cancellationToken);
        try
        {
            if (
                _token is not null
                && _expires > DateTimeOffset.UtcNow
            )
            {
                return _token;
            }

            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                throw new InvalidOperationException(
                    "Clé API TVDB v4 absente."
                );
            }

            var body = new Dictionary<string, string>
            {
                ["apikey"] = settings.ApiKey,
            };
            if (!string.IsNullOrWhiteSpace(settings.Pin))
            {
                body["pin"] = settings.Pin;
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(s_api, "login")
            )
            {
                Content = JsonContent.Create(body),
            };
            using HttpResponseMessage response =
                await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Connexion TVDB refusée (HTTP {(int)response.StatusCode}).",
                    null,
                    response.StatusCode
                );
            }

            using JsonDocument json =
                await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(
                        cancellationToken
                    ),
                    cancellationToken: cancellationToken
                );
            string next = Text(
                Child(json.RootElement, "data"),
                "token"
            );
            if (
                Text(json.RootElement, "status") != "success"
                || next.Length == 0
            )
            {
                throw new InvalidDataException(
                    "TVDB n'a pas renvoyé de jeton."
                );
            }

            _token = next;
            _expires = DateTimeOffset.UtcNow.AddHours(23);
            return next;
        }
        finally
        {
            _login.Release();
        }
    }

    private async Task<JsonDocument> GetAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        var refreshed = false;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            string bearer = await TokenAsync(cancellationToken);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri(s_api, path)
            );
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", bearer);
            request.Headers.UserAgent.ParseAdd(
                "yaDIS3/2.5.0-alpha.4"
            );
            using HttpResponseMessage response =
                await http.SendAsync(request, cancellationToken);
            if (
                response.StatusCode
                    == HttpStatusCode.Unauthorized
                && !refreshed
            )
            {
                refreshed = true;
                await _login.WaitAsync(cancellationToken);
                try
                {
                    if (_token == bearer)
                    {
                        _token = null;
                    }
                }
                finally
                {
                    _login.Release();
                }

                continue;
            }

            if (
                response.StatusCode
                    == HttpStatusCode.TooManyRequests
                && attempt < 3
            )
            {
                TimeSpan delay =
                    response.Headers.RetryAfter?.Delta
                    ?? TimeSpan.FromSeconds(attempt + 1);
                await Task.Delay(
                    TimeSpan.FromSeconds(
                        Math.Clamp(delay.TotalSeconds, 1, 30)
                    ),
                    cancellationToken
                );
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"TVDB : HTTP {(int)response.StatusCode}.",
                    null,
                    response.StatusCode
                );
            }

            JsonDocument json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(
                    cancellationToken
                ),
                cancellationToken: cancellationToken
            );
            if (Text(json.RootElement, "status") == "success")
            {
                return json;
            }

            json.Dispose();
            throw new InvalidDataException(
                "Réponse TVDB sans statut success."
            );
        }

        throw new HttpRequestException(
            "TVDB reste temporairement indisponible."
        );
    }

    /// <summary>
    /// Searches TVDB series titles using the configured
    /// language.
    /// </summary>
    public async Task<IReadOnlyList<SeriesMatch>> SearchAsync(
        string title,
        string language = "fra",
        CancellationToken cancellationToken = default
    )
    {
        Language(language);
        if (string.IsNullOrWhiteSpace(title))
        {
            return [];
        }

        // Keep the existing API query and alternative titles; no
        // title-only filtering.
        using JsonDocument json = await GetAsync(
            $"search?type=series&query={Uri.EscapeDataString(title)}&language={language}&limit=50",
            cancellationToken
        );
        var result = new List<SeriesMatch>();
        foreach (
            JsonElement item in Items(
                Child(json.RootElement, "data")
            )
        )
        {
            string idText = Text(item, "tvdb_id");
            if (idText.Length == 0)
            {
                idText = Text(item, "id")
                    .Replace(
                        "series-",
                        "",
                        StringComparison.Ordinal
                    );
            }

            if (!long.TryParse(idText, out var id) || id <= 0)
            {
                continue;
            }

            string name = Text(
                Child(item, "translations"),
                language
            );
            if (name.Length == 0)
            {
                name = Text(item, "name_translated");
            }

            if (name.Length == 0)
            {
                name = Text(item, "name");
            }

            string plot = Text(
                Child(item, "overviews"),
                language
            );
            if (plot.Length == 0)
            {
                plot = Text(item, "overview");
            }

            result.Add(
                new SeriesMatch(
                    id,
                    name,
                    plot,
                    Text(item, "year")
                )
            );
        }

        return result;
    }

    private async Task<IReadOnlyList<EpisodeOrder>> TypesAsync(
        CancellationToken cancellationToken
    )
    {
        if (_types is not null)
        {
            return _types;
        }

        using JsonDocument json = await GetAsync(
            "seasons/types",
            cancellationToken
        );
        var result = Items(Child(json.RootElement, "data"))
            .Select(translation => new EpisodeOrder(
                Text(translation, "id"),
                Text(translation, "type"),
                Text(translation, "alternateName")
                    is { Length: > 0 } label
                    ? label
                    : Text(translation, "name")
            ))
            .Where(orderValue =>
                Regex.IsMatch(
                    orderValue.Code,
                    "^[a-z0-9_-]+$",
                    RegexOptions.CultureInvariant
                )
            )
            .DistinctBy(orderValue => orderValue.Code)
            .Select(orderValue =>
                orderValue with
                {
                    Name = orderValue.Code switch
                    {
                        "official" => "Diffusion (Aired)",
                        "dvd" => "DVD",
                        "absolute" => "Absolu",
                        _ => orderValue.Name,
                    },
                }
            )
            .OrderBy(orderValue =>
                orderValue.Code switch
                {
                    "official" => 0,
                    "dvd" => 1,
                    "absolute" => 2,
                    _ => 3,
                }
            )
            .ToArray();
        if (result.Length == 0)
        {
            throw new InvalidDataException(
                "TVDB n'a indiqué aucun type d'ordre."
            );
        }

        _types = result;
        return result;
    }

    /// <summary>
    /// Gets the episode orders available for the selected
    /// series.
    /// </summary>
    public async Task<
        IReadOnlyList<EpisodeOrder>
    > AvailableOrdersAsync(
        long id,
        CancellationToken cancellationToken = default
    )
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        IReadOnlyList<EpisodeOrder> all = await TypesAsync(
            cancellationToken
        );
        using JsonDocument json = await GetAsync(
            $"series/{id}/extended",
            cancellationToken
        );
        JsonElement series = Child(json.RootElement, "data");
        var available = Items(Child(series, "seasonTypes"))
            .Concat(
                Items(Child(series, "seasons"))
                    .Select(season => Child(season, "type"))
            )
            .ToArray();
        var result = all.Where(order =>
                available.Any(translation =>
                    Text(translation, "type") == order.Code
                    || Text(translation, "id") == order.Id
                )
            )
            .ToArray();
        if (result.Length == 0)
        {
            throw new InvalidDataException(
                "TVDB n'a indiqué aucun ordre disponible pour cette série."
            );
        }

        return result;
    }

    /// <summary>
    /// Loads series details and every episode in the requested
    /// order.
    /// </summary>
    public async Task<SeriesDetails> SeriesAsync(
        long id,
        string order,
        string language = "fra",
        CancellationToken cancellationToken = default
    )
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        ValidateOrder(order);
        Language(language);
        using JsonDocument json = await GetAsync(
            $"series/{id}/extended?meta=translations",
            cancellationToken
        );
        JsonElement series = Child(json.RootElement, "data");
        if (Text(series, "id").Length == 0)
        {
            throw new InvalidDataException(
                "Métadonnées de série incomplètes."
            );
        }

        var episodes = new List<TvdbEpisode>();
        string prefix =
            $"series/{id}/episodes/{order}/{language}";
        for (var page = 0; page < 10000; page++)
        {
            JsonDocument document;
            try
            {
                document = await GetAsync(
                    $"{prefix}?page={page}",
                    cancellationToken
                );
            }
            catch (HttpRequestException exception)
                when (exception.StatusCode
                        == HttpStatusCode.NotFound
                    && page == 0
                )
            {
                // Fall back on language only, never on the
                // user's chosen episode order.
                prefix = $"series/{id}/episodes/{order}";
                document = await GetAsync(
                    $"{prefix}?page=0",
                    cancellationToken
                );
            }

            using (document)
            {
                JsonElement payload = Child(
                    Child(document.RootElement, "data"),
                    "episodes"
                );
                if (payload.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException(
                        "Liste d'épisodes TVDB incomplète."
                    );
                }

                foreach (
                    JsonElement episode in payload.EnumerateArray()
                )
                {
                    episodes.Add(
                        new TvdbEpisode(
                            Number(episode, "seasonNumber"),
                            Number(episode, "number"),
                            Localized(episode, "name", language),
                            Localized(
                                episode,
                                "overview",
                                language
                            ),
                            Text(episode, "aired"),
                            Text(episode, "image")
                        )
                    );
                }

                if (
                    Text(
                        Child(document.RootElement, "links"),
                        "next"
                    ).Length == 0
                )
                {
                    return new SeriesDetails(
                        id,
                        Localized(series, "name", language),
                        Localized(series, "overview", language),
                        Text(series, "firstAired"),
                        Text(
                            Child(series, "originalNetwork"),
                            "name"
                        ),
                        episodes
                    );
                }

                if (payload.GetArrayLength() == 0)
                {
                    throw new InvalidDataException(
                        "Pagination TVDB incohérente."
                    );
                }
            }
        }

        throw new InvalidDataException(
            "Pagination TVDB excessive."
        );
    }
}
