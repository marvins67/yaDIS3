using System.Xml.Linq;
using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class LegacyExportEpisodeParityTests
{
    [Theory]
    [InlineData("/Séries")]
    [InlineData("smb://nas/Séries")]
    [InlineData("storage_uuid://26b6a642067197ba/Séries")]
    public void Export_SeasonsAndSharedFile_PreservesConfiguredPlaybackRoot(string duneRoot)
    {
        using ExportFixture fixture = new();
        string data = Path.Combine(fixture.Root, "data", ExportFixture.SeriesName, "data.xml");
        XElement document = XElement.Load(data);
        XElement info = document.Element("info")!;
        info.Elements("episode").Remove();
        string[] keys = ["01_001_000", "01_002_000", "02_001_000"];
        foreach (string key in keys)
        {
            info.Add(new XElement("episode",
                new XAttribute("key", key),
                new XAttribute("season", int.Parse(key.AsSpan(0, 2), System.Globalization.CultureInfo.InvariantCulture)),
                new XAttribute("episode", int.Parse(key.AsSpan(3, 3), System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("title", "Épisode partagé"),
                new XElement("file", "/media/Series/SPY×FAMILY/Été [FR] + bonus.mkv")));
        }
        document.Save(data);
        DuneExporter exporter = new(
            ExportFixture.Template(), new RenderOptions(),
            [new PathMapping("/media/Series", duneRoot)],
            catalogOptions: new LegacyCatalogOptions { Categories = "00000000000" });

        ExportResult result = exporter.Export(fixture.Cards(), fixture.Destination);

        Assert.Equal(3, result.EpisodeCount);
        foreach (string key in keys)
        {
            string path = Path.Combine(result.Directory, "01", ExportFixture.SeriesName, key[..2], key, "dune_folder.txt");
            Assert.Contains(
                $"media_url={duneRoot}/SPY×FAMILY/Été [FR] + bonus.mkv",
                File.ReadAllLines(path));
        }
    }
}
