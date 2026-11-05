namespace YaDIS3.Rendering;

/// <summary>
/// Reproduit la synchronisation de ProcessForm.SyncInterface de yaDIS2.
/// </summary>
public sealed class LegacyExportSynchronizer
{
    /// <summary>
    /// Synchronise les répertoires sans sauvegarde ni remplacement global.
    /// </summary>
    public void Synchronize(
        string source,
        string destination,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            SynchronizeDirectory(source, destination, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            // SyncInterface abandonne le niveau courant sur erreur.
        }
    }

    private void SynchronizeDirectory(
        string source,
        string destination,
        CancellationToken cancellationToken
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        Directory.CreateDirectory(destination);
        var remaining = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase
        );
        foreach (string path in Directory.GetFileSystemEntries(destination))
        {
            remaining[Path.GetFileName(path)] = path;
        }

        foreach (string path in Directory.GetFileSystemEntries(source))
        {
            string name = Path.GetFileName(path);
            string target = Path.Combine(destination, name);
            remaining.Remove(name);

            if (File.Exists(path))
            {
                CopyWhenChanged(path, target);
            }
            else if (
                Directory.Exists(path)
                && !name.StartsWith('_')
            )
            {
                SynchronizeDirectory(path, target, cancellationToken);
            }
        }

        foreach (string path in remaining.Values)
        {
            if (File.Exists(path))
            {
                TryDeleteFile(path);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }

    private static void CopyWhenChanged(string source, string destination)
    {
        if (File.Exists(destination))
        {
            double delta = (
                File.GetLastWriteTimeUtc(destination)
                - File.GetLastWriteTimeUtc(source)
            ).TotalSeconds;
            if (Math.Truncate(delta) == 0)
            {
                return;
            }
        }

        try
        {
            if (File.Exists(destination))
            {
                File.SetAttributes(
                    destination,
                    File.GetAttributes(destination) & ~FileAttributes.ReadOnly
                );
            }

            File.Copy(source, destination, true);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            // La copie suivante continue malgré l'échec courant dans yaDIS2.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.SetAttributes(
                path,
                File.GetAttributes(path) & ~FileAttributes.ReadOnly
            );
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            // SyncInterface ignore les erreurs de suppression d'un fichier.
        }
    }
}
