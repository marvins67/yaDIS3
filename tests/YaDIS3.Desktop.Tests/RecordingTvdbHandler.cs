using System.Net;
using System.Text;

namespace YaDIS3.Desktop.Tests;

internal sealed class RecordingTvdbHandler(
    HttpStatusCode searchStatus
) : HttpMessageHandler
{
    public List<string> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        string uri = request.RequestUri!.AbsoluteUri;
        Requests.Add(uri);
        bool login = uri.EndsWith(
            "/login",
            StringComparison.Ordinal
        );
        string body = login
            ? "{\"status\":\"success\",\"data\":{\"token\":\"test-only-token\"}}"
            : "{\"status\":\"success\",\"data\":[{\"tvdb_id\":\"123\",\"translations\":{\"fra\":\"Titre français\"},\"year\":\"2023\"}]}";
        return Task.FromResult(
            new HttpResponseMessage(
                login ? HttpStatusCode.OK : searchStatus
            )
            {
                Content = new StringContent(
                    body,
                    Encoding.UTF8,
                    "application/json"
                ),
            }
        );
    }
}
