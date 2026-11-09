using System.Xml.Linq;
using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyAlphabetOrderTests
{
    [Fact]
    public void Export_Alphabet_AppendsSagasAfterMoviesWithoutIndexGaps()
    {
        using ExportFixture fixture = new();
        AddMovie(fixture, "one.mkv", "Bravo");
        AddMovie(fixture, "two.mkv", "beta");
        AddMovie(fixture, "member.mkv", "B member");
        XElement groups = new(
            "groups",
            Saga(
                "B saga two",
                "member.mkv",
                ExportFixture.MovieName
            ),
            Saga("B saga one", "member.mkv"),
            new XElement(
                "selection",
                new XAttribute("enabled", "yes"),
                new XElement("name", "Favorites"),
                new XElement("movie", "one.mkv")
            )
        );
        groups.Save(Path.Combine(fixture.Root, "groups.xml"));
        fixture.ProcessIndexes();
        MediaContext[] cards = OrderedCards(fixture);
        DuneExporter exporter = ExportFixture.Exporter();
        ExportResult result = exporter.Export(
            cards,
            fixture.Destination
        );
        string text = Alphabet(result, "B");
        string[] expected =
        [
            "item.0.caption=beta",
            "item.1.caption=Bravo",
            "item.2.caption=B saga two",
            "item.3.caption=B saga one",
        ];
        Assert.Equal(expected, Captions(text));
        Assert.Contains(
            "item.2.media_url=../../98/B saga two\r\n",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "item.3.media_url=../../98/B saga one\r\n",
            text,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "member.mkv",
            text,
            StringComparison.Ordinal
        );
        AssertNavigationTargets(result, text, "B");
        AssertGenreMembership(result, "Drame");
        AssertGenreMembership(result, "Adventure");
        AssertReexport(fixture, exporter, cards, text);
    }

    [Theory]
    [InlineData("À couteaux tirés", null)]
    [InlineData("Été", null)]
    [InlineData("à nouveau", null)]
    [InlineData("42 films", "0-9")]
    [InlineData("! Film", "0-9")]
    [InlineData("alpha", "A")]
    public void Export_Initial_UsesOnlyPrecreatedLegacyDirectories(
        string title,
        string? letter
    )
    {
        using ExportFixture fixture = new();
        AddMovie(fixture, "initial.mkv", title);
        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        if (letter is null)
        {
            Assert.False(
                Directory.Exists(
                    Path.Combine(
                        result.Directory,
                        "00",
                        "03",
                        title[..1]
                    )
                )
            );
        }
        else
        {
            Assert.Contains(
                "item.0.caption=" + title + "\r\n",
                Alphabet(result, letter),
                StringComparison.Ordinal
            );
        }

        Assert.True(
            File.Exists(
                Path.Combine(
                    result.Directory,
                    "00",
                    "99",
                    "initial.mkv",
                    "dune_folder.txt"
                )
            )
        );
    }

    private static MediaContext[] OrderedCards(
        ExportFixture fixture
    )
    {
        MediaContext[] cards = fixture.Cards();
        string[] order =
        [
            "two.mkv",
            "member.mkv",
            "one.mkv",
            ExportFixture.MovieName,
            ExportFixture.SeriesName,
        ];
        return order
            .Select(name =>
                cards.Single(card =>
                    card.DataDirectory
                    == Path.Combine(fixture.Root, "data", name)
                )
            )
            .ToArray();
    }

    private static void AssertGenreMembership(
        ExportResult result,
        string genre
    )
    {
        string text = File.ReadAllText(
            Path.Combine(
                result.Directory,
                "00",
                "04",
                genre,
                "dune_folder.txt"
            )
        );
        Assert.Contains(
            "../../99/one.mkv\r\n",
            text,
            StringComparison.Ordinal
        );
    }

    private static void AssertReexport(
        ExportFixture fixture,
        DuneExporter exporter,
        MediaContext[] cards,
        string expected
    )
    {
        string obsolete = Path.Combine(
            fixture.Destination,
            "00",
            "03",
            "À"
        );
        Directory.CreateDirectory(obsolete);
        File.WriteAllText(
            Path.Combine(obsolete, "dune_folder.txt"),
            "obsolete"
        );
        ExportResult repeated = exporter.Export(
            cards,
            fixture.Destination
        );
        Assert.False(Directory.Exists(obsolete));
        Assert.Equal(expected, Alphabet(repeated, "B"));
    }

    private static XElement Saga(
        string name,
        params string[] members
    )
    {
        return new XElement(
            "saga",
            new XElement("name", name),
            new XElement("sort_name", name),
            members.Select(member =>
                new XElement("movie", member)
            )
        );
    }

    private static string Alphabet(
        ExportResult result,
        string letter
    )
    {
        return File.ReadAllText(
            Path.Combine(
                result.Directory,
                "00",
                "03",
                letter,
                "dune_folder.txt"
            )
        );
    }

    private static string[] Captions(string text)
    {
        return text.Split(
                "\r\n",
                StringSplitOptions.RemoveEmptyEntries
            )
            .Where(line =>
                line.StartsWith("item.", StringComparison.Ordinal)
                && line.Contains(
                    ".caption=",
                    StringComparison.Ordinal
                )
            )
            .ToArray();
    }

    private static void AssertNavigationTargets(
        ExportResult result,
        string text,
        string letter
    )
    {
        string directory = Path.Combine(
            result.Directory,
            "00",
            "03",
            letter
        );
        foreach (
            string line in text.Split("\r\n")
                .Where(line =>
                    line.Contains(
                        ".media_url=",
                        StringComparison.Ordinal
                    )
                )
        )
        {
            string target = line.Split('=', 2)[1];
            Assert.True(
                File.Exists(
                    Path.Combine(
                        directory,
                        target,
                        "dune_folder.txt"
                    )
                )
            );
        }
    }

    private static void AddMovie(
        ExportFixture fixture,
        string name,
        string title
    )
    {
        XDocument data = XDocument.Load(
            Path.Combine(
                fixture.Root,
                "data",
                ExportFixture.MovieName,
                "data.xml"
            )
        );
        data.Root!.Element("item")!.Element("filename")!.Value =
            name;
        XElement info = data.Root.Element("info")!;
        info.Element("title")!.Value = title;
        info.Element("sort_title")!.Value = title;
        info.Add(new XElement("genre", "Adventure"));
        string directory = Path.Combine(
            fixture.Root,
            "data",
            name
        );
        Directory.CreateDirectory(directory);
        data.Save(Path.Combine(directory, "data.xml"));
    }
}
