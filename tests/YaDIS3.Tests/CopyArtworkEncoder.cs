using YaDIS3.Core;

namespace YaDIS3.Tests;

// Préserve les octets des PNG de test pour vérifier les choix.
internal sealed class CopyArtworkEncoder : IJpegEncoder
{
    public void Encode(string source, string target) =>
        File.Copy(source, target);
}
