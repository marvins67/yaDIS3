using System.Net;
using System.Text;

namespace YaDIS3.Tests;

internal sealed class SeriesMetadataHandler : HttpMessageHandler
{
    public List<string> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken
    )
    {
        string path = request.RequestUri!.PathAndQuery;
        Requests.Add(path);
        string body = path.Contains("login", StringComparison.Ordinal)
            ? """{"status":"success","data":{"token":"test"}}"""
            : path.Contains("/episodes/8/extended", StringComparison.Ordinal)
                ? """{"status":"success","data":{"characters":[{"peopleType":"Director","personName":"Réalisateur"}]}}"""
            : path.Contains("/series/42/episodes/", StringComparison.Ordinal)
                ? """{"status":"success","data":{"episodes":[{"id":8,"seasonNumber":1,"number":1,"name":"Épisode","aired":"2020-01-01","score":99}]},"links":{"next":null}}"""
                : """{"status":"success","data":{"id":42,"name":"Série","score":99,"genres":[{"name":"Drame"},{"name":"Comédie"}],"characters":[{"peopleType":"Actor","personName":"Actrice"},{"peopleType":"Guest Star","personName":"Invité"},{"peopleType":"Director","personName":"Autre"},{"peopleType":"Actor","personName":"Actrice"}]}}""";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }
}
