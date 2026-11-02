using System.Xml;
using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

/// <summary>Retire les anciens critères après une sauvegarde manuelle réussie.</summary>
public sealed class LegacyIndexedCardSaver(
    IWorkspaceRepository repository,
    IExportFileSystem? fileSystem = null
)
{
    private readonly IExportFileSystem _fileSystem =
        fileSystem ?? new ExportFileSystem();

    /// <summary>Conserve les contrôles de conflit avant de modifier les index historiques.</summary>
    public async Task SaveAsync(
        MediaCard card,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        XElement? previous = ReadPrevious(card);
        await repository.SaveAsync(card, cancellationToken);
        if (previous is not null)
        {
            string workspace = Path.GetDirectoryName(
                Path.GetDirectoryName(card.DataDirectory)
            )!;
            new LegacyMovieIndexRemoval(_fileSystem).Remove(
                previous,
                workspace,
                cancellationToken
            );
        }
    }

    private XElement? ReadPrevious(MediaCard card)
    {
        if (
            !card.IsDirty
            || card.IsSeries
            || !_fileSystem.FileExists(card.FilePath)
        )
        {
            return null;
        }
        using StringReader input = new(
            _fileSystem.ReadText(card.FilePath)
        );
        using XmlReader reader = XmlReader.Create(
            input,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            }
        );
        return XElement.Load(reader);
    }
}
