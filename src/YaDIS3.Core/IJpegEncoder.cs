namespace YaDIS3.Core;

/// <summary>
/// Convertit les images importées au format JPEG du workspace.
/// </summary>
public interface IJpegEncoder
{
    /// <summary>
    /// Convertit l'image source en JPEG au chemin demandé.
    /// </summary>
    void Encode(string source, string target);
}
