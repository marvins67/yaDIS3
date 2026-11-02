using System.Xml.Linq;
using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyIndexMembershipTests
{
    // CreateIndexItem recherche Caption + "|" dans les entrées déjà écrites.
    [Theory]
    [InlineData("L’été d’Adèle", "L’été d’Adèle", 1)]
    [InlineData("L’été d’Adèle", "été d’Adèle", 1)]
    [InlineData("L’été d’Adèle", "l’été d’Adèle", 2)]
    [InlineData("L’été d’Adèle", "L’été d’Adèle [1080p].mkv", 1)]
    public void Export_MetadataIndexes_UsesLegacyCaptionMembership(
        string first,
        string second,
        int expected
    )
    {
        using ExportFixture fixture = new();
        AddVersion(fixture, first, second);
        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        string[] indexes =
        [
            "00/04/Drame",
            "00/05/E/Adèle Exarchopoulos",
            "00/06/K/Abdellatif Kechiche",
            "00/07/3h 00",
            "00/08/France",
            "00/09/2020",
            "00/10/8",
            "00/11/M2TS",
        ];
        foreach (string index in indexes)
        {
            string path = Path.Combine(
                result.Directory,
                index.Replace('/', Path.DirectorySeparatorChar),
                "dune_folder.txt"
            );
            Assert.Equal(expected, Captions(path).Length);
        }
        // Les listes principales de yaDIS2 conservent les différentes versions.
        Assert.Equal(
            2,
            Captions(
                Path.Combine(
                    result.Directory,
                    "00",
                    "00",
                    "dune_folder.txt"
                )
            ).Length
        );
    }

    [Fact]
    public void Export_IndexNamesDifferingByCase_MergesAllMoviesLikeWindows()
    {
        using ExportFixture fixture = new();
        string second = AddVersion(
            fixture,
            "Premier film",
            "Second film"
        );
        SetIndexNames(
            MoviePath(fixture),
            "Animation",
            "john smith",
            "jane doe",
            "FR",
            "Matroska"
        );
        SetIndexNames(
            second,
            "animation",
            "John Smith",
            "Jane Doe",
            "fr",
            "matroska"
        );
        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        foreach (
            string relative in new[]
            {
                "00/04/Animation",
                "00/05/S/john smith",
                "00/06/D/jane doe",
                "00/08/FR",
                "00/11/Matroska",
            }
        )
        {
            string directory = Path.Combine(
                result.Directory,
                relative.Replace(
                    '/',
                    Path.DirectorySeparatorChar
                )
            );
            Assert.Equal(
                2,
                Captions(
                    Path.Combine(directory, "dune_folder.txt")
                ).Length
            );
            string matching = Assert.Single(
                Directory.EnumerateDirectories(
                    Path.GetDirectoryName(directory)!
                ),
                path =>
                    string.Equals(
                        Path.GetFileName(path),
                        Path.GetFileName(directory),
                        StringComparison.OrdinalIgnoreCase
                    )
            );
            Assert.Equal(directory, matching);
        }
        Assert.Single(
            Directory.EnumerateDirectories(
                Path.Combine(result.Directory, "00", "05")
            ),
            path =>
                string.Equals(
                    Path.GetFileName(path),
                    "S",
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    private static string[] Captions(string path)
    {
        return File.ReadAllLines(path)
            .Where(line =>
                line.StartsWith(
                    "item.",
                    StringComparison.Ordinal
                )
                && line.Contains(
                    ".caption=",
                    StringComparison.Ordinal
                )
            )
            .ToArray();
    }

    private static string AddVersion(
        ExportFixture fixture,
        string first,
        string second
    )
    {
        string path = MoviePath(fixture);
        XDocument data = XDocument.Load(path);
        SetTitle(data, first);
        data.Save(path);
        const string Version =
            "L’été d’Adèle [version longue].mkv";
        data.Root!.Element("item")!.Element("filename")!.Value =
            Version;
        SetTitle(data, second);
        string directory = Path.Combine(
            fixture.Root,
            "data",
            Version
        );
        Directory.CreateDirectory(directory);
        string result = Path.Combine(directory, "data.xml");
        data.Save(result);
        return result;
    }

    private static void SetTitle(XDocument data, string title)
    {
        XElement info = data.Root!.Element("info")!;
        info.Element("title")!.Value = title;
        info.Element("sort_title")!.Value = title;
    }

    private static void SetIndexNames(
        string path,
        string genre,
        string actor,
        string director,
        string country,
        string format
    )
    {
        XDocument data = XDocument.Load(path);
        XElement info = data.Root!.Element("info")!;
        info.Element("genre")!.Value = genre;
        info.Element("actor")!.Value = actor;
        info.Element("director")!.Value = director;
        info.Element("country")!.Value = country;
        data
            .Root.Element("mediainfo")!
            .Element("format")!
            .Value = format;
        data.Save(path);
    }

    private static string MoviePath(ExportFixture fixture)
    {
        return Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "data.xml"
        );
    }
}
