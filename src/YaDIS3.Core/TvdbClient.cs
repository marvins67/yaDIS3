using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YaDIS3.Core;

/// <summary>
/// Interroge TVDB v4 sans journaliser les identifiants ni les
/// jetons.
/// </summary>
public sealed partial class TvdbClient(
    HttpClient http,
    TvdbSettings settings
) : IDisposable
{
    private static readonly Uri s_api = new(
        "https://api4.thetvdb.com/v4/"
    );

    private readonly SemaphoreSlim _login = new(1, 1);

    private string? _token;

    private DateTimeOffset _expires;

    private IReadOnlyList<EpisodeOrder>? _types;

    private static JsonElement ReadChild(
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

    private static string ReadText(JsonElement node, string key)
    {
        JsonElement value = ReadChild(node, key);
        return
            value.ValueKind
                is JsonValueKind.Undefined
                    or JsonValueKind.Null
            ? ""
            : value.ToString();
    }

    private static JsonElement.ArrayEnumerator ReadItems(
        JsonElement node
    )
    {
        return node.ValueKind == JsonValueKind.Array
            ? node.EnumerateArray()
            : default;
    }

    private static int ReadNumber(JsonElement node, string key)
    {
        return int.TryParse(ReadText(node, key), out var number)
            ? number
            : 0;
    }

    private static string ReadTranslation(
        JsonElement node,
        string field,
        string language
    )
    {
        return ReadItems(
                    ReadChild(
                        ReadChild(node, "translations"),
                        field == "name"
                            ? "nameTranslations"
                            : "overviewTranslations"
                    )
                )
                .Where(translation =>
                    ReadText(translation, "language") == language
                )
                .Select(translation =>
                    ReadText(translation, field)
                )
                .FirstOrDefault(itemValue =>
                    itemValue.Length > 0
                )
            ?? ReadText(node, field);
    }

    /// <summary>
    /// Refuse les identifiants d'ordre d'épisodes TVDB
    /// invalides.
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

    private static void ValidateLanguage(string value)
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

    /// <summary>
    /// Recherche les titres de séries TVDB dans la langue
    /// configurée.
    /// </summary>
    public async Task<IReadOnlyList<SeriesMatch>> SearchAsync(
        string title,
        string language = "fra",
        CancellationToken cancellationToken = default
    )
    {
        ValidateLanguage(language);
        if (string.IsNullOrWhiteSpace(title))
        {
            return [];
        }

        // Conserver la recherche et ses titres alternatifs, sans filtrer sur le seul titre.
        using JsonDocument json = await GetAsync(
            $"search?type=series&query={Uri.EscapeDataString(title)}&language={language}&limit=50",
            cancellationToken
        );
        var result = new List<SeriesMatch>();
        foreach (
            JsonElement item in ReadItems(
                ReadChild(json.RootElement, "data")
            )
        )
        {
            string idText = ReadText(item, "tvdb_id");
            if (idText.Length == 0)
            {
                idText = ReadText(item, "id")
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

            string name = ReadText(
                ReadChild(item, "translations"),
                language
            );
            if (name.Length == 0)
            {
                name = ReadText(item, "name_translated");
            }

            if (name.Length == 0)
            {
                name = ReadText(item, "name");
            }

            string plot = ReadText(
                ReadChild(item, "overviews"),
                language
            );
            if (plot.Length == 0)
            {
                plot = ReadText(item, "overview");
            }

            result.Add(
                new SeriesMatch(
                    id,
                    name,
                    plot,
                    ReadText(item, "year"),
                    ReadText(item, "image_url")
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
        var result = ReadItems(
                ReadChild(json.RootElement, "data")
            )
            .Select(translation => new EpisodeOrder(
                ReadText(translation, "id"),
                ReadText(translation, "type"),
                ReadText(translation, "alternateName")
                    is { Length: > 0 } label
                    ? label
                    : ReadText(translation, "name")
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
    /// Obtient les ordres d'épisodes proposés pour la série
    /// sélectionnée.
    /// </summary>
    public async Task<
        IReadOnlyList<EpisodeOrder>
    > AvailableOrdersAsync(
        long id,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        IReadOnlyList<EpisodeOrder> all = await TypesAsync(
            cancellationToken
        );
        using JsonDocument json = await GetAsync(
            $"series/{id}/extended",
            cancellationToken
        );
        JsonElement series = ReadChild(json.RootElement, "data");
        var available = ReadItems(
                ReadChild(series, "seasonTypes")
            )
            .Concat(
                ReadItems(ReadChild(series, "seasons"))
                    .Select(season => ReadChild(season, "type"))
            )
            .ToArray();
        var result = all.Where(order =>
                available.Any(translation =>
                    ReadText(translation, "type") == order.Code
                    || ReadText(translation, "id") == order.Id
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
    /// Charge la série et tous ses épisodes dans l'ordre
    /// demandé.
    /// </summary>
    public Task<SeriesDetails> SeriesAsync(
        long id, string order, string language = "fra",
        CancellationToken cancellationToken = default
    ) => SeriesAsync(id, order, language, null, cancellationToken);

    /// <summary>Charge les détails des seuls épisodes demandés.</summary>
    public async Task<SeriesDetails> SeriesAsync(
        long id,
        string order,
        string language,
        IReadOnlySet<(int Season, int Number)>? requestedEpisodes,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        ValidateOrder(order);
        ValidateLanguage(language);
        using JsonDocument json = await GetAsync(
            $"series/{id}/extended?meta=translations",
            cancellationToken
        );
        JsonElement series = ReadChild(json.RootElement, "data");
        if (ReadText(series, "id").Length == 0)
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
                JsonElement payload = ReadChild(
                    ReadChild(document.RootElement, "data"),
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
                            ReadNumber(episode, "seasonNumber"),
                            ReadNumber(episode, "number"),
                            ReadTranslation(
                                episode,
                                "name",
                                language
                            ),
                            ReadTranslation(
                                episode,
                                "overview",
                                language
                            ),
                            ReadText(episode, "aired"),
                            ReadText(episode, "image")
                        )
                        {
                            Director = requestedEpisodes is null
                                || requestedEpisodes.Contains((
                                    ReadNumber(episode, "seasonNumber"),
                                    ReadNumber(episode, "number")))
                                ? await EpisodeDirectorsAsync(episode, cancellationToken)
                                : "",
                        }
                    );
                }

                if (
                    ReadText(
                        ReadChild(document.RootElement, "links"),
                        "next"
                    ).Length == 0
                )
                {
                    return new SeriesDetails(
                        id,
                        ReadTranslation(
                            series,
                            "name",
                            language
                        ),
                        ReadTranslation(
                            series,
                            "overview",
                            language
                        ),
                        ReadText(series, "firstAired"),
                        ReadText(
                            ReadChild(series, "originalNetwork"),
                            "name"
                        ),
                        episodes
                    )
                    {
                        // score désigne la popularité TVDB, pas
                        // la note du public ; Rating reste vide.
                        Genres = ReadNames(series, "genres"),
                        Actors = ReadPeople(series, "actor", "guest"),
                        Artwork = ReadSeriesArtwork(
                            series,
                            language
                        ),
                    };
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

    private static Dictionary<string, string> ReadSeriesArtwork(
        JsonElement series,
        string language
    )
    {
        Dictionary<string, string> artwork = new(
            StringComparer.Ordinal
        );
        JsonElement images = ReadChild(series, "artworks");
        if (images.ValueKind != JsonValueKind.Array)
        {
            return artwork;
        }
        foreach (
            JsonElement image in images
                .EnumerateArray()
                .OrderBy(item =>
                    ReadText(item, "language") == language
                        ? 0
                        : 1
                )
        )
        {
            string name = ReadNumber(image, "type") switch
            {
                1 => "banner",
                2 => "poster",
                3 => "fanart",
                _ => "",
            };
            string url = ReadText(image, "image");
            if (name.Length > 0 && url.Length > 0)
            {
                artwork.TryAdd(name, url);
            }
        }
        return artwork;
    }

    /// <summary>
    /// Libère le verrou de connexion sans fermer le client HTTP
    /// injecté.
    /// </summary>
    public void Dispose()
    {
        _login.Dispose();
    }
}
