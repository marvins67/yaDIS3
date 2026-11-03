using System.Text;
using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class SourceFolderScannerTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Scan_Films_RespecteFichiersDossiersEtExclusions(
        bool basic,
        bool expectedFile
    )
    {
        using ExportFixture fixture = new();
        string source = Path.Combine(fixture.Root, "Films");
        Directory.CreateDirectory(
            Path.Combine(source, "Le Retour Du Roi (2003)")
        );
        Directory.CreateDirectory(
            Path.Combine(source, "_interface")
        );
        Directory.CreateDirectory(
            Path.Combine(source, "Film~temporaire")
        );
        File.WriteAllBytes(
            Path.Combine(source, "Wall·E [FR].ISO"),
            []
        );
        File.WriteAllBytes(
            Path.Combine(source, "informations.txt"),
            []
        );
        new SourceFolderScanner().Scan(
            fixture.Root,
            [new(source, 0, "smb://nas/Films")],
            basic
        );
        Assert.True(
            Directory.Exists(
                Path.Combine(
                    fixture.Root,
                    "data",
                    "Le Retour Du Roi (2003)"
                )
            )
        );
        Assert.Equal(
            expectedFile,
            Directory.Exists(
                Path.Combine(
                    fixture.Root,
                    "data",
                    "Wall·E [FR].ISO"
                )
            )
        );
        Assert.False(
            Directory.Exists(
                Path.Combine(fixture.Root, "data", "_interface")
            )
        );
        Assert.False(
            Directory.Exists(
                Path.Combine(
                    fixture.Root,
                    "data",
                    "Film~temporaire"
                )
            )
        );
        Assert.False(
            Directory.Exists(
                Path.Combine(
                    fixture.Root,
                    "data",
                    "informations.txt"
                )
            )
        );
    }

    [Theory]
    [InlineData(false, "1")]
    [InlineData(true, "3")]
    public void Scan_ChangementSource_ConserveInfosEtMarqueRetraitementSeulementSiCheminDuneChange(
        bool changed,
        string status
    )
    {
        using ExportFixture fixture = new();
        string source = Path.Combine(fixture.Root, "Films");
        Directory.CreateDirectory(source);
        File.WriteAllBytes(
            Path.Combine(source, ExportFixture.MovieName),
            []
        );
        string data = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "data.xml"
        );
        XElement expected = new(
            XDocument.Load(data).Root!.Element("info")!
        );
        new SourceFolderScanner().Scan(
            fixture.Root,
            [
                new(
                    source,
                    0,
                    changed
                        ? "smb://nas/Nouveau"
                        : "smb://nas/Films"
                ),
            ],
            false
        );
        XDocument actual = XDocument.Load(data);
        Assert.True(
            XNode.DeepEquals(
                expected,
                actual.Root!.Element("info")
            )
        );
        Assert.Equal(
            status,
            actual.Root.Element("item")!.Element("status")!.Value
        );
        Assert.Equal(
            source,
            actual.Root.Element("item")!.Element("path")!.Value
        );
    }

    [Fact]
    public void Scan_Series_PrendSeulementLesDossiersDirects()
    {
        using ExportFixture fixture = new();
        string source = Path.Combine(fixture.Root, "Séries");
        Directory.CreateDirectory(
            Path.Combine(source, "Dragon Ball Yabai", "Saison 1")
        );
        File.WriteAllBytes(
            Path.Combine(source, "S01E01.mkv"),
            []
        );
        new SourceFolderScanner().Scan(
            fixture.Root,
            [new(source, 1, "smb://nas/Series")],
            false
        );
        Assert.True(
            Directory.Exists(
                Path.Combine(
                    fixture.Root,
                    "data",
                    "Dragon Ball Yabai"
                )
            )
        );
        Assert.False(
            Directory.Exists(
                Path.Combine(fixture.Root, "data", "Saison 1")
            )
        );
        Assert.False(
            Directory.Exists(
                Path.Combine(fixture.Root, "data", "S01E01.mkv")
            )
        );
    }

    [Fact]
    public void Scan_MediaAbsent_ConserveLaFicheAvecStatutDeux()
    {
        using ExportFixture fixture = new();
        string source = Path.Combine(
            fixture.Root,
            "disque déconnecté"
        );
        string data = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "data.xml"
        );
        XDocument document = XDocument.Load(data);
        document
            .Root!.Element("item")!
            .SetElementValue("path", source);
        document.Save(data);
        new SourceFolderScanner().Scan(
            fixture.Root,
            [new(source, 0, "smb://nas/Films")],
            false
        );
        Assert.Equal(
            "2",
            XDocument
                .Load(data)
                .Root!.Element("item")!
                .Element("status")!
                .Value
        );
        Assert.NotNull(
            XDocument.Load(data).Root!.Element("info")
        );
    }

    [Fact]
    public void Scan_AnnulationInitiale_NeModifieAucunFichier()
    {
        using ExportFixture fixture = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        string data = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "data.xml"
        );
        string expected = File.ReadAllText(data, Encoding.UTF8);
        Assert.Throws<OperationCanceledException>(() =>
            new SourceFolderScanner().Scan(
                fixture.Root,
                [],
                false,
                cancellation.Token
            )
        );
        Assert.Equal(
            expected,
            File.ReadAllText(data, Encoding.UTF8)
        );
    }
}
