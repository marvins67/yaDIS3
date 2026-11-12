using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class LegacyPersonIndexTests
{
    // AddToIndex dans yaDIS2 ignore ce nom lorsque Substring dépasse sa longueur.
    [Theory]
    [InlineData("actor", "Burt Lancaster ", "00/05")]
    [InlineData("actor", "   ", "00/05")]
    [InlineData("director", "Stanley Kubrick ", "00/06")]
    [InlineData("director", " ", "00/06")]
    [InlineData("actor", "\t", "00/05")]
    [InlineData("director", "\u00a0", "00/06")]
    public void Create_PersonWithTrailingSpace_SkipsOnlyInvalidLegacyIndex(
        string field,
        string name,
        string directory
    )
    {
        XElement info = new(
            "info",
            new XElement(field, name),
            new XElement(field, "John Carpenter")
        );
        IReadOnlyList<LegacyIndexEntry> entries =
            LegacyMovieIndexes.Create(info, null);
        Assert.Single(entries);
        Assert.Equal(
            new LegacyIndexEntry(
                directory + "/C/John Carpenter",
                "John Carpenter"
            ),
            entries[0]
        );
    }

    [Theory]
    [InlineData("Burt Lancaster", "L/Burt Lancaster")]
    [InlineData("Burt  Lancaster", "L/Burt  Lancaster")]
    [InlineData(" Walter Goggins", "G/ Walter Goggins")]
    [InlineData("Björk", "B/Björk")]
    [InlineData("Shigesato Itoi", "I/Shigesato Itoi")]
    [InlineData("Hitoshi Takagi", "T/Hitoshi Takagi")]
    [InlineData("糸井重里", "糸/糸井重里")]
    [InlineData("高木均", "高/高木均")]
    [InlineData("Eric Summer", "S/Eric Summer")]
    [InlineData("Éric Summer", "S/Éric Summer")]
    [InlineData("E\u0301ric Summer", "S/E\u0301ric Summer")]
    public void Create_ValidPersonName_PreservesLegacySpelling(
        string name,
        string expected
    )
    {
        XElement info = new("info", new XElement("actor", name));
        Assert.Equal(
            new LegacyIndexEntry("00/05/" + expected, name),
            Assert.Single(LegacyMovieIndexes.Create(info, null))
        );
    }
}
