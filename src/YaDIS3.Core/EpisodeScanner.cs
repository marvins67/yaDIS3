using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>
/// Identifies an episode detected in a local video
/// filename.
/// </summary>
public sealed record VideoEpisode(
    int Season,
    int Number,
    string File
);

/// <summary>
/// Detects local episodes and merges them into a series
/// card.
/// </summary>
public static class EpisodeScanner
{
    private static readonly HashSet<string> s_extensions = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ".mkv",
        ".mp4",
        ".avi",
        ".m2ts",
        ".mov",
        ".wmv",
        ".ts",
        ".iso",
        ".mpg",
        ".mpeg",
        ".vob",
    };

    /// <summary>
    /// Finds supported video files and extracts their episode
    /// numbers.
    /// </summary>
    public static IReadOnlyList<VideoEpisode> Detect(
        string directory
    )
    {
        return Directory
            .EnumerateFiles(
                directory,
                "*",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip =
                        FileAttributes.ReparsePoint,
                }
            )
            .Order(StringComparer.Ordinal)
            .Where(path =>
                s_extensions.Contains(Path.GetExtension(path))
            )
            .SelectMany(Parse)
            .DistinctBy(element =>
                (element.Season, element.Number)
            )
            .OrderBy(element => element.Season)
            .ThenBy(element => element.Number)
            .ToArray();
    }

    /// <summary>
    /// Extracts season and episode numbers from a video
    /// filename.
    /// </summary>
    public static IReadOnlyList<VideoEpisode> Parse(string file)
    {
        string normalized = file.Replace('\\', '/');
        string name = normalized[
            (normalized.LastIndexOf('/') + 1)..
        ];
        string stem = Path.GetFileNameWithoutExtension(name);
        Match pair = Regex.Match(
            stem,
            """(?i)(?:\bS(?<s>\d{1,2})[ ._-]*E(?<e>\d{1,3})|(?<!\d)(?<s>\d{1,2})[xXeE](?<e>\d{1,3}))(?!\d)"""
        );
        if (pair.Success)
        {
            int season = int.Parse(
                pair.Groups["s"].Value,
                CultureInfo.InvariantCulture
            );
            int number = int.Parse(
                pair.Groups["e"].Value,
                CultureInfo.InvariantCulture
            );
            Match range = Regex.Match(
                stem[(pair.Index + pair.Length)..],
                """^(?:[ ._]*E|\s*-\s*)(?<last>\d{1,3})(?!\d)""",
                RegexOptions.IgnoreCase
            );
            int end = range.Success
                ? int.Parse(
                    range.Groups["last"].Value,
                    CultureInfo.InvariantCulture
                )
                : number;
            if (end < number || end - number > 100)
            {
                end = number;
            }

            return Enumerable
                .Range(number, end - number + 1)
                .Select(numberValue => new VideoEpisode(
                    season,
                    numberValue,
                    file
                ))
                .ToArray();
        }

        Match single = Regex.Match(
            stem,
            """(?i)(?:\b(?:E|EP|Episode|Épisode)[ ._-]*|^\s*|\s+-\s+)(?<e>\d{1,3})(?=$|\s+-\s+|[ ._-]+[A-Za-zÀ-ÿ])"""
        );
        if (!single.Success)
        {
            single = Regex.Match(
                stem,
                """(?:^|[ ._])(?<e>\d{1,3})$|^\[(?<e>\d{1,3})\](?:\s|$)"""
            );
        }

        if (!single.Success)
        {
            return [];
        }

        string directory = normalized[
            ..Math.Max(0, normalized.LastIndexOf('/'))
        ];
        Match parent = Regex.Match(
            directory,
            """(?i)(?:^|/)(?:Season|Saison|S)[ ._-]*(?<s>\d{1,2})(?:/|$)"""
        );
        return
        [
            new VideoEpisode(
                parent.Success
                    ? int.Parse(
                        parent.Groups["s"].Value,
                        CultureInfo.InvariantCulture
                    )
                    : 1,
                int.Parse(
                    single.Groups["e"].Value,
                    CultureInfo.InvariantCulture
                ),
                file
            ),
        ];
    }

    /// <summary>
    /// Merges detected video paths without deleting existing
    /// episodes.
    /// </summary>
    public static void Synchronize(
        MediaCard card,
        IReadOnlyList<VideoEpisode> detected
    )
    {
        if (!card.IsSeries)
        {
            throw new InvalidOperationException(
                "Sélectionnez une série."
            );
        }

        card.Update(() =>
        {
            XElement info = card.EnsureInfo();
            foreach (
                VideoEpisode found in detected.DistinctBy(
                    element => (element.Season, element.Number)
                )
            )
            {
                XElement? existing = info.Elements("episode")
                    .FirstOrDefault(element =>
                        (string?)element.Attribute("season")
                            == found.Season.ToString(
                                CultureInfo.InvariantCulture
                            )
                        && (string?)element.Attribute("episode")
                            == found.Number.ToString(
                                CultureInfo.InvariantCulture
                            )
                    );
                if (existing is not null)
                {
                    if (
                        existing.Element("file")?.Value
                        != found.File
                    )
                    {
                        MediaCard.Put(
                            existing,
                            "file",
                            found.File
                        );
                        existing.Element("mediainfo")?.Remove();
                    }

                    continue;
                }

                var episode = new XElement(
                    "episode",
                    new XAttribute("season", found.Season),
                    new XAttribute("episode", found.Number),
                    new XAttribute(
                        "key",
                        $"{found.Season:00}_{found.Number:000}_000"
                    )
                );
                foreach (
                    string field in new[]
                    {
                        "title",
                        "plot",
                        "aired",
                        "rating",
                        "director",
                        "image",
                        "file",
                    }
                )
                {
                    episode.Add(
                        new XElement(
                            field,
                            field == "file" ? found.File
                                : field == "title"
                                    ? Path.GetFileNameWithoutExtension(
                                        found.File
                                    )
                                : ""
                        )
                    );
                }

                info.Add(episode);
            }
        });
        // Offline volumes must never cause automatic deletion of
        // existing metadata.
        card.RefreshEpisodes();
    }
}
