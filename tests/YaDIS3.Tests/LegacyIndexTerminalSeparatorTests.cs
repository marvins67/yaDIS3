using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyIndexTerminalSeparatorTests
{
    private const int PropertiesPerItem = 8;

    [Theory]
    [InlineData("center", true)]
    [InlineData("enter", true)]
    [InlineData("ter", true)]
    [InlineData("CENTER", true)]
    [InlineData("1", false)]
    [InlineData("Premier film", false)]
    [InlineData("film", false)]
    public void Update_CaptionMatchesLastValueSuffix_PreservesLegacyTerminalSeparator(
        string title,
        bool includeSecond
    )
    {
        using ExportFixture fixture = new();
        LegacyMovieIndexStore store = new(ExportFixture.Template(), new RenderOptions(AaiBackgrounds: true));
        store.Update(Movie(fixture, "Premier.mkv", "Premier film"), fixture.Root);
        MediaContext second = Movie(fixture, "Second.mkv", title);

        for (int attempt = 0; attempt < 2; attempt++)
        {
            store.Update(second, fixture.Root);
            AssertIndexes(fixture, title, includeSecond);
        }
    }

    private static void AssertIndexes(ExportFixture fixture, string title, bool includeSecond)
    {
        (string Relative, string Ancestor)[] indexes =
        [
            ("00/04/Drame", "../.."),
            ("00/05/E/Adèle Exarchopoulos", "../../.."),
            ("00/06/K/Abdellatif Kechiche", "../../.."),
            ("00/07/3h 00", "../.."),
            ("00/08/France", "../.."),
            ("00/09/2020", "../.."),
            ("00/10/8", "../.."),
            ("00/11/M2TS", "../.."),
        ];
        foreach ((string relative, string ancestor) in indexes)
        {
            AssertIndex(fixture, relative, ancestor, title, includeSecond);
        }
    }

    private static void AssertIndex(
        ExportFixture fixture,
        string relative,
        string ancestor,
        string title,
        bool includeSecond
    )
    {
        string[] lines = File.ReadAllLines(Path.Combine(fixture.Root, "interface",
            relative.Replace('/', Path.DirectorySeparatorChar), "dune_folder.txt"));
        string[] captions = includeSecond
            ? ["item.0.caption=Premier film", "item.1.caption=" + title]
            : ["item.0.caption=Premier film"];
        Assert.Equal(captions, lines.Where(line => line.Contains(".caption=", StringComparison.Ordinal)));
        Assert.Equal(captions.Length * PropertiesPerItem,
            lines.Count(line => line.StartsWith("item.", StringComparison.Ordinal)));
        Assert.Contains("item.0.media_url=" + ancestor + "/99/Premier.mkv", lines);
        if (includeSecond)
        {
            Assert.Contains("item.1.media_url=" + ancestor + "/99/Second.mkv", lines);
        }
        else
        {
            Assert.DoesNotContain(lines, line => line.Contains("Second.mkv", StringComparison.Ordinal));
        }
    }

    private static MediaContext Movie(ExportFixture fixture, string name, string title)
    {
        MediaContext movie = fixture.Cards().Single(card => !card.IsSeries);
        movie.Header.SetElementValue("filename", name);
        movie.Info.SetElementValue("sort_title", title);
        return movie;
    }
}
