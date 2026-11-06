using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

/// <summary>
/// Reproduit l'ordre de bgwDeleteMedias_DoWork, sans toucher aux sources.
/// </summary>
public sealed class LegacyMediaDeletion(
    DuneConfigurationStore configurationStore,
    IExportFileSystem? fileSystem = null
)
{
    private readonly IExportFileSystem _files =
        fileSystem ?? new ExportFileSystem();

    /// <summary>Supprime données, index et interface, puis enregistre les groupes.</summary>
    public LegacyMediaDeletionResult Delete(
        string workspace,
        IReadOnlyList<MediaCard> cards,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        ArgumentNullException.ThrowIfNull(cards);
        string root = WorkspaceRepository.ResolveDirectory(workspace);
        XDocument groups = configurationStore.Load(root).Groups;
        List<string> deleted = [];
        List<string> errors = [];
        HashSet<string> removedSagas = new(StringComparer.Ordinal);

        foreach (MediaCard card in cards)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string folder = ValidateDataDirectory(root, card);
            try
            {
                _files.DeleteDirectory(folder);
                deleted.Add(card.FilePath);
            }
            catch (Exception error)
                when (error is not OperationCanceledException)
            {
                errors.Add(card.FolderName + " (données) : " + error.Message);
            }

            try
            {
                if (card.IsSeries)
                {
                    RemoveSeriesRendering(root, card.FolderName);
                }
                else
                {
                    foreach (string saga in LegacyMovieGroups.RemoveMovie(
                        groups,
                        card.FolderName
                    ))
                    {
                        removedSagas.Add(saga);
                    }
                    new LegacyMovieIndexRemoval(_files).Remove(
                        card.Snapshot().Document.Root!,
                        root,
                        cancellationToken
                    );
                    RemoveDirectoryIfPresent(
                        root,
                        "interface", "00", "99", card.FolderName
                    );
                }
            }
            catch (Exception error)
                when (error is not OperationCanceledException)
            {
                errors.Add(card.FolderName + " (interface) : " + error.Message);
            }
        }
        try
        {
            configurationStore.SaveGroups(root, groups);
        }
        catch (Exception error)
            when (error is IOException or UnauthorizedAccessException)
        {
            errors.Add("groups.xml : " + error.Message);
        }
        foreach (string saga in removedSagas)
        {
            try
            {
                RemoveDirectoryIfPresent(
                    root,
                    "interface", "00", "98", saga
                );
            }
            catch (Exception error)
                when (error is IOException or UnauthorizedAccessException)
            {
                errors.Add(saga + " (rendu saga) : " + error.Message);
            }
        }
        return new(deleted, errors);
    }

    private static string ValidateDataDirectory(
        string workspace,
        MediaCard card
    )
    {
        string expected = Path.GetFullPath(Path.Combine(
            workspace, "data", card.FolderName, "data.xml"
        ));
        string actual = Path.GetFullPath(card.FilePath);
        if (!string.Equals(
            actual, expected, StringComparison.OrdinalIgnoreCase
        ))
        {
            throw new InvalidDataException(
                "La fiche ne se trouve pas dans le workspace actif."
            );
        }
        return Path.GetDirectoryName(expected)!;
    }

    private void RemoveSeriesRendering(
        string workspace,
        string filename
    )
    {
        RemoveDirectoryIfPresent(workspace, "interface", "01", filename);
        string folder = Path.Combine(workspace, "interface", "01");
        if (_files.DirectoryExists(folder)
            && !_files.EnumerateDirectories(folder).Any())
        {
            _files.DeleteDirectory(folder);
        }
    }

    private void RemoveDirectoryIfPresent(
        string workspace,
        params string[] parts
    )
    {
        string root = Path.GetFullPath(workspace);
        string destination = Path.GetFullPath(
            Path.Combine([root, .. parts])
        );
        if (!destination.StartsWith(
            root + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase
        ))
        {
            throw new InvalidDataException(
                "Chemin de rendu extérieur au workspace."
            );
        }
        if (_files.DirectoryExists(destination))
        {
            _files.DeleteDirectory(destination);
        }
    }
}
