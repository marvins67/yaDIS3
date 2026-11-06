using YaDIS3.Core;

namespace YaDIS3.Rendering;

/// <summary>
/// Retire les anciens critères avant une nouvelle identification TMDB.
/// </summary>
public sealed class LegacyMovieTitleChangeCleanup(
    IExportFileSystem? fileSystem = null
)
{
    private readonly IExportFileSystem _files =
        fileSystem ?? new ExportFileSystem();

    /// <summary>Nettoie les index associés à l'ancienne fiche du film.</summary>
    public void Remove(
        MediaCard card,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card.IsSeries)
        {
            throw new ArgumentException(
                "Le nettoyage des index concerne les films.",
                nameof(card)
            );
        }
        string workspace = Path.GetDirectoryName(
            Path.GetDirectoryName(card.DataDirectory)
        )!;
        new LegacyMovieIndexRemoval(_files).Remove(
            card.Snapshot().Document.Root!,
            workspace,
            cancellationToken
        );
    }
}
