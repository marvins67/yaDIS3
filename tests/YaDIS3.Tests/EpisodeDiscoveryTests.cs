using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class EpisodeDiscoveryTests
{
    [Theory]
    [InlineData(".iso")]
    [InlineData(".mkv")]
    [InlineData(".mk3d")]
    [InlineData(".ts")]
    [InlineData(".ps")]
    [InlineData(".mpg")]
    [InlineData(".mpeg")]
    [InlineData(".m2ts")]
    [InlineData(".mts")]
    [InlineData(".vob")]
    [InlineData(".avi")]
    [InlineData(".mov")]
    [InlineData(".m4v")]
    [InlineData(".mp4")]
    [InlineData(".qt")]
    [InlineData(".asf")]
    [InlineData(".wmv")]
    [InlineData(".divx")]
    [InlineData(".ogg")]
    [InlineData(".ogm")]
    [InlineData(".ogv")]
    [InlineData(".img")]
    public void Detect_ExtensionHistorique_ReconnaitLaVideo(string extension)
    {
        using ExportFixture fixture = new();
        string directory = Path.Combine(fixture.Root, "episodes");
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "S01E03" + extension.ToUpperInvariant());
        File.WriteAllBytes(file, []);
        File.WriteAllBytes(Path.Combine(directory, "S01E04.txt"), []);
        VideoEpisode episode = Assert.Single(EpisodeScanner.Detect(directory));
        Assert.Equal(new VideoEpisode(1, 3, file), episode);
    }

    [Theory]
    [InlineData("S01E01.S01E03.mkv", 1, 3)]
    [InlineData("1x01.1x03.mkv", 1, 3)]
    [InlineData("S01E01-03.mkv", 1, 2, 3)]
    [InlineData("1x01-03.mkv", 1, 2, 3)]
    public void Parse_EpisodesMultiples_ReproduitLesNumerosHistoriques(
        string name, params int[] expected)
    {
        Assert.Equal(expected, EpisodeScanner.Parse(name).Select(item => item.Number));
    }
}
