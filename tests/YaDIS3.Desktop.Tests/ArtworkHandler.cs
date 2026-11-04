using System.Net;

namespace YaDIS3.Desktop.Tests;

internal sealed class ArtworkHandler : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];
    public bool FailFirstThumbnail { get; init; }

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
        bool fail = FailFirstThumbnail
            && uri.AbsolutePath == "/t/p/w300/first.jpg";
        return Task.FromResult(new HttpResponseMessage(
            fail ? HttpStatusCode.NotFound : HttpStatusCode.OK
        )
        {
            Content = new ByteArrayContent(Image),
        });
    }
}
