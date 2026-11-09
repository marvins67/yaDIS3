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
    ) => Synchronize(source, destination, null, cancellationToken);

    /// <summary>Collecte les erreurs tout en poursuivant la synchronisation yaDIS2.</summary>
    public void Synchronize(
        string source,
        string destination,
        ICollection<string>? errors,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            SynchronizeDirectory(source, destination, errors, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            errors?.Add($"Synchronisation de {source} vers {destination} : {exception.Message}");
        }
    }

    private void SynchronizeDirectory(
        string source,
        string destination,
        ICollection<string>? errors,
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
            if (File.Exists(path))
            {
                remaining.Remove(name);
                CopyWhenChanged(path, target, errors);
            }
            else if (
                Directory.Exists(path)
                && !name.StartsWith('_')
            )
            {
                remaining.Remove(name);
                Synchronize(path, target, errors, cancellationToken);
            }
        }

        foreach (string path in remaining.Values)
        {
            if (File.Exists(path))
            {
                TryDeleteFile(path, errors);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }

    private static void CopyWhenChanged(string source, string destination, ICollection<string>? errors)
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
            errors?.Add($"Copie de {source} vers {destination} : {exception.Message}");
        }
    }

    private static void TryDeleteFile(string path, ICollection<string>? errors)
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
            errors?.Add($"Suppression de {path} : {exception.Message}");
        }
    }
}
