using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>Reprend AddToSelection sans doublon ni création implicite de sélection.</summary>
public sealed class LegacyAutoSelection(
    IExportFileSystem? fileSystem = null
)
{
    private readonly IExportFileSystem _files =
        fileSystem ?? new ExportFileSystem();

    /// <summary>Ajoute le film à la fin de la sélection existante.</summary>
    public void Add(
        string workspace,
        string selectionName,
        string movie
    )
    {
        XDocument groups = new DuneConfigurationStore(_files)
            .Load(workspace)
            .Groups;
        XElement? selection = groups
            .Descendants("selection")
            .FirstOrDefault(node =>
                node.Element("name")?.Value == selectionName
            );
        if (
            selection is null
            || selection
                .Elements("movie")
                .Any(node => node.Value == movie)
        )
        {
            return;
        }
        selection.Add(new XElement("movie", movie));
        using MemoryStream stream = new();
        groups.Save(stream);
        _files.WriteBytes(
            Path.Combine(workspace, "groups.xml"),
            stream.ToArray()
        );
    }
}
