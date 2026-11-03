namespace YaDIS3.Core;

/// <summary>Reprend les sous-dossiers proposés par les sélecteurs de SetupForm.</summary>
public static class ConfigurationFolderChoice
{
    /// <summary>Propose workspace ou yaDIS lorsque le dossier choisi contient déjà des fichiers.</summary>
    public static string Resolve(
        string path,
        string suffix,
        IExportFileSystem? fileSystem = null
    )
    {
        IExportFileSystem files =
            fileSystem ?? new ExportFileSystem();
        string full = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(path)
        );
        bool matches =
            suffix == "workspace"
                ? Path.GetFileName(full)
                    .Equals(
                        suffix,
                        StringComparison.OrdinalIgnoreCase
                    )
                : full.EndsWith(
                    suffix,
                    StringComparison.OrdinalIgnoreCase
                );
        return
            !matches
            && (
                files.EnumerateFiles(full).Any()
                || files.EnumerateDirectories(full).Any()
            )
            ? Path.Combine(full, suffix)
            : full;
    }
}
