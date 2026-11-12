using System.Xml.Linq;
using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyExportSelectionsParityTests
{
    [Fact]
    public void Export_MultipleMemberships_PreservesCollectionOrderAndSourceGroups()
    {
        using ExportFixture fixture = new();
        MediaContext first = Movie(fixture, "Premier.mkv");
        MediaContext shared = Movie(fixture, "Partagé.mkv");
        MediaContext excluded = Movie(fixture, "Erreur.mkv");
        excluded.Header.SetElementValue("status", "3");
        string groups = Path.Combine(fixture.Root, "groups.xml");
        new XElement("groups",
            Selection("Papa", "Partagé.mkv", "Premier.mkv", "Partagé.mkv", "Erreur.mkv", ExportFixture.SeriesName),
            Selection("Enfants", "Partagé.mkv"),
            Selection("Disney", "Premier.mkv"),
            Selection("Ghibli"))
            .Save(groups);
        byte[] original = File.ReadAllBytes(groups);
        string[] names = ["Papa", "Enfants", "Disney", "Ghibli"];
        new XElement("shortcuts", names.Select(name =>
            new XElement("shortcut", new XAttribute("type", "0"), name)))
            .Save(Path.Combine(fixture.Root, "shortcuts.xml"));
        Directory.CreateDirectory(fixture.Destination);
        DuneExporter exporter = new(
            ExportFixture.Template(), new RenderOptions(), [],
            catalogOptions: new LegacyCatalogOptions { Categories = "00000000000" });
        MediaContext[] cards = [first, shared, excluded, fixture.Cards().Single(card => card.IsSeries)];

        for (int attempt = 0; attempt < 2; attempt++)
        {
            exporter.ExportLegacy(cards, fixture.Destination);
            Assert.Equal(["../99/Premier.mkv", "../99/Partagé.mkv"], Links(fixture, "Papa"));
            Assert.Equal(["../99/Partagé.mkv"], Links(fixture, "Enfants"));
            Assert.Equal(["../99/Premier.mkv"], Links(fixture, "Disney"));
            Assert.Empty(Links(fixture, "Ghibli"));
            for (int index = 0; index < names.Length; index++)
            {
                string shortcut = Path.Combine(fixture.Destination, $"05_{index:00}_{names[index]}", "dune_folder.txt");
                Assert.Contains($"media_url=../00/01_{names[index]}", File.ReadAllLines(shortcut));
            }
        }
        Assert.Equal(original, File.ReadAllBytes(groups));
    }

    private static XElement Selection(string name, params string[] members) =>
        new("selection", new XAttribute("enabled", "yes"),
            new XElement("name", name),
            members.Select(member => new XElement("movie", member)));

    private static MediaContext Movie(ExportFixture fixture, string name)
    {
        MediaContext movie = fixture.Cards().Single(card => !card.IsSeries);
        movie.Header.SetElementValue("filename", name);
        movie.Info.SetElementValue("sort_title", name);
        return movie;
    }

    private static IEnumerable<string> Links(ExportFixture fixture, string name) =>
        File.ReadAllLines(Path.Combine(fixture.Destination, "00", "01_" + name, "dune_folder.txt"))
            .Where(line => line.StartsWith("item.", StringComparison.Ordinal)
                && line.Contains(".media_url=", StringComparison.Ordinal))
            .Select(line => line.Split('=', 2)[1]);
}
