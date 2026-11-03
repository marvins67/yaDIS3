using System.Globalization;
using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class LegacyLibraryFilterTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Matches_StatutXml_ReprendLeDecalageYaDis2(
        int status
    )
    {
        using ExportFixture fixture = new();
        using MediaCard card = OpenMovie(fixture, status);
        XDocument groups = new(new XElement("groups"));
        // La présence d'un aperçu ne change jamais le statut.
        File.WriteAllBytes(
            Path.Combine(card.DataDirectory, "preview.jpg"),
            []
        );
        for (int index = 0; index <= 4; index++)
        {
            LibraryFilterKind kind = (LibraryFilterKind)index;
            bool expected = index == 0 || status == index - 1;
            Assert.Equal(
                expected,
                new LegacyLibraryFilter(kind).Matches(
                    card,
                    groups
                )
            );
            Assert.Equal(
                !expected,
                new LegacyLibraryFilter(
                    kind,
                    Inverted: true
                ).Matches(card, groups)
            );
        }
    }

    [Theory]
    [InlineData(LibraryFilterKind.Saga, "saga")]
    [InlineData(LibraryFilterKind.Selection, "selection")]
    [InlineData(LibraryFilterKind.AllSagas, "saga")]
    [InlineData(LibraryFilterKind.AllSelections, "selection")]
    public void Matches_GroupeDesactive_CompareLeNomDuFichier(
        LibraryFilterKind kind,
        string element
    )
    {
        using ExportFixture fixture = new();
        using MediaCard card = OpenMovie(fixture, 1);
        XDocument groups = new(
            new XElement(
                "groups",
                new XElement(
                    element,
                    new XAttribute("enabled", "no"),
                    new XElement("name", "L'été d'Arthur & Zoé"),
                    new XElement("movie", card.FolderName)
                )
            )
        );
        LegacyLibraryFilter filter = new(
            kind,
            "L'été d'Arthur & Zoé"
        );
        Assert.True(filter.Matches(card, groups));
        Assert.False(
            (filter with { Inverted = true }).Matches(
                card,
                groups
            )
        );
        groups.Descendants("movie").Single().Value =
            card.FolderName.ToUpperInvariant();
        Assert.False(filter.Matches(card, groups));
    }

    [Theory]
    [InlineData("Science-fiction", true)]
    [InlineData("Science", false)]
    [InlineData("science-fiction", false)]
    public void Matches_Genre_CompareLaValeurExacte(
        string name,
        bool expected
    )
    {
        using ExportFixture fixture = new();
        string file = MovieFile(fixture);
        XDocument xml = XDocument.Load(file);
        xml.Root!.Element("info")!
            .Add(
                new XElement(
                    "genres",
                    new XElement("genre", "Science-fiction")
                )
            );
        xml.Save(file);
        using MediaCard card = OpenMovie(fixture, 0);
        Assert.Equal(
            expected,
            new LegacyLibraryFilter(
                LibraryFilterKind.Genre,
                name
            ).Matches(card, new(new XElement("groups")))
        );
    }

    [Fact]
    public void ProcessingState_Edition_DevientNonVerifie()
    {
        using ExportFixture fixture = new();
        using MediaCard card = OpenMovie(fixture, 0);
        card.Title = "Titre modifié";
        Assert.Equal(
            MediaProcessingState.Unchecked,
            card.ProcessingState
        );
        Assert.True(card.IsDirty);
    }

    [Fact]
    public void ProcessingState_StatutAbsent_NeDevientPasVerifie()
    {
        using ExportFixture fixture = new();
        string file = MovieFile(fixture);
        XDocument xml = XDocument.Load(file);
        xml.Root!.Element("item")!.Element("status")!.Remove();
        xml.Save(file);
        using WorkspaceRepository repository = new();
        using MediaCard card = repository
            .Open(fixture.Root)
            .Cards.Single(value => !value.IsSeries);
        Assert.Equal(
            MediaProcessingState.Unknown,
            card.ProcessingState
        );
    }

    private static MediaCard OpenMovie(
        ExportFixture fixture,
        int status
    )
    {
        string file = MovieFile(fixture);
        XDocument xml = XDocument.Load(file);
        xml.Root!.Element("item")!
            .SetElementValue(
                "status",
                status.ToString(CultureInfo.InvariantCulture)
            );
        xml.Save(file);
        using WorkspaceRepository repository = new();
        return repository
            .Open(fixture.Root)
            .Cards.Single(card => !card.IsSeries);
    }

    private static string MovieFile(ExportFixture fixture) =>
        Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "data.xml"
        );
}
