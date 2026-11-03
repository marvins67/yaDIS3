using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>Relie le traitement aux fournisseurs existants.</summary>
public sealed class OnlineTreatmentMetadata(
    HttpClient http,
    TvdbClient? tvdb,
    string? tmdbToken,
    string language,
    ImageImporter importer
) : ITreatmentMetadata
{
    private string TvdbLanguage =>
        language == "fr" ? "fra" : "eng";

    /// <inheritdoc />
    public async Task<SeriesDetails?> FindSeriesAsync(
        MediaCard card,
        CancellationToken cancellationToken
    )
    {
        TvdbClient client =
            tvdb
            ?? throw new InvalidOperationException(
                "Configurez TVDB avant de traiter cette série."
            );
        if (!long.TryParse(card.TvdbId, out long id) || id <= 0)
        {
            IReadOnlyList<SeriesMatch> matches =
                await client.SearchAsync(
                    card.FolderName,
                    TvdbLanguage,
                    cancellationToken
                );
            if (matches.Count == 0)
            {
                return null;
            }
            id = matches[0].Id;
        }
        return await client.SeriesAsync(
            id,
            card.Order,
            TvdbLanguage,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<XElement?> FindMovieAsync(
        MediaCard card,
        CancellationToken cancellationToken
    )
    {
        string? id = await FindMovieIdAsync(
            card,
            cancellationToken
        );
        if (id is null)
        {
            return null;
        }
        using JsonDocument movie = await GetMovieJsonAsync(
            $"movie/{id}?language={language}&append_to_response=credits",
            cancellationToken
        );
        return CreateMovieInfo(movie.RootElement);
    }

    private async Task<string?> FindMovieIdAsync(
        MediaCard card,
        CancellationToken cancellationToken
    )
    {
        XElement? info = card.TreatmentInfo;
        string? id = info?.Element("id")?.Value;
        string? source = info?.Element("source")?.Value;
        if (
            source is null or "" or "0"
            && long.TryParse(id, out long number)
            && number > 0
        )
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }
        string? imdb = info?.Element("imdb")?.Value;
        if (
            !string.IsNullOrEmpty(imdb)
            && imdb.StartsWith("tt", StringComparison.Ordinal)
        )
        {
            using JsonDocument match = await GetMovieJsonAsync(
                $"find/{Uri.EscapeDataString(imdb)}?external_source=imdb_id",
                cancellationToken
            );
            return FirstMovieId(match, "movie_results");
        }
        string title = card.FolderName;
        if (LegacyVideoFiles.IsVideo(title))
        {
            title = Path.GetFileNameWithoutExtension(title)
                .Replace('.', ' ');
        }
        title = Regex
            .Replace(
                title,
                @"\([^()]+\)|\[[^\[\]]+\]|\{[^{}]+\}",
                ""
            )
            .Trim();
        string query = Uri.EscapeDataString(title);
        using JsonDocument search = await GetMovieJsonAsync(
            $"search/movie?query={query}&language={language}",
            cancellationToken
        );
        return FirstMovieId(search, "results");
    }

    private static string? FirstMovieId(
        JsonDocument json,
        string list
    )
    {
        JsonElement results = json.RootElement.GetProperty(list);
        return results.GetArrayLength() == 0
            ? null
            : results[0]
                .GetProperty("id")
                .GetInt64()
                .ToString(CultureInfo.InvariantCulture);
    }

    private async Task<JsonDocument> GetMovieJsonAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(tmdbToken))
        {
            throw new InvalidOperationException(
                "Renseignez YADIS_TMDB_TOKEN avec le jeton de lecture TMDB."
            );
        }
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "https://api.themoviedb.org/3/" + path
        );
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", tmdbToken);
        using HttpResponseMessage response =
            await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream body =
            await response.Content.ReadAsStreamAsync(
                cancellationToken
            );
        return await JsonDocument.ParseAsync(
            body,
            cancellationToken: cancellationToken
        );
    }

    private static XElement CreateMovieInfo(JsonElement movie)
    {
        string date = Value(movie, "release_date");
        return new XElement(
            "info",
            new XElement("id", Value(movie, "id")),
            new XElement("imdb", Value(movie, "imdb_id")),
            new XElement("source", "0"),
            new XElement("title", Value(movie, "title")),
            new XElement(
                "original_title",
                Value(movie, "original_title")
            ),
            new XElement("tagline", Value(movie, "tagline")),
            new XElement("plot", Value(movie, "overview")),
            new XElement(
                "year",
                date.Length >= 4 ? date[..4] : ""
            ),
            new XElement("runtime", Value(movie, "runtime")),
            new XElement("rating", Value(movie, "vote_average")),
            new XElement(
                "genres",
                Names(movie.GetProperty("genres"), "genre")
            ),
            new XElement(
                "actors",
                Names(
                    movie
                        .GetProperty("credits")
                        .GetProperty("cast"),
                    "actor"
                )
            ),
            new XElement(
                "images",
                new XElement(
                    "poster",
                    MovieImage(Value(movie, "poster_path"))
                ),
                new XElement(
                    "fanart",
                    MovieImage(Value(movie, "backdrop_path"))
                )
            )
        );
    }

    private static IEnumerable<XElement> Names(
        JsonElement list,
        string element
    ) =>
        list.EnumerateArray()
            .Select(item => new XElement(
                element,
                Value(item, "name")
            ));

    private static string Value(JsonElement item, string name) =>
        item.TryGetProperty(name, out JsonElement value)
            ? value.ToString()
            : "";

    private static string MovieImage(string path) =>
        path.Length == 0
            ? ""
            : "https://image.tmdb.org/t/p/original" + path;

    /// <inheritdoc />
    public async Task CompleteArtworkAsync(
        MediaCard card,
        CancellationToken cancellationToken
    )
    {
        foreach (
            ArtworkKind kind in new[]
            {
                ArtworkKind.Poster,
                ArtworkKind.Banner,
                ArtworkKind.Fanart,
            }
        )
        {
            string name = kind.ToString().ToLowerInvariant();
            string? image = card
                .TreatmentInfo?.Element("images")
                ?.Element(name)
                ?.Value;
            await ImportMissingAsync(
                card,
                kind,
                name,
                image,
                null,
                cancellationToken
            );
        }
        foreach (Episode episode in card.Episodes)
        {
            await ImportMissingAsync(
                card,
                ArtworkKind.Episode,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{episode.Season}x{episode.Number}"
                ),
                episode.Node.Element("image")?.Value,
                episode,
                cancellationToken
            );
        }
    }

    private async Task ImportMissingAsync(
        MediaCard card,
        ArtworkKind kind,
        string name,
        string? reference,
        Episode? episode,
        CancellationToken cancellationToken
    )
    {
        if (File.Exists(card.ArtworkPath(name + ".jpg")))
        {
            return;
        }
        string local = Path.Combine(
            card.TreatmentSource,
            name + ".jpg"
        );
        if (
            Directory.Exists(card.TreatmentSource)
            && File.Exists(local)
        )
        {
            importer.ImportAutomatic(card, kind, local, episode);
            return;
        }
        if (string.IsNullOrWhiteSpace(reference))
        {
            return;
        }
        Uri url = ArtworkUri(reference);
        string temporary = Path.GetTempFileName();
        try
        {
            using Stream image = await http.GetStreamAsync(
                url,
                cancellationToken
            );
            await using (
                FileStream output = File.Create(temporary)
            )
            {
                await image.CopyToAsync(
                    output,
                    cancellationToken
                );
            }
            importer.ImportAutomatic(
                card,
                kind,
                temporary,
                episode
            );
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static Uri ArtworkUri(string reference)
    {
        const string EncodedArtworkPrefix = "episodes/v4url_";
        if (
            reference.StartsWith(
                EncodedArtworkPrefix,
                StringComparison.Ordinal
            )
        )
        {
            string encoded = reference[
                EncodedArtworkPrefix.Length..
            ]
                .Split('/')[0];
            reference = Encoding.UTF8.GetString(
                Convert.FromHexString(encoded)
            );
        }
        Uri url = new(
            new Uri("https://artworks.thetvdb.com/banners/"),
            reference
        );
        if (
            url.Host
            is not (
                "artworks.thetvdb.com"
                or "image.tmdb.org"
                or "thetvdb.com"
                or "www.thetvdb.com"
            )
        )
        {
            throw new InvalidDataException(
                "Hôte d'illustration non reconnu."
            );
        }
        return new UriBuilder(url)
        {
            Scheme = "https",
            Port = -1,
        }.Uri;
    }
}
