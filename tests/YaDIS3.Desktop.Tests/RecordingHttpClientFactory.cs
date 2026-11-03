namespace YaDIS3.Desktop.Tests;

internal sealed class RecordingHttpClientFactory(
    HttpMessageHandler handler
) : IHttpClientFactory
{
    public string? RequestedName { get; private set; }

    public HttpClient CreateClient(string name)
    {
        RequestedName = name;
        return new HttpClient(handler, disposeHandler: false);
    }
}
