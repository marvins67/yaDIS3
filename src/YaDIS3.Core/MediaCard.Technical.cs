using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>Édition du bloc mediainfo utilisé par yaDIS2.</summary>
public sealed partial class MediaCard
{
    /// <summary>Codec vidéo.</summary>
    public string VideoCodec
    {
        get => ReadTechnical("video", "codec");
        set => SetTechnical("video", "codec", value);
    }

    /// <summary>Résolution vidéo.</summary>
    public string Resolution
    {
        get => ReadTechnical("video", "resolution");
        set => SetTechnical("video", "resolution", value);
    }

    /// <summary>Images par seconde.</summary>
    public string FrameRate
    {
        get => ReadTechnical("video", "framerate");
        set => SetTechnical("video", "framerate", value);
    }

    /// <summary>Rapport de forme.</summary>
    public string AspectRatio
    {
        get => ReadTechnical("video", "aspectratio");
        set => SetTechnical("video", "aspectratio", value);
    }

    /// <summary>Conteneur du fichier.</summary>
    public string ContainerFormat
    {
        get => ReadTechnical("", "format");
        set => SetTechnical("", "format", value);
    }

    /// <summary>Boîtier sélectionné.</summary>
    public string CaseType
    {
        get => ReadTechnical("", "source");
        set => SetTechnical("", "source", value);
    }

    /// <summary>Codec audio.</summary>
    public string AudioCodec
    {
        get => ReadTechnical("audio", "codec");
        set => SetTechnical("audio", "codec", value);
    }

    /// <summary>Canaux audio.</summary>
    public string AudioChannels
    {
        get => ReadTechnical("audio", "channels");
        set => SetTechnical("audio", "channels", value);
    }

    /// <summary>Langues audio.</summary>
    public string AudioLanguages
    {
        get =>
            ReadTechnical("audio", "languages")
                .Replace("|", ", ", StringComparison.Ordinal);
        set =>
            SetTechnical(
                "audio",
                "languages",
                value
                    .ToUpperInvariant()
                    .Replace(", ", "|", StringComparison.Ordinal)
            );
    }

    /// <summary>Langues des sous-titres.</summary>
    public string SubtitleLanguages
    {
        get =>
            ReadTechnical("subtitles", "languages")
                .Replace("|", ", ", StringComparison.Ordinal);
        set =>
            SetTechnical(
                "subtitles",
                "languages",
                value
                    .ToUpperInvariant()
                    .Replace(", ", "|", StringComparison.Ordinal)
            );
    }

    private XElement? TechnicalSection(string section)
    {
        XElement? technical =
            Root.Element("mediainfo")
            ?? Header.Element("mediainfo");
        return section.Length == 0
            ? technical
            : technical?.Element(section);
    }

    private string ReadTechnical(string section, string field) =>
        TechnicalSection(section)?.Element(field)?.Value ?? "";

    private void SetTechnical(
        string section,
        string field,
        string value
    )
    {
        XElement? node = TechnicalSection(section);
        // Comme SaveInfos, ne crée pas de données techniques sans analyse préalable.
        if (
            node?.Element(field) is not null
            && ReadTechnical(section, field) != value
        )
        {
            Update(() => Put(node, field, value));
        }
    }
}
