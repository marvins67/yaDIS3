using System.Xml.Linq;
using Xunit;

namespace YaDIS3.Tests;

public sealed class LegacyExportLocalReferenceTests
{
    [Fact]
    public void Export_CompleteTree_ResolvesExplicitLocalReferences()
    {
        using ExportFixture fixture = new();
        new XElement("groups", new XElement("selection",
            new XAttribute("enabled", "yes"),
            new XElement("name", "Choix"),
            new XElement("movie", ExportFixture.MovieName)))
            .Save(Path.Combine(fixture.Root, "groups.xml"));
        new XElement("shortcuts", new XElement("shortcut",
            new XAttribute("type", "0"), "Choix"))
            .Save(Path.Combine(fixture.Root, "shortcuts.xml"));
        fixture.ProcessIndexes();
        ExportFixture.Exporter().Export(fixture.Cards(), fixture.Destination);

        string selection = Path.Combine(fixture.Destination, "00", "01_Choix", "dune_folder.txt");
        string season = Path.Combine(fixture.Destination, "01", ExportFixture.SeriesName, "01", "dune_folder.txt");
        // ProcessTVShow ne génère l'icône de saison que pour season > 0.
        string[] legacyMissing =
        [
            $"{selection}: icon_path=icon.aai",
            $"{season}: icon_path=icon.aai",
            $"{season}: icon_sel_path=icon.aai",
        ];
        Assert.Equal(
            legacyMissing.Order(StringComparer.Ordinal),
            MissingReferences(fixture.Destination).Order(StringComparer.Ordinal));

        UseNumberedSeason(fixture);
        ExportFixture.Exporter().Export(fixture.Cards(), fixture.Destination);
        Assert.Equal(
            [$"{selection}: icon_path=icon.aai"],
            MissingReferences(fixture.Destination));

        string icon = Path.Combine(fixture.Destination, "00", "99", ExportFixture.MovieName, "icon.aai");
        File.Delete(icon);
        Assert.Contains(MissingReferences(fixture.Destination), reference =>
            reference.Contains("item.", StringComparison.Ordinal)
            && reference.EndsWith("/icon.aai", StringComparison.Ordinal));
    }

    private static void UseNumberedSeason(ExportFixture fixture)
    {
        string path = Path.Combine(fixture.Root, "data", ExportFixture.SeriesName, "data.xml");
        XElement data = XElement.Load(path);
        XElement episode = data.Element("info")!.Element("episode")!;
        episode.SetAttributeValue("key", "01_001_000");
        episode.SetAttributeValue("season", "1");
        episode.SetAttributeValue("episode", "1");
        data.Save(path);
    }

    private static IEnumerable<string> MissingReferences(string root)
    {
        foreach (string file in Directory.EnumerateFiles(root, "dune_folder.txt", SearchOption.AllDirectories))
        {
            Dictionary<string, string> properties = File.ReadAllLines(file)
                .Where(line => line.Contains('='))
                .Select(line => line.Split('=', 2))
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
            foreach ((string key, string value) in properties)
            {
                if (!IsLocalReference(key, value, properties))
                {
                    continue;
                }
                string target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, value));
                if (!File.Exists(target) && !Directory.Exists(target))
                {
                    yield return $"{file}: {key}={value}";
                }
            }
        }
    }

    private static bool IsLocalReference(
        string key,
        string value,
        Dictionary<string, string> properties
    )
    {
        if (value.Length == 0 || value.Contains("://", StringComparison.Ordinal) || Path.IsPathRooted(value))
        {
            return false;
        }
        string field = key[(key.LastIndexOf('.') + 1)..];
        if (field == "media_url")
        {
            string prefix = key[..^field.Length];
            return properties.TryGetValue(prefix + "media_action", out string? action) && action == "browse";
        }
        return field is "background_path" or "icon_path" or "icon_sel_path";
    }
}
