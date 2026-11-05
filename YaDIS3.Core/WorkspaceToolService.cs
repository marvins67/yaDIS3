using Microsoft.Extensions.Logging;

namespace YaDIS3.Core;

/// <summary>
/// Reproduit les suppressions du menu Outils de yaDIS2.
/// </summary>
public sealed class WorkspaceToolService
{
    private readonly ILogger _logger;

    public WorkspaceToolService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Ne supprime pas les sous-dossiers du cache.
    /// </summary>
    public void FlushImageCache(string workspace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        string cache = Path.Combine(workspace, "cache");
        DeleteFiles(cache);
    }

    /// <summary>
    /// Supprime les fichiers et tous les sous-dossiers de l'interface.
    /// </summary>
    public void ResetDuneInterface(string workspace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        string directory = Path.Combine(workspace, "interface");
        DeleteFiles(directory);
        DeleteSubdirectories(directory);
    }

    private void DeleteFiles(string directory)
    {
        try
        {
            foreach (string file in Directory.GetFiles(directory))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception exception) when (IsFileSystemError(exception))
                {
                    _logger.LogWarning(
                        exception,
                        "Impossible de supprimer le fichier {Path}",
                        file
                    );
                }
            }
        }
        catch (Exception exception) when (IsFileSystemError(exception))
        {
            // yaDIS2 affiche la confirmation même si l'énumération échoue.
            _logger.LogWarning(
                exception,
                "Impossible d'énumérer les fichiers de {Path}",
                directory
            );
        }
    }

    private void DeleteSubdirectories(string directory)
    {
        try
        {
            foreach (string child in Directory.GetDirectories(directory))
            {
                try
                {
                    Directory.Delete(child, recursive: true);
                }
                catch (Exception exception) when (IsFileSystemError(exception))
                {
                    _logger.LogWarning(
                        exception,
                        "Impossible de supprimer le dossier {Path}",
                        child
                    );
                }
            }
        }
        catch (Exception exception) when (IsFileSystemError(exception))
        {
            _logger.LogWarning(
                exception,
                "Impossible d'énumérer les dossiers de {Path}",
                directory
            );
        }
    }

    private static bool IsFileSystemError(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException;
}
