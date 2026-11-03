using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>Entrée de folders.xml : type 0 pour les films, 1 pour les séries.</summary>
public sealed record MediaSourceFolder(
    string Path,
    int Type,
    string DunePath
)
{
    /// <summary>Extensions XML préservées lors de l'édition.</summary>
    public XElement? Original { get; init; }
}
