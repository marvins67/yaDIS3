using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using YaDIS3.Core;
namespace YaDIS3.Rendering;

public sealed class MediaContext
{
    public XElement Root { get; }
    public XElement Header { get; }
    public XElement Info { get; }
    public string DataDirectory { get; }
    public string Title => Info.ValueAt("title") is { Length: > 0 } title ? title : Header.ValueAt("filename");
    public bool IsSeries => Header.ValueAt("type") == "1";
    public string Id { get; }
    public IReadOnlyList<XElement> Episodes { get; }
    public MediaContext(MediaCard card)
    {
        Root = card.Snapshot().Document.Root!; Header = Root.Name == "item" ? Root : Root.Element("item")!;
        Info = Root.Element("info") ?? Header.Element("info") ?? new XElement("info", new XElement("title", Header.ValueAt("filename")));
        DataDirectory = card.DataDirectory;
        var seed = (IsSeries ? "tv:" : "movie:") + Path.GetFileName(DataDirectory).Normalize(NormalizationForm.FormC);
        Id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed)).AsSpan(0, 10)).ToLowerInvariant();
        Episodes = Info.Elements("episode").OrderBy(e => e.Attr("key"), StringComparer.Ordinal).ToArray();
    }
    public string Artwork(string name) => Path.Combine(DataDirectory, name);
    public static int Season(XElement episode) => episode.Int("season");
    public static int Number(XElement episode) => episode.Int("episode");
    // Preserve the legacy key's viewing season (specials may be positioned in another season).
    public static string ViewingSeason(XElement episode)
    {
        var key = episode.Attr("key");
        var first = key.Split('_')[0];
        if (first.Length is >= 2 and <= 3 && int.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out _)) return first;
        return Season(episode).ToString("00", CultureInfo.InvariantCulture);
    }
    public static string EpisodeFolder(XElement episode)
    {
        var key = episode.Attr("key");
        return System.Text.RegularExpressions.Regex.IsMatch(key, "^[0-9]{2,3}_[0-9]{3,5}_[0-9]{3,5}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            ? key : $"{Season(episode):00}_{Number(episode):000}_000";
    }
}
