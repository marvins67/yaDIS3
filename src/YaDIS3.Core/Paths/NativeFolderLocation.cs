namespace YaDIS3.Core;

/// <summary>Équivalent natif d'un lecteur Windows et de son sous-dossier.</summary>
public sealed record NativeFolderLocation(
    string NetworkPath,
    string VolumeLabel,
    string RelativePath
);
