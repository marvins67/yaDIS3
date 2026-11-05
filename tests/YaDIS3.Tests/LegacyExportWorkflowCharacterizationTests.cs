using System.Text;
using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

/// <summary>
/// Fige les conséquences actuelles de l'export avant son alignement
/// sur ProcessForm.bgwExport_DoWork et SyncInterface de yaDIS2.
/// Ces tests ne constituent pas une preuve de conformité à yaDIS2.
/// </summary>
public sealed class LegacyExportWorkflowCharacterizationTests
{
    [Fact]
    public void Export_DestinationAbsente_CreeLeRepertoire()
    {
        using ExportFixture fixture = new();
        Assert.False(Directory.Exists(fixture.Destination));

        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);

        Assert.Equal(fixture.Destination, result.Directory);
        Assert.True(
            File.Exists(Path.Combine(result.Directory, "dune_folder.txt"))
        );
    }

    [Fact]
    public void Export_DeuxExecutions_RemplaceLeCatalogueEtSauvegardeLePrecedent()
    {
        using ExportFixture fixture = new();
        DuneExporter exporter = ExportFixture.Exporter();
        exporter.Export(fixture.Cards(), fixture.Destination);

        string obsolete = Path.Combine(fixture.Destination, "obsolete.txt");
        File.WriteAllText(obsolete, "ancien contenu", Encoding.UTF8);

        exporter.Export(fixture.Cards(), fixture.Destination);

        Assert.False(File.Exists(obsolete));
        Assert.Equal(
            "ancien contenu",
            File.ReadAllText(
                Path.Combine(fixture.Destination + ".previous", "obsolete.txt")
            )
        );
        Assert.True(
            File.Exists(Path.Combine(fixture.Destination, "dune_folder.txt"))
        );
    }

    [Fact]
    public void Export_AnnulationAvantPublication_ConserveLeCataloguePrecedent()
    {
        using ExportFixture fixture = new();
        DuneExporter exporter = ExportFixture.Exporter();
        exporter.Export(fixture.Cards(), fixture.Destination);

        string marker = Path.Combine(fixture.Destination, "dune_folder.txt");
        byte[] previous = File.ReadAllBytes(marker);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            exporter.Export(
                fixture.Cards(),
                fixture.Destination,
                cancellationToken: cancellation.Token
            )
        );
        Assert.Equal(previous, File.ReadAllBytes(marker));
    }

    [Fact]
    public void Export_BibliothequeVide_RefuseSansCreerLaDestination()
    {
        using ExportFixture fixture = new();

        Assert.Throws<InvalidOperationException>(() =>
            ExportFixture.Exporter().Export([], fixture.Destination)
        );

        Assert.False(Directory.Exists(fixture.Destination));
    }

    [Fact]
    public void Export_FilmMultiFichiers_EcritLaPlaylistDansLeDossierSource()
    {
        using ExportFixture fixture = new();
        string sourceRoot = Path.Combine(fixture.Root, "videos");
        string movieDirectory = Path.Combine(
            sourceRoot,
            ExportFixture.MovieName
        );
        Directory.CreateDirectory(movieDirectory);
        File.WriteAllBytes(Path.Combine(movieDirectory, "part1.mkv"), []);
        File.WriteAllBytes(Path.Combine(movieDirectory, "part2.mkv"), []);

        string metadata = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "data.xml"
        );
        System.Xml.Linq.XDocument document =
            System.Xml.Linq.XDocument.Load(metadata);
        System.Xml.Linq.XElement item = document.Root!.Element("item")!;
        item.Element("path")!.Value = sourceRoot;
        item.Element("folder")!.Value = "yes";
        document.Save(metadata);

        ExportFixture.Exporter().Export(
            fixture.Cards(),
            fixture.Destination
        );

        string playlist = Path.Combine(movieDirectory, "playlist.m3u");
        Assert.True(File.Exists(playlist));
        Assert.Equal(
            "part1.mkv\r\npart2.mkv\r\n",
            File.ReadAllText(playlist, Encoding.UTF8)
        );
    }
}
