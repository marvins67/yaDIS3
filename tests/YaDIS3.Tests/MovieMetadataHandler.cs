using System.Net;
using System.Text;

namespace YaDIS3.Tests;

internal sealed class MovieMetadataHandler(string movie)
    : HttpMessageHandler
{
    public List<string> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        string uri = request.RequestUri!.AbsoluteUri;
        Requests.Add(uri);
        return Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    uri.Contains(
                        "search/movie",
                        StringComparison.Ordinal
                    )
                        ? "{\"results\":[{\"id\":42}]}"
                        : movie,
                    Encoding.UTF8,
                    "application/json"
                ),
            }
        );
    }
}
