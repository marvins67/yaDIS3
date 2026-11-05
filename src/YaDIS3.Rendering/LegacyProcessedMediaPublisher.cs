using YaDIS3.Core;

namespace YaDIS3.Rendering;

/// <summary>
/// Conserve dans workspace/interface les images du dernier traitement.
/// </summary>
internal static class LegacyProcessedMediaPublisher
{
    public static void Publish(
        MediaContext media,
        string workspace,
        string cache
    )
    {
        if (!Directory.Exists(cache))
        {
            return;
        }

        string output = Path.Combine(
            workspace,
            "interface",
            LegacyExportPaths.Media(
                media.Header.ValueAt("filename"),
                media.IsSeries
            ).Replace('/', Path.DirectorySeparatorChar)
        );
        CopyImages(cache, output);
    }

    private static void CopyImages(string source, string output)
    {
        Directory.CreateDirectory(output);
        foreach (string path in Directory.EnumerateFiles(source))
        {
            string name = Path.GetFileName(path);
            if (
                name.Equals("preview.jpg", StringComparison.OrdinalIgnoreCase)
                || name.Equals("render.json", StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            File.Copy(path, Path.Combine(output, name), true);
        }

        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            string name = Path.GetFileName(directory);
            if (!name.StartsWith('_'))
            {
                CopyImages(directory, Path.Combine(output, name));
            }
        }
    }
}
