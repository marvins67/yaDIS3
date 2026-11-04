using System.Net.Sockets;
using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class LegacyDunePathLabelTests
{
    [Theory]
    [InlineData("")]
    [InlineData(@"\\DUNE\DisqueSansIdentifiant\Films")]
    [InlineData(@"\\ServeurAbsent\Films")]
    public async Task ResolveAsync_Failure_UsesExactYaDis2FrenchLabel(
        string folder
    )
    {
        string file = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Legacy",
            "dune-path-label.fr.xml"
        );
        string expected = XDocument
            .Load(file)
            .Descendants("string")
            .Single(node =>
                (string?)node.Attribute("id") == "31"
            )
            .Value;
        DunePathEnvironment environment = new()
        {
            Failure = new SocketException(),
        };
        string actual = await new LegacyDunePathResolver(
            environment
        ).ResolveAsync(folder);
        Assert.Equal(expected, actual);
    }
}
