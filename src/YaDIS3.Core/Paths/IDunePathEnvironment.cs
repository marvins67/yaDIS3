namespace YaDIS3.Core;

/// <summary>Accès substituable aux lecteurs et à la résolution réseau.</summary>
public interface IDunePathEnvironment
{
    /// <summary>Racine UNC d'un lecteur réseau, sinon chaîne vide.</summary>
    string NetworkRoot(string drive);

    /// <summary>Nom du volume local.</summary>
    string VolumeLabel(string drive);

    /// <summary>Lecteur ou montage natif hors Windows.</summary>
    Task<NativeFolderLocation> NativeFolderAsync(
        string folder,
        CancellationToken cancellationToken
    );

    /// <summary>Première adresse renvoyée par DNS, sans filtrage IPv4.</summary>
    Task<string> FirstAddressAsync(
        string host,
        CancellationToken cancellationToken
    );
}
