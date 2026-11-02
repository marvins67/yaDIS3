using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyProcessingStateTests
{
    [Theory]
    [InlineData("0", "0")]
    [InlineData("1", "1")]
    [InlineData("2", "1")]
    [InlineData("3", "1")]
    [InlineData("4", "1")]
    public async Task MarkProcessed_LegacyStatus_PreservesZeroAndExistingMetadata(
        string initial,
        string expected
    )
    {
        using ExportFixture fixture = new();
        string path = MoviePath(fixture);
        XDocument data = XDocument.Load(path);
        data.Root!.Element("item")!.Element("status")!.Value =
            initial;
        data.Save(path);
        using WorkspaceRepository repository = new();
        MediaCard card = repository
            .Open(fixture.Root)
            .Cards.Single(card => !card.IsSeries);
        card.MarkProcessed(new DateOnly(2026, 11, 2));
        await repository.SaveAsync(card);
        data.Root.Element("item")!.Element("status")!.Value =
            expected;
        Assert.True(
            XNode.DeepEquals(data, XDocument.Load(path))
        );
    }

    [Fact]
    public async Task MarkProcessed_EmptyDate_RecordsProcessingDate()
    {
        using ExportFixture fixture = new();
        string path = MoviePath(fixture);
        XDocument data = XDocument.Load(path);
        data.Root!.Element("item")!.Element("date")!.Value = "";
        data.Save(path);
        using WorkspaceRepository repository = new();
        MediaCard card = repository
            .Open(fixture.Root)
            .Cards.Single(card => !card.IsSeries);
        card.MarkProcessed(new DateOnly(2026, 11, 2));
        await repository.SaveAsync(card);
        Assert.Equal(
            "2026-11-02",
            XDocument
                .Load(path)
                .Root!.Element("item")!
                .Element("date")!
                .Value
        );
    }

    [Fact]
    public async Task ProcessAsync_FailedRender_KeepsErrorStatusAndOriginalXml()
    {
        using ExportFixture fixture = new();
        PrepareDvd(fixture);
        string path = MoviePath(fixture);
        byte[] original = File.ReadAllBytes(path);
        await File.WriteAllBytesAsync(
            Path.Combine(
                Path.GetDirectoryName(path)!,
                "fanart.jpg"
            ),
            [1, 2, 3]
        );
        using WorkspaceRepository repository = new();
        MediaCard card = repository
            .Open(fixture.Root)
            .Cards.Single(card => !card.IsSeries);
        MediaProcessingService processor = new(
            repository,
            new FixedTimeProvider()
        );
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            processor.ProcessAsync(
                card,
                ExportFixture.Template(),
                new RenderOptions(),
                fixture.Root
            )
        );
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal(
            "3",
            card.Snapshot()
                .Document.Root!.Element("item")!
                .Element("status")!
                .Value
        );
    }

    [Fact]
    public async Task ProcessAsync_ErrorDvdCard_BecomesExportableAfterSuccessfulRender()
    {
        using ExportFixture fixture = new();
        PrepareDvd(fixture);
        using WorkspaceRepository repository = new();
        Workspace workspace = repository.Open(fixture.Root);
        MediaCard card = workspace.Cards.Single(card =>
            !card.IsSeries
        );
        MediaProcessingService processor = new(
            repository,
            new FixedTimeProvider()
        );
        await processor.ProcessAsync(
            card,
            ExportFixture.Template(),
            new RenderOptions(),
            fixture.Root
        );
        Assert.Equal(
            "1",
            XDocument
                .Load(card.FilePath)
                .Root!.Element("item")!
                .Element("status")!
                .Value
        );
        Assert.False(card.IsDirty);
        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        Assert.Equal(2, result.MediaCount);
        Assert.True(
            File.Exists(
                Path.Combine(
                    result.Directory,
                    "00",
                    "99",
                    ExportFixture.MovieName,
                    "dune_folder.txt"
                )
            )
        );
        Assert.True(
            File.Exists(
                Path.Combine(
                    result.Directory,
                    "00",
                    "11",
                    "DVD",
                    "dune_folder.txt"
                )
            )
        );
        Assert.True(
            File.Exists(
                Path.Combine(
                    result.Directory,
                    "00",
                    "05",
                    "A",
                    "Claud Allister",
                    "dune_folder.txt"
                )
            )
        );
    }

    [Fact]
    public async Task ProcessAsync_CanceledRender_KeepsOriginalStatus()
    {
        using ExportFixture fixture = new();
        PrepareDvd(fixture);
        using WorkspaceRepository repository = new();
        MediaCard card = repository
            .Open(fixture.Root)
            .Cards.Single(card => !card.IsSeries);
        byte[] original = File.ReadAllBytes(card.FilePath);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        MediaProcessingService processor = new(
            repository,
            new FixedTimeProvider()
        );
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                processor.ProcessAsync(
                    card,
                    ExportFixture.Template(),
                    new RenderOptions(),
                    fixture.Root,
                    cancellationToken: cancellation.Token
                )
        );
        Assert.Equal(original, File.ReadAllBytes(card.FilePath));
        Assert.False(card.IsDirty);
    }

    private static void PrepareDvd(ExportFixture fixture)
    {
        string path = MoviePath(fixture);
        XDocument data = XDocument.Load(path);
        data.Root!.Element("item")!.Element("status")!.Value =
            "3";
        data
            .Root.Element("mediainfo")!
            .Element("format")!
            .Value = "DVD";
        data.Root.Element("info")!.Element("actor")!.Value =
            "Claud Allister";
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
