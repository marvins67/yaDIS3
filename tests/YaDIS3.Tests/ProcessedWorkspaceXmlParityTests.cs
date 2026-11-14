using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class ProcessedWorkspaceXmlParityTests
{
    [Theory]
    [InlineData("no", "no")]
    [InlineData("yes", "yes")]
    [InlineData(null, null)]
    public async Task MarkProcessed_SelectionTransitoire_RespecteLeCycleHistorique(
        string? before,
        string? expected
    )
    {
        using ExportFixture fixture = new();
        string file = Path.Combine(
            fixture.Root, "data", ExportFixture.MovieName, "data.xml"
        );
        XDocument document = XDocument.Load(file);
        XElement header = document.Root!.Element("item")!;
        header.Element("selection")?.Remove();
        if (before is not null)
        {
            header.Add(new XElement("selection", before));
        }
        header.SetElementValue("status", "3");
        header.SetElementValue("date", "");
        document.Save(file);

        using WorkspaceRepository repository = new();
        using MediaCard card = repository.Open(fixture.Root)
            .Cards.Single(item => !item.IsSeries);
        XElement infoBefore = new(card.Snapshot().Document.Root!.Element("info")!);

        card.MarkProcessed(new DateOnly(2026, 11, 14));
        await repository.SaveAsync(card);

        XDocument saved = XDocument.Load(file);
        XElement savedHeader = saved.Root!.Element("item")!;
        Assert.Equal(expected, savedHeader.Element("selection")?.Value);
        Assert.Equal(new DateTime(2026, 11, 14).ToString("d",
            System.Globalization.CultureInfo.CurrentCulture), savedHeader.Element("date")?.Value);
        Assert.Equal("1", savedHeader.Element("status")?.Value);
        Assert.True(XNode.DeepEquals(
            infoBefore, saved.Root.Element("info")!
        ));
    }

    [Fact]
    public async Task MarkProcessed_DateDejaPresente_NeReecritPasLesMetadonnees()
    {
        using ExportFixture fixture = new();
        string file = Path.Combine(
            fixture.Root, "data", ExportFixture.MovieName, "data.xml"
        );
        XDocument document = XDocument.Load(file);
        XElement header = document.Root!.Element("item")!;
        header.SetElementValue("date", "2024-03-02");
        header.SetElementValue("status", "0");
        header.Add(new XElement("selection", "no"));
        document.Save(file);

        using WorkspaceRepository repository = new();
        using MediaCard card = repository.Open(fixture.Root)
            .Cards.Single(item => !item.IsSeries);
        XElement infoBefore = new(card.Snapshot().Document.Root!.Element("info")!);
        card.MarkProcessed(new DateOnly(2026, 11, 14));
        await repository.SaveAsync(card);

        XDocument saved = XDocument.Load(file);
        XElement savedHeader = saved.Root!.Element("item")!;
        Assert.Equal("2024-03-02", savedHeader.Element("date")!.Value);
        Assert.Equal("0", savedHeader.Element("status")!.Value);
        Assert.Equal("no", savedHeader.Element("selection")?.Value);
        Assert.True(XNode.DeepEquals(
            infoBefore, saved.Root.Element("info")!
        ));
    }
}
