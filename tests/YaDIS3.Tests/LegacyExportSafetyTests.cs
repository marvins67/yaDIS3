using System.Text;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyExportSafetyTests
{
    [Fact]
    public void Export_RecognizedYadis2Catalog_ReplacesOnlyAfterSuccess()
    {
        using ExportFixture fixture = new();
        Directory.CreateDirectory(fixture.Destination);
        File.WriteAllBytes(
            Path.Combine(fixture.Destination, "dune_folder.txt"),
            LegacyDuneEncoding.Encode(
                "storage_caption=yaDIS",
                false
            )
        );
        File.WriteAllText(
            Path.Combine(fixture.Destination, "old.txt"),
            "previous",
            Encoding.UTF8
        );
        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);
        Assert.Equal(fixture.Destination, result.Directory);
        Assert.False(
            File.Exists(
                Path.Combine(result.Directory, "old.txt")
            )
        );
        Assert.Equal(
            "previous",
            File.ReadAllText(
                Path.Combine(
                    fixture.Destination + ".previous",
                    "old.txt"
                )
            )
        );
    }

    [Fact]
    public void Export_UnrelatedDestination_DoesNotDeleteUserFiles()
    {
        using ExportFixture fixture = new();
        Directory.CreateDirectory(fixture.Destination);
        string path = Path.Combine(
            fixture.Destination,
            "keep.txt"
        );
        File.WriteAllText(path, "keep", Encoding.UTF8);
        Assert.Throws<IOException>(() =>
            ExportFixture
                .Exporter()
                .Export(fixture.Cards(), fixture.Destination)
        );
        Assert.Equal("keep", File.ReadAllText(path));
    }

    [Fact]
    public void Export_CanceledOperation_DoesNotPublishPartialCatalog()
    {
        using ExportFixture fixture = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            ExportFixture
                .Exporter()
                .Export(
                    fixture.Cards(),
                    fixture.Destination,
                    cancellationToken: cancellation.Token
                )
        );
        Assert.False(Directory.Exists(fixture.Destination));
    }
}
