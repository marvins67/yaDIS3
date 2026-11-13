using System.Globalization;
using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

/// <summary>Retire les illustrations et rendus des épisodes supprimés.</summary>
internal static class EpisodeTreatmentCleanup
{
    internal static void RemoveDeleted(
        MediaContext previous, MediaContext current, string workspace)
    {
        var retained = current.Episodes.Select(Identity).ToHashSet();
        foreach (XElement episode in previous.Episodes.Where(item => !retained.Contains(Identity(item))))
        {
            string thumbnail = string.Create(CultureInfo.InvariantCulture,
                $"{MediaContext.Season(episode)}x{MediaContext.Number(episode)}.jpg");
            File.Delete(Path.Combine(current.DataDirectory, thumbnail));
            string season = MediaContext.ViewingSeason(episode);
            string folder = MediaContext.EpisodeFolder(episode);
            string output = LegacyExportPaths.WorkspaceMedia(workspace,
                current.Header.ValueAt("filename"), current.IsSeries);
            DeleteRender(workspace, Path.Combine(Path.GetRelativePath(workspace, output), season, folder));

        }
    }

    private static (int Season, int Number) Identity(XElement episode) =>
        (MediaContext.Season(episode), MediaContext.Number(episode));

    private static void DeleteRender(string workspace, string relative)
    {
        string path = workspace;
        foreach (string part in relative.Split(Path.DirectorySeparatorChar))
        {
            path = Path.Combine(path, part);
            if (!Directory.Exists(path))
            {
                return;
            }
            // Un lien ne doit jamais rediriger une suppression hors du workspace.
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Le dossier de rendu est un lien : {path}");
            }
        }
        Directory.Delete(path, recursive: true);
    }
}
