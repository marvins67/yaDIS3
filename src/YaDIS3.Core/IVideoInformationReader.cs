using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>Lit les caractéristiques d'une vidéo.</summary>
public interface IVideoInformationReader
{
    /// <summary>Produit le bloc XML technique historique.</summary>
    Task<XElement> ReadAsync(
        string file,
        CancellationToken cancellationToken
    );
}
