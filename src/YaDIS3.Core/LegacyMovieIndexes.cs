using System.Globalization;
using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>
/// Reprend les regroupements de ProcessForm.AddToIndex.
/// </summary>
public static class LegacyMovieIndexes
{
    /// <summary>
    /// Les séries ne participent pas aux index de films de
    /// yaDIS2.
    /// </summary>
    public static IReadOnlyList<LegacyIndexEntry> Create(
        XElement info,
        XElement? technical
    )
    {
        ArgumentNullException.ThrowIfNull(info);
        List<LegacyIndexEntry> entries = [];
        AddNames(entries, info.Descendants("genre"), "00/04");
        AddPeople(
            entries,
            info.Descendants("actor").Take(5),
            "00/05"
        );
        AddPeople(
            entries,
            info.Descendants("director"),
            "00/06"
        );
        AddRuntime(
            entries,
            info.Element("runtime")?.Value ?? ""
        );
        AddNames(entries, info.Descendants("country"), "00/08");
        AddYear(entries, info.Element("year")?.Value ?? "");
        AddRating(entries, info.Element("rating")?.Value ?? "");
        Add(
            entries,
            "00/11",
            (technical?.Element("format")?.Value ?? "").Replace(
                "BDAV",
                "M2TS",
                StringComparison.Ordinal
            )
        );
        return entries;
    }

    private static void AddNames(
        List<LegacyIndexEntry> entries,
        IEnumerable<XElement> names,
        string directory
    )
    {
        foreach (XElement name in names)
        {
            Add(entries, directory, name.Value);
        }
    }

    private static void AddPeople(
        List<LegacyIndexEntry> entries,
        IEnumerable<XElement> people,
        string directory
    )
    {
        foreach (XElement person in people)
        {
            string name = person.Value;
            int initialIndex = name.LastIndexOf(' ') + 1;
            // yaDIS2 ignore cette entrée si aucun caractère ne suit le dernier espace.
            if (initialIndex < name.Length)
            {
                Add(
                    entries,
                    directory + "/" + name[initialIndex],
                    name
                );
            }
        }
    }

    private static void AddRuntime(
        List<LegacyIndexEntry> entries,
        string value
    )
    {
        if (
            !double.TryParse(
                value,
                CultureInfo.InvariantCulture,
                out double runtime
            )
        )
        {
            return;
        }

        int minutes = (int)Math.Round(runtime);
        int hours = minutes / 60;
        int remainder = minutes % 60;
        if (remainder > 30)
        {
            hours++;
            remainder = 0;
        }
        else if (remainder > 0)
        {
            remainder = 30;
        }

        if (hours + remainder > 0)
        {
            Add(
                entries,
                "00/07",
                FormattableString.Invariant(
                    $"{hours}h {remainder:00}"
                )
            );
        }
    }

    private static void AddYear(
        List<LegacyIndexEntry> entries,
        string year
    )
    {
        if (year.Length == 4)
        {
            Add(entries, "00/09", year[..3] + "0");
        }
    }

    private static void AddRating(
        List<LegacyIndexEntry> entries,
        string rating
    )
    {
        if (rating.Length > 0 && rating[0] != '0')
        {
            Add(
                entries,
                "00/10",
                rating.StartsWith("10", StringComparison.Ordinal)
                    ? "10"
                    : rating[..1]
            );
        }
    }

    private static void Add(
        List<LegacyIndexEntry> entries,
        string directory,
        string caption
    )
    {
        // Une métadonnée blanche ne constitue pas une catégorie exportable.
        if (string.IsNullOrWhiteSpace(caption))
        {
            return;
        }

        LegacyExportPaths.ValidateName(caption);
        // CreateIndexItem passe par les chemins Win32, qui retirent les points finaux.
        string name = caption.TrimEnd(' ', '.');
        if (name.Length == 0)
        {
            return;
        }
        entries.Add(
            new LegacyIndexEntry(directory + "/" + name, caption)
        );
    }
}
