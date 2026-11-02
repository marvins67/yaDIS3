using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class LegacyMovieIndexTests
{
    [Theory]
    [InlineData("90", "1h 30")]
    [InlineData("91", "2h 00")]
    [InlineData("120", "2h 00")]
    [InlineData("121", "2h 30")]
    [InlineData("150", "2h 30")]
    [InlineData("151", "3h 00")]
    public void Create_Runtime_UsesLegacyHalfHourBuckets(
        string runtime,
        string expected
    )
    {
        var info = new XElement(
            "info",
            new XElement("runtime", runtime)
        );
        Assert.Contains(
            new LegacyIndexEntry("00/07/" + expected, expected),
            LegacyMovieIndexes.Create(info, null)
        );
    }

    [Fact]
    public void Create_Metadata_PreservesLegacyBucketsAndFiveActorLimit()
    {
        var info = new XElement(
            "info",
            new XElement("year", "2023"),
            new XElement("rating", "8,7"),
            Enumerable
                .Range(1, 6)
                .Select(value => new XElement(
                    "actor",
                    "Acteur " + value
                ))
        );
        var technical = new XElement(
            "mediainfo",
            new XElement("format", "BDAV")
        );
        IReadOnlyList<LegacyIndexEntry> entries =
            LegacyMovieIndexes.Create(info, technical);
        Assert.Contains(
            new LegacyIndexEntry("00/09/2020", "2020"),
            entries
        );
        Assert.Contains(
            new LegacyIndexEntry("00/10/8", "8"),
            entries
        );
        Assert.Contains(
            new LegacyIndexEntry("00/11/M2TS", "M2TS"),
            entries
        );
        Assert.Equal(
            5,
            entries.Count(entry =>
                entry.Directory.StartsWith(
                    "00/05/",
                    StringComparison.Ordinal
                )
            )
        );
    }
}
