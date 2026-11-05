using System.Text;
using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

/// <summary>
/// Caractérise les règles de ProcessForm.SyncInterface (yaDIS2).
/// </summary>
public sealed class LegacyExportSynchronizationTests
{
    [Fact]
    public void Synchronize_FichierAbsent_CopieDepuisLeWorkspace()
    {
        using ExportFixture fixture = new();
        string source = Path.Combine(fixture.Root, "source");
        string destination = Path.Combine(fixture.Root, "target");
        Directory.CreateDirectory(source);
        File.WriteAllText(
            Path.Combine(source, "dune_folder.txt"),
            "navigation",
            Encoding.UTF8
        );

        new LegacyExportSynchronizer().Synchronize(source, destination);

        Assert.Equal(
            "navigation",
            File.ReadAllText(Path.Combine(destination, "dune_folder.txt"))
        );
    }

    [Fact]
    public void Synchronize_DatesIdentiques_ConserveLeContenuDestination()
    {
        using ExportFixture fixture = new();
        string source = Path.Combine(fixture.Root, "source");
        string destination = Path.Combine(fixture.Root, "target");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        string first = Path.Combine(source, "icon.aai");
        string second = Path.Combine(destination, "icon.aai");
        File.WriteAllText(first, "source", Encoding.UTF8);
        File.WriteAllText(second, "destination", Encoding.UTF8);
        DateTime timestamp = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(first, timestamp);
        File.SetLastWriteTimeUtc(second, timestamp);

        new LegacyExportSynchronizer().Synchronize(source, destination);

        Assert.Equal("destination", File.ReadAllText(second));
    }

    [Fact]
    public void Synchronize_DatesDifferentes_RemplaceLeFichier()
    {
        using ExportFixture fixture = new();
        string source = Path.Combine(fixture.Root, "source");
        string destination = Path.Combine(fixture.Root, "target");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        string first = Path.Combine(source, "icon.aai");
        string second = Path.Combine(destination, "icon.aai");
        File.WriteAllText(first, "source", Encoding.UTF8);
        File.WriteAllText(second, "destination", Encoding.UTF8);
        DateTime timestamp = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(first, timestamp);
        File.SetLastWriteTimeUtc(second, timestamp.AddMinutes(-2));

        new LegacyExportSynchronizer().Synchronize(source, destination);

        Assert.Equal("source", File.ReadAllText(second));
    }

    [Fact]
    public void Synchronize_DossiersExclus_SupprimeLesAnciensFichiers()
    {
        using ExportFixture fixture = new();
        string source = Path.Combine(fixture.Root, "source");
        string destination = Path.Combine(fixture.Root, "target");
        Directory.CreateDirectory(Path.Combine(source, "_hidden"));
        Directory.CreateDirectory(Path.Combine(destination, "_hidden"));
        File.WriteAllText(
            Path.Combine(source, "_hidden", "source.txt"),
            "ne pas copier"
        );
        File.WriteAllText(
            Path.Combine(destination, "_hidden", "old.txt"),
            "obsolete"
        );

        new LegacyExportSynchronizer().Synchronize(source, destination);

        Assert.False(Directory.Exists(Path.Combine(destination, "_hidden")));
    }

    [Fact]
    public void Synchronize_AnnulationPrealable_NeModifieRien()
    {
        using ExportFixture fixture = new();
        string source = Path.Combine(fixture.Root, "source");
        string destination = Path.Combine(fixture.Root, "target");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "new.txt"), "source");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        new LegacyExportSynchronizer().Synchronize(
            source,
            destination,
            cancellation.Token
        );

        Assert.False(Directory.Exists(destination));
    }
}
