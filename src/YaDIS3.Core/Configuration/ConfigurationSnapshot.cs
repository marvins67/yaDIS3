using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>Copie indépendante des documents modifiés uniquement à la validation.</summary>
public sealed record ConfigurationSnapshot(
    DuneConfiguration Settings,
    IReadOnlyList<MediaSourceFolder> Folders,
    XDocument Groups,
    IReadOnlyList<DuneShortcut> Shortcuts
);
