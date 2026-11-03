using System.Xml;

namespace YaDIS3.Core;

/// <summary>
/// Applique les réglages et redécouvre les sources
/// indépendamment de l'interface graphique.
/// </summary>
public sealed class DuneConfigurationService(
    IExportFileSystem? fileSystem = null
)
{
    private readonly IExportFileSystem _files =
        fileSystem ?? new ExportFileSystem();

    /// <summary>
    /// Change éventuellement de workspace puis enregistre les
    /// options validées.
    /// </summary>
    public void Apply(
        string? source,
        string destination,
        ConfigurationSnapshot snapshot,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        DuneConfigurationStore.Validate(snapshot);
        cancellationToken.ThrowIfCancellationRequested();
        if (source is not null)
        {
            new WorkspaceRelocator(_files).Copy(
                source,
                destination,
                cancellationToken
            );
        }
        _files.CreateDirectory(
            Path.Combine(destination, "data")
        );
        DuneConfigurationStore store = new(_files);
        ConfigurationSnapshot previous = store.Load(destination);
        store.Save(destination, snapshot);
        RefreshSources(
            store,
            destination,
            snapshot,
            previous,
            cancellationToken
        );
    }

    private void RefreshSources(
        DuneConfigurationStore store,
        string destination,
        ConfigurationSnapshot snapshot,
        ConfigurationSnapshot previous,
        CancellationToken cancellationToken
    )
    {
        try
        {
            SourceFolderScanner scanner = new(_files);
            scanner.Scan(
                destination,
                snapshot.Folders,
                snapshot.Settings.BasicMode,
                cancellationToken
            );
            scanner.ValidateCards(
                destination,
                cancellationToken
            );
        }
        catch (Exception failure)
            when (failure
                    is IOException
                        or InvalidDataException
                        or UnauthorizedAccessException
                        or XmlException
                        or OperationCanceledException
            )
        {
            try
            {
                store.Save(destination, previous);
            }
            catch (Exception restore)
                when (restore
                        is IOException
                            or UnauthorizedAccessException
                            or AggregateException
                )
            {
                throw new AggregateException(failure, restore);
            }
            throw;
        }
    }
}
