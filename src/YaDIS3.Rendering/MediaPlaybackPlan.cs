using System.Xml.Linq;

namespace YaDIS3.Rendering;

/// <summary>Chemins de lecture validés avant toute écriture dans le catalogue.</summary>
internal sealed record MediaPlaybackPlan(
    MediaContext Media,
    IReadOnlyList<string> MovieUrls,
    IReadOnlyDictionary<XElement, string> Episodes
);
