using YaDIS3.Core;

namespace YaDIS3.Tests;

// Ces scénarios de métadonnées ne doivent pas importer d'image.
internal sealed class UnusedJpegEncoder : IJpegEncoder
{
    public void Encode(string source, string target) =>
        throw new InvalidOperationException(
            "Import d'image inattendu dans ce scénario."
        );
}
