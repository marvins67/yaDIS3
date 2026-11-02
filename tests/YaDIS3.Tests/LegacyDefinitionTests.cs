using Xunit;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyDefinitionTests
{
    [Theory]
    [InlineData("Movie")]
    [InlineData("Series")]
    [InlineData("Season")]
    [InlineData("Episode")]
    [InlineData("MovieListing")]
    [InlineData("SeriesListing")]
    [InlineData("MovieMenu")]
    [InlineData("Root")]
    public void Encode_ExtractedYadis2Definition_MatchesFrozenBytes(
        string kind
    )
    {
        string assets = Path.Combine(
            AppContext.BaseDirectory,
            "Assets"
        );
        var template = DuneTemplate.Load(
            Path.Combine(assets, "Templates", "Origins"),
            Path.Combine(assets, "Images")
        );
        var definitions = new LegacyDuneDefinitions(template);
        const string Url =
            "smb://nas/Films/L’été d’Adèle [1080p].mkv";
        string content = kind switch
        {
            "Movie" => definitions.Movie(Url),
            "Series" => definitions.Series(),
            "Season" => definitions.Season(),
            "Episode" => LegacyDuneDefinitions.Episode(Url),
            "MovieListing" => definitions.MovieListing(),
            "SeriesListing" => definitions.SeriesListing(),
            "MovieMenu" => definitions.MovieMenu(),
            "Root" => definitions.Root(),
            _ => throw new ArgumentException(
                "Type de fixture inconnu.",
                nameof(kind)
            ),
        };
        byte[] expected = File.ReadAllBytes(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                kind + ".txt"
            )
        );
        Assert.Equal(
            expected,
            LegacyDuneEncoding.Encode(content, false)
        );
    }
}
