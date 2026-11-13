using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class SingleInterfaceTreatmentTests
{
    [Fact]
    public void RemoveDeleted_DataFolderDiffersFromFilename_UsesCanonicalSeriesDirectory()
    {
        using ExportFixture fixture = new();
        MediaContext previous = fixture.Cards().Single(card => card.IsSeries);
        string data = Path.Combine(previous.DataDirectory, "data.xml");
        XElement xml = XElement.Load(data);
        xml.Element("item")!.SetElementValue("filename", "Nom de série distinct");
        xml.Element("info")!.Elements("episode").Remove();
        xml.Save(data);
        MediaContext current = fixture.Cards().Single(card => card.IsSeries);
        string output = LegacyExportPaths.WorkspaceMedia(fixture.Root, "Nom de série distinct", true);
        string episode = Path.Combine(output, "01", "01_000_001");
        Directory.CreateDirectory(episode);
        File.WriteAllText(Path.Combine(episode, "marker.txt"), "ancien épisode");

        EpisodeTreatmentCleanup.RemoveDeleted(previous, current, fixture.Root);

        Assert.False(Directory.Exists(episode));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Process_RepeatedTreatment_UsesOnlyHistoricalInterface(bool series)
    {
        using ExportFixture fixture = new();
        MediaContext media = fixture.Cards().Single(card => card.IsSeries == series);
        string output = LegacyExportPaths.WorkspaceMedia(fixture.Root,
            media.Header.Element("filename")!.Value, series);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            ProcessingResult result = DuneExporter.Process(media, ExportFixture.Template(),
                new RenderOptions(AaiBackgrounds: true), fixture.Root);
            Assert.True(File.Exists(result.PreviewPath));
            Assert.True(File.Exists(Path.Combine(output, "dune_folder.txt")));
            Assert.True(File.Exists(Path.Combine(output, "icon.aai")));
            Assert.False(Directory.Exists(Path.Combine(fixture.Root, "interface-yaDIS3")));
        }
        Assert.Empty(Directory.EnumerateDirectories(Path.GetDirectoryName(output)!, "_*.previous"));
        Assert.All(Directory.EnumerateDirectories(fixture.Root), path =>
            Assert.True(Path.GetFileName(path) is "data" or "interface"));
    }

    [Theory]
    [InlineData("preview.jpg", "render.json")]
    [InlineData("PREVIEW.JPG", "RENDER.JSON")]
    public void Synchronize_InternalTreatmentFiles_AreNotExported(string preview, string manifest)
    {
        using ExportFixture fixture = new();
        string source = Path.Combine(fixture.Root, "interface", "01", "Series");
        Directory.CreateDirectory(source);
        foreach (string name in new[] { preview, manifest, "icon.aai" })
        {
            File.WriteAllText(Path.Combine(source, name), "test");
        }
        new LegacyExportSynchronizer().Synchronize(Path.Combine(fixture.Root, "interface"), fixture.Destination);
        string output = Path.Combine(fixture.Destination, "01", "Series");
        Assert.True(File.Exists(Path.Combine(output, "icon.aai")));
        Assert.False(File.Exists(Path.Combine(output, preview)));
        Assert.False(File.Exists(Path.Combine(output, manifest)));
    }

    [Fact]
    public void Process_LegacyCardWithoutManifest_CanBeRetreatedInPlace()
    {
        using ExportFixture fixture = new();
        MediaContext media = fixture.Cards().Single(card => !card.IsSeries);
        string output = LegacyExportPaths.WorkspaceMedia(fixture.Root,
            media.Header.Element("filename")!.Value, false);
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, "dune_folder.txt"),
            LegacyDuneEncoding.Encode("icon_path=icon.aai", true));

        DuneExporter.Process(media, ExportFixture.Template(),
            new RenderOptions(AaiBackgrounds: true), fixture.Root);

        Assert.True(File.Exists(Path.Combine(output, "icon.aai")));
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "interface-yaDIS3")));
    }
}
