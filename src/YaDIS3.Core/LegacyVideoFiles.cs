namespace YaDIS3.Core;

/// <summary>
/// Extensions de fichiers vidéo reconnues par la découverte des
/// sources de yaDIS2.
/// </summary>
public static class LegacyVideoFiles
{
    private static readonly HashSet<string> s_extensions = new(
        [
            ".iso",
            ".mkv",
            ".mk3d",
            ".ts",
            ".ps",
            ".mpg",
            ".mpeg",
            ".m2ts",
            ".mts",
            ".vob",
            ".avi",
            ".mov",
            ".m4v",
            ".mp4",
            ".qt",
            ".asf",
            ".wmv",
            ".divx",
            ".ogg",
            ".ogm",
            ".ogv",
            ".img",
        ],
        StringComparer.OrdinalIgnoreCase
    );

    /// <summary>
    /// Teste une extension sans tenir compte de sa casse.
    /// </summary>
    public static bool IsVideo(string path) =>
        s_extensions.Contains(Path.GetExtension(path));
}
