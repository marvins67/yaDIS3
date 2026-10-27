using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
namespace YaDIS3.Core;

public sealed record VideoEpisode(int Season, int Number, string File);
public static class EpisodeScanner
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".mkv", ".mp4", ".avi", ".m2ts", ".mov", ".wmv", ".ts", ".iso", ".mpg", ".mpeg", ".vob" };
    public static IReadOnlyList<VideoEpisode> Detect(string directory) => Directory.EnumerateFiles(directory, "*", new EnumerationOptions
        { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }).Order(StringComparer.Ordinal)
        .Where(p => Extensions.Contains(Path.GetExtension(p))).SelectMany(Parse).DistinctBy(e => (e.Season, e.Number)).OrderBy(e => e.Season).ThenBy(e => e.Number).ToArray();
    public static IReadOnlyList<VideoEpisode> Parse(string file)
    {
        var normalized = file.Replace('\\', '/'); var name = normalized[(normalized.LastIndexOf('/') + 1)..];
        var stem = Path.GetFileNameWithoutExtension(name);
        var pair = Regex.Match(stem, @"(?i)(?:\bS(?<s>\d{1,2})[ ._-]*E(?<e>\d{1,3})|(?<!\d)(?<s>\d{1,2})[xXeE](?<e>\d{1,3}))(?!\d)");
        if (pair.Success)
        {
            var season = int.Parse(pair.Groups["s"].Value, CultureInfo.InvariantCulture); var number = int.Parse(pair.Groups["e"].Value, CultureInfo.InvariantCulture);
            var range = Regex.Match(stem[(pair.Index + pair.Length)..], @"^(?:[ ._]*E|\s*-\s*)(?<last>\d{1,3})(?!\d)", RegexOptions.IgnoreCase);
            var end = range.Success ? int.Parse(range.Groups["last"].Value, CultureInfo.InvariantCulture) : number;
            if (end < number || end - number > 100) end = number;
            return Enumerable.Range(number, end - number + 1).Select(n => new VideoEpisode(season, n, file)).ToArray();
        }
        var single = Regex.Match(stem, @"(?i)(?:\b(?:E|EP|Episode|Épisode)[ ._-]*|^\s*|\s+-\s+)(?<e>\d{1,3})(?=$|\s+-\s+|[ ._-]+[A-Za-zÀ-ÿ])");
        if (!single.Success) single = Regex.Match(stem, @"(?:^|[ ._])(?<e>\d{1,3})$|^\[(?<e>\d{1,3})\](?:\s|$)");
        if (!single.Success) return [];
        var directory = normalized[..Math.Max(0, normalized.LastIndexOf('/'))];
        var parent = Regex.Match(directory, @"(?i)(?:^|/)(?:Season|Saison|S)[ ._-]*(?<s>\d{1,2})(?:/|$)");
        return [new VideoEpisode(parent.Success ? int.Parse(parent.Groups["s"].Value, CultureInfo.InvariantCulture) : 1, int.Parse(single.Groups["e"].Value, CultureInfo.InvariantCulture), file)];
    }
    public static void Synchronize(MediaCard card, IReadOnlyList<VideoEpisode> detected)
    {
        if (!card.IsSeries) throw new InvalidOperationException("Sélectionnez une série.");
        card.Update(() =>
        {
            var info = card.EnsureInfo();
            foreach (var found in detected.DistinctBy(e => (e.Season, e.Number)))
            {
                var existing = info.Elements("episode").FirstOrDefault(e => (string?)e.Attribute("season") == found.Season.ToString(CultureInfo.InvariantCulture) && (string?)e.Attribute("episode") == found.Number.ToString(CultureInfo.InvariantCulture));
                if (existing is not null)
                {
                    if (existing.Element("file")?.Value != found.File) { MediaCard.Put(existing, "file", found.File); existing.Element("mediainfo")?.Remove(); }
                    continue;
                }
                var ep = new XElement("episode", new XAttribute("season", found.Season), new XAttribute("episode", found.Number), new XAttribute("key", $"{found.Season:00}_{found.Number:000}_000"));
                foreach (var field in new[] { "title", "plot", "aired", "rating", "director", "image", "file" })
                    ep.Add(new XElement(field, field == "file" ? found.File : field == "title" ? Path.GetFileNameWithoutExtension(found.File) : ""));
                info.Add(ep);
            }
        });
        // Offline volumes must never cause automatic deletion of existing metadata.
        card.RefreshEpisodes();
    }
}
