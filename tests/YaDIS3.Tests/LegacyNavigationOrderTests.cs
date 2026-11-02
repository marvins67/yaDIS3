using System.Xml.Linq;
using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyNavigationOrderTests
{
    [Theory]
    [InlineData(
        "Le Voyage de Chihiro",
        "le club des cinq et le secret de la pyramide"
    )]
    [InlineData(
        "le club des cinq et le secret de la pyramide",
        "Le Voyage de Chihiro"
    )]
    public void Export_AlphabetMixedCase_PreservesBothMoviesInUppercaseDirectory(
        string firstTitle,
        string secondTitle
    )
    {
        using ExportFixture fixture = new();
        AddMovie(fixture, "Premier film.mkv", firstTitle);
        AddMovie(fixture, "Second film.mkv", secondTitle);
        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        string alphabet = Path.Combine(
            result.Directory,
            "00",
            "03"
        );
        string text = File.ReadAllText(
            Path.Combine(alphabet, "L", "dune_folder.txt")
        );
        Assert.Equal(
            2,
            text.Split(
                ".media_url=",
                StringSplitOptions.None
            ).Length - 1
        );
        Assert.Contains(
            "../../99/Premier film.mkv\r\n",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "../../99/Second film.mkv\r\n",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains(
            ".caption=" + firstTitle + "\r\n",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains(
            ".caption=" + secondTitle + "\r\n",
            text,
            StringComparison.Ordinal
        );
        string letterDirectory = Assert.Single(
            Directory.EnumerateDirectories(alphabet),
            path =>
                string.Equals(
                    Path.GetFileName(path),
                    "L",
                    StringComparison.OrdinalIgnoreCase
                )
        );
        Assert.Equal("L", Path.GetFileName(letterDirectory));
    }

    [Fact]
    public void Export_SagaOrder_UsesLegacyNumericCaptionsInXmlOrder()
    {
        using ExportFixture fixture = new();
        AddMovie(
            fixture,
            "Premier film.mkv",
            "Z : premier dans la saga"
        );
        AddMovie(
            fixture,
            "Second film.mkv",
            "A : second dans la saga"
        );
        new XDocument(
            new XElement(
                "groups",
                new XElement(
                    "saga",
                    new XElement("name", "Ordre personnalisé"),
                    new XElement(
                        "sort_name",
                        "Ordre personnalisé"
                    ),
                    new XElement("movie", "Premier film.mkv"),
                    new XElement("movie", "Second film.mkv")
                )
            )
        ).Save(Path.Combine(fixture.Root, "groups.xml"));
        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        string text = File.ReadAllText(
            Path.Combine(
                result.Directory,
                "00",
                "98",
                "Ordre personnalisé",
                "dune_folder.txt"
            )
        );
        Assert.Contains(
            "item.0.caption=00\r\n",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "item.1.caption=01\r\n",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "item.0.media_url=../../99/Premier film.mkv\r\n",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "item.1.media_url=../../99/Second film.mkv\r\n",
            text,
            StringComparison.Ordinal
        );
        Assert.Equal("no", Lines(text)["paint_captions"]);
    }

    private static Dictionary<string, string> Lines(string text)
    {
        return text.Split(
                "\r\n",
                StringSplitOptions.RemoveEmptyEntries
            )
            .Where(line => line.Contains('='))
            .Select(line => line.Split('=', 2))
            .ToDictionary(
                parts => parts[0],
                parts => parts[1],
                StringComparer.Ordinal
            );
    }

    private static void AddMovie(
        ExportFixture fixture,
        string name,
        string title
    )
    {
        string source = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "data.xml"
        );
        XDocument data = XDocument.Load(source);
        data.Root!.Element("item")!.Element("filename")!.Value =
            name;
        XElement info = data.Root.Element("info")!;
        info.Element("title")!.Value = title;
        info.Element("sort_title")!.Value = title;
        string directory = Path.Combine(
            fixture.Root,
            "data",
            name
        );
        Directory.CreateDirectory(directory);
        data.Save(Path.Combine(directory, "data.xml"));
    }
}
