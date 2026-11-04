using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace YaDIS3.Tests;

internal sealed class ArtworkHttpHandler : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];
    public bool FailDownloads { get; set; }
    public bool PreferredArtwork { get; init; }

    public static byte[] Image => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAFElEQVR4nGMMmHaJARtgwio6aCUAWuUByCfwExEAAAAASUVORK5CYII="
    );

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        Uri uri = request.RequestUri!;
        Requests.Add(uri);
        if (uri.Host == "image.tmdb.org")
        {
            return Task.FromResult(new HttpResponseMessage(
                FailDownloads
                    ? HttpStatusCode.ServiceUnavailable
                    : HttpStatusCode.OK
            )
            {
                Content = new ByteArrayContent(Image),
            });
        }
        string name = uri.AbsolutePath.EndsWith(
            "/images", StringComparison.Ordinal
        ) ? "movie-images.json" : "movie.json";
        string body = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "Metadata", name
        ));
        if (PreferredArtwork && name == "movie.json")
        {
            JsonNode movie = JsonNode.Parse(body)!;
            movie["poster_path"] = "/preferred-poster.jpg";
            movie["backdrop_path"] = "/preferred-fanart.jpg";
            body = movie.ToJsonString();
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }
}
