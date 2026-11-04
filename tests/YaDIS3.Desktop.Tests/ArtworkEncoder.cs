using YaDIS3.Core;

namespace YaDIS3.Desktop.Tests;

internal sealed class ArtworkEncoder : IJpegEncoder
{
    public void Encode(string source, string target) =>
        File.Copy(source, target);
}
