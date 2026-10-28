using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

/// <summary>
/// Provides an immutable metadata snapshot for rendering
/// and export.
/// </summary>
public sealed class MediaContext
{
    /// <summary>
    /// Gets the XML root for the loaded data.
    /// </summary>
    public XElement Root { get; }

    /// <summary>
    /// Gets the card's legacy item header.
    /// </summary>
    public XElement Header { get; }

    /// <summary>
    /// Gets the metadata used to render the card.
    /// </summary>
    public XElement Info { get; }

    /// <summary>
    /// Gets the directory containing the card metadata and
    /// artwork.
    /// </summary>
    public string DataDirectory { get; }

    /// <summary>
    /// Gets or sets the displayed title.
    /// </summary>
    public string Title =>
        Info.ValueAt("title") is { Length: > 0 } title
            ? title
            : Header.ValueAt("filename");

    /// <summary>
    /// Indicates whether the current card represents a series.
    /// </summary>
    public bool IsSeries => Header.ValueAt("type") == "1";

    /// <summary>
    /// Gets the stable identifier used for this catalog entry.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the card's episodes in their stored navigation
    /// order.
    /// </summary>
    public IReadOnlyList<XElement> Episodes { get; }

    /// <summary>
    /// Copies the card metadata for a stable render and export
    /// snapshot.
    /// </summary>
    public MediaContext(MediaCard card)
    {
        Root = card.Snapshot().Document.Root!;
        Header =
            Root.Name == "item" ? Root : Root.Element("item")!;
        Info =
            Root.Element("info")
            ?? Header.Element("info")
            ?? new XElement(
                "info",
                new XElement("title", Header.ValueAt("filename"))
            );
        DataDirectory = card.DataDirectory;
        string seed =
            (IsSeries ? "tv:" : "movie:")
            + Path.GetFileName(DataDirectory)
                .Normalize(NormalizationForm.FormC);
        Id = Convert
            .ToHexString(
                SHA256
                    .HashData(Encoding.UTF8.GetBytes(seed))
                    .AsSpan(0, 10)
            )
            .ToLowerInvariant();
        Episodes = Info.Elements("episode")
            .OrderBy(
                element => element.Attr("key"),
                StringComparer.Ordinal
            )
            .ToArray();
    }

    /// <summary>
    /// Resolves an artwork filename within the card directory.
    /// </summary>
    public string Artwork(string name)
    {
        return Path.Combine(DataDirectory, name);
    }

    /// <summary>
    /// Gets the season number stored for an episode.
    /// </summary>
    public static int Season(XElement episode)
    {
        return episode.Int("season");
    }

    /// <summary>
    /// Gets the episode number within its season.
    /// </summary>
    public static int Number(XElement episode)
    {
        return episode.Int("episode");
    }

    // Preserve the legacy key's viewing season (specials may be
    // positioned in another season).

    /// <summary>
    /// Gets the navigation season, preserving special-episode
    /// placement.
    /// </summary>
    public static string ViewingSeason(XElement episode)
    {
        string key = episode.Attr("key");
        string first = key.Split('_')[0];
        if (
            first.Length is >= 2 and <= 3
            && int.TryParse(
                first,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out _
            )
        )
        {
            return first;
        }

        return Season(episode)
            .ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets a safe, stable folder name for an episode's Dune
    /// entry.
    /// </summary>
    public static string EpisodeFolder(XElement episode)
    {
        string key = episode.Attr("key");
        return System.Text.RegularExpressions.Regex.IsMatch(
            key,
            "^[0-9]{2,3}_[0-9]{3,5}_[0-9]{3,5}$",
            System
                .Text
                .RegularExpressions
                .RegexOptions
                .CultureInvariant
        )
            ? key
            : $"{Season(episode):00}_{Number(episode):000}_000";
    }
}
