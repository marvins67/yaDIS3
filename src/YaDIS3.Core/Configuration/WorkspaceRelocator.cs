namespace YaDIS3.Core;

/// <summary>
/// Reprend SyncWorkspace tout en conservant une sauvegarde de
/// l'ancienne destination.
/// </summary>
public sealed class WorkspaceRelocator(
    IExportFileSystem? fileSystem = null
)
{
    private readonly IExportFileSystem _files =
        fileSystem ?? new ExportFileSystem();

    /// <summary>
    /// Copie tous les fichiers ; la source reste disponible.
    /// </summary>
    public void Copy(
        string source,
        string destination,
        CancellationToken cancellationToken = default
    )
    {
        source = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(source)
        );
        destination = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(destination)
        );
        if (
            source.Equals(
                destination,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }
        if (
            destination.StartsWith(
                source + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            )
            || source.StartsWith(
                destination + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            throw new IOException(
                "Les deux espaces de travail ne peuvent pas être imbriqués."
            );
        }
        string stage =
            destination
            + ".stage-"
            + Guid.NewGuid().ToString("N");
        try
        {
            CopyTree(source, stage, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            CommitCopy(stage, destination);
        }
        finally
        {
            if (_files.DirectoryExists(stage))
            {
                _files.DeleteDirectory(stage);
            }
        }
    }

    private void CommitCopy(string stage, string destination)
    {
        string? backup = null;
        if (_files.DirectoryExists(destination))
        {
            backup =
                destination
                + ".previous-"
                + Guid.NewGuid().ToString("N");
            _files.MoveDirectory(destination, backup);
        }
        try
        {
            _files.MoveDirectory(stage, destination);
        }
        catch
        {
            if (backup is not null)
            {
                _files.MoveDirectory(backup, destination);
            }
            throw;
        }
    }

    private void CopyTree(
        string source,
        string target,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        _files.CreateDirectory(target);
        foreach (string path in _files.EnumerateFiles(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _files.CopyFile(
                path,
                Path.Combine(target, Path.GetFileName(path)),
                true
            );
        }
        foreach (
            string directory in _files.EnumerateDirectories(
                source
            )
        )
        {
            CopyTree(
                directory,
                Path.Combine(
                    target,
                    Path.GetFileName(directory)
                ),
                cancellationToken
            );
        }
    }
}
