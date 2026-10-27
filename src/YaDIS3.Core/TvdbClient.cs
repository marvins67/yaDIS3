using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace YaDIS3.Core;

public sealed record TvdbSettings(string Apikey, string Pin)
{
    public static TvdbSettings Load(string file)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(file));
        var key = json.RootElement.TryGetProperty("apikey", out var k) ? k.GetString() ?? "" : "";
        var pin = json.RootElement.TryGetProperty("pin", out var p) ? p.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidDataException("Renseignez apikey dans votre fichier tvdb-v4.json.");
        return new TvdbSettings(key, pin);
    }
}
public sealed record SeriesMatch(long Id, string Name, string Overview, string Year)
{ public string Label => string.IsNullOrEmpty(Year) ? Name : $"{Name} ({Year})"; }
public sealed record EpisodeOrder(string Id, string Code, string Name);
public sealed record TvdbEpisode(int Season, int Number, string Name, string Overview, string Aired, string Image);
public sealed record SeriesDetails(long Id, string Name, string Overview, string FirstAired, string Network, IReadOnlyList<TvdbEpisode> Episodes);

/// <summary>Native async TVDB v4. Credentials and bearer tokens are never logged.</summary>
public sealed class TvdbClient(HttpClient http, TvdbSettings settings)
{
    private static readonly Uri Api = new("https://api4.thetvdb.com/v4/");
    private readonly SemaphoreSlim login = new(1, 1);
    private string? token;
    private DateTimeOffset expires;
    private IReadOnlyList<EpisodeOrder>? types;
    private static JsonElement Child(JsonElement node, string key) => node.ValueKind == JsonValueKind.Object && node.TryGetProperty(key, out var value) ? value : default;
    private static string Text(JsonElement node, string key)
    { var value = Child(node, key); return value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? "" : value.ToString(); }
    private static IEnumerable<JsonElement> Items(JsonElement node) => node.ValueKind == JsonValueKind.Array ? node.EnumerateArray() : [];
    private static int Number(JsonElement node, string key) => int.TryParse(Text(node, key), out var n) ? n : 0;
    private static string Localized(JsonElement node, string field, string language) => Items(Child(Child(node, "translations"), field == "name" ? "nameTranslations" : "overviewTranslations"))
        .Where(t => Text(t, "language") == language).Select(t => Text(t, field)).FirstOrDefault(t => t.Length > 0) ?? Text(node, field);
    public static void ValidateOrder(string value)
    { if (!Regex.IsMatch(value, "^[a-z0-9_-]+$", RegexOptions.CultureInvariant)) throw new ArgumentException("Ordre TVDB invalide."); }
    private static void Language(string value)
    { if (!Regex.IsMatch(value, "^[a-z]{3}$", RegexOptions.CultureInvariant)) throw new ArgumentException("Langue TVDB invalide, par exemple fra."); }
    private async Task<string> TokenAsync(CancellationToken cancellationToken)
    {
        await login.WaitAsync(cancellationToken);
        try
        {
            if (token is not null && expires > DateTimeOffset.UtcNow) return token;
            if (string.IsNullOrWhiteSpace(settings.Apikey)) throw new InvalidOperationException("Clé API TVDB v4 absente.");
            var body = new Dictionary<string, string> { ["apikey"] = settings.Apikey };
            if (!string.IsNullOrWhiteSpace(settings.Pin)) body["pin"] = settings.Pin;
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Api, "login")) { Content = JsonContent.Create(body) };
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Connexion TVDB refusée (HTTP {(int)response.StatusCode}).", null, response.StatusCode);
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var next = Text(Child(json.RootElement, "data"), "token");
            if (Text(json.RootElement, "status") != "success" || next.Length == 0) throw new InvalidDataException("TVDB n'a pas renvoyé de jeton.");
            token = next; expires = DateTimeOffset.UtcNow.AddHours(23); return next;
        }
        finally { login.Release(); }
    }
    private async Task<JsonDocument> GetAsync(string path, CancellationToken cancellationToken)
    {
        var refreshed = false;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var bearer = await TokenAsync(cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Api, path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            request.Headers.UserAgent.ParseAdd("yaDIS3/2.5.0-alpha.4");
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && !refreshed)
            {
                refreshed = true; await login.WaitAsync(cancellationToken);
                try { if (token == bearer) token = null; } finally { login.Release(); }
                continue;
            }
            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 3)
            {
                var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(attempt + 1);
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(delay.TotalSeconds, 1, 30)), cancellationToken); continue;
            }
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"TVDB : HTTP {(int)response.StatusCode}.", null, response.StatusCode);
            var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (Text(json.RootElement, "status") == "success") return json;
            json.Dispose(); throw new InvalidDataException("Réponse TVDB sans statut success.");
        }
        throw new HttpRequestException("TVDB reste temporairement indisponible.");
    }
    public async Task<IReadOnlyList<SeriesMatch>> SearchAsync(string title, string language = "fra", CancellationToken cancellationToken = default)
    {
        Language(language); if (string.IsNullOrWhiteSpace(title)) return [];
        // Keep the existing API query and alternative titles; no title-only filtering.
        using var json = await GetAsync($"search?type=series&query={Uri.EscapeDataString(title)}&language={language}&limit=50", cancellationToken);
        var result = new List<SeriesMatch>();
        foreach (var item in Items(Child(json.RootElement, "data")))
        {
            var idText = Text(item, "tvdb_id"); if (idText.Length == 0) idText = Text(item, "id").Replace("series-", "", StringComparison.Ordinal);
            if (!long.TryParse(idText, out var id) || id <= 0) continue;
            var name = Text(Child(item, "translations"), language); if (name.Length == 0) name = Text(item, "name_translated"); if (name.Length == 0) name = Text(item, "name");
            var plot = Text(Child(item, "overviews"), language); if (plot.Length == 0) plot = Text(item, "overview");
            result.Add(new SeriesMatch(id, name, plot, Text(item, "year")));
        }
        return result;
    }
    private async Task<IReadOnlyList<EpisodeOrder>> TypesAsync(CancellationToken cancellationToken)
    {
        if (types is not null) return types;
        using var json = await GetAsync("seasons/types", cancellationToken);
        var result = Items(Child(json.RootElement, "data")).Select(t => new EpisodeOrder(Text(t, "id"), Text(t, "type"), Text(t, "alternateName") is { Length: > 0 } label ? label : Text(t, "name")))
            .Where(t => Regex.IsMatch(t.Code, "^[a-z0-9_-]+$", RegexOptions.CultureInvariant)).DistinctBy(t => t.Code)
            .Select(t => t with { Name = t.Code switch { "official" => "Diffusion (Aired)", "dvd" => "DVD", "absolute" => "Absolu", _ => t.Name } })
            .OrderBy(t => t.Code switch { "official" => 0, "dvd" => 1, "absolute" => 2, _ => 3 }).ToArray();
        if (result.Length == 0) throw new InvalidDataException("TVDB n'a indiqué aucun type d'ordre.");
        types = result; return result;
    }
    public async Task<IReadOnlyList<EpisodeOrder>> AvailableOrdersAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        var all = await TypesAsync(cancellationToken);
        using var json = await GetAsync($"series/{id}/extended", cancellationToken);
        var series = Child(json.RootElement, "data");
        var available = Items(Child(series, "seasonTypes")).Concat(Items(Child(series, "seasons")).Select(s => Child(s, "type"))).ToArray();
        var result = all.Where(order => available.Any(t => Text(t, "type") == order.Code || Text(t, "id") == order.Id)).ToArray();
        if (result.Length == 0) throw new InvalidDataException("TVDB n'a indiqué aucun ordre disponible pour cette série.");
        return result;
    }
    public async Task<SeriesDetails> SeriesAsync(long id, string order, string language = "fra", CancellationToken cancellationToken = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        ValidateOrder(order); Language(language);
        using var json = await GetAsync($"series/{id}/extended?meta=translations", cancellationToken);
        var series = Child(json.RootElement, "data");
        if (Text(series, "id").Length == 0) throw new InvalidDataException("Métadonnées de série incomplètes.");
        var episodes = new List<TvdbEpisode>(); var prefix = $"series/{id}/episodes/{order}/{language}";
        for (var page = 0; page < 10000; page++)
        {
            JsonDocument document;
            try { document = await GetAsync($"{prefix}?page={page}", cancellationToken); }
            catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.NotFound && page == 0)
            {
                // Fall back on language only, never on the user's chosen episode order.
                prefix = $"series/{id}/episodes/{order}"; document = await GetAsync($"{prefix}?page=0", cancellationToken);
            }
            using (document)
            {
                var payload = Child(Child(document.RootElement, "data"), "episodes");
                if (payload.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Liste d'épisodes TVDB incomplète.");
                foreach (var ep in payload.EnumerateArray()) episodes.Add(new TvdbEpisode(Number(ep, "seasonNumber"), Number(ep, "number"), Localized(ep, "name", language), Localized(ep, "overview", language), Text(ep, "aired"), Text(ep, "image")));
                if (Text(Child(document.RootElement, "links"), "next").Length == 0)
                    return new SeriesDetails(id, Localized(series, "name", language), Localized(series, "overview", language), Text(series, "firstAired"), Text(Child(series, "originalNetwork"), "name"), episodes);
                if (payload.GetArrayLength() == 0) throw new InvalidDataException("Pagination TVDB incohérente.");
            }
        }
        throw new InvalidDataException("Pagination TVDB excessive.");
    }
}
