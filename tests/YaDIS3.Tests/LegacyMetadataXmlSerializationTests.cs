using System.Xml;
using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class LegacyMetadataXmlSerializationTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public async Task SaveAsync_AccentsEtChampsVides_ReproduitLesAffectationsHistoriques(bool series, int mode)
    {
        using ExportFixture fixture = new();
        string path = Path.Combine(fixture.Root, "data", series ? ExportFixture.SeriesName : ExportFixture.MovieName, "data.xml");
        XmlDocument expected = new();
        expected.Load(path);
        string title = mode == 1 ? "" : mode == 2 ? "   " : "Été & l'apostrophe \"citée\"";
        string plotText = mode == 1 ? "" : mode == 2 ? "   " : "  Début\r\nSuite & <fin>  ";
        LegacyXmlReference.Set(expected, "//info/title", title);
        XmlElement plot = expected.CreateElement("plot");
        plot.InnerText = plotText;
        expected.SelectSingleNode("//info")!.AppendChild(plot);
        if (series)
        {
            ((XmlElement)expected.SelectSingleNode("//info")!).SetAttribute("episode_edits_pending", "yes");
        }
        using WorkspaceRepository repository = new();
        using MediaCard card = repository.Open(fixture.Root).Cards.Single(card => card.IsSeries == series);
        card.Title = title;
        card.Plot = plotText;
        // L'édition marque aussi le statut comme dans le formulaire yaDIS3.
        LegacyXmlReference.Set(expected, "//item/status", card.Snapshot().Document.Descendants("status").Single().Value);
        await repository.SaveAsync(card);
        LegacyXmlReference.AssertFile(expected, path);
        byte[] first = File.ReadAllBytes(path);
        await repository.SaveAsync(card);
        Assert.Equal(first, File.ReadAllBytes(path));
        using MediaCard reread = repository.Open(fixture.Root).Cards.Single(card => card.IsSeries == series);
        XmlDocument parsed = new();
        parsed.Load(path);
        Assert.Equal(parsed.SelectSingleNode("//info/title")!.InnerText, reread.Title);
        Assert.Equal(parsed.SelectSingleNode("//info/plot")!.InnerText, reread.Plot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveAsync_EpisodeEdite_ReproduitOrdreAttributsEtTextes(bool replaceImage)
    {
        using ExportFixture fixture = new();
        string path = Path.Combine(fixture.Root, "data", ExportFixture.SeriesName, "data.xml");
        XmlDocument expected = new();
        expected.Load(path);
        foreach (string field in new[] { "plot", "aired", "rating", "director" })
        {
            XmlElement element = expected.CreateElement(field);
            element.InnerText = "";
            expected.SelectSingleNode("//episode")!.AppendChild(element);
        }
        File.WriteAllBytes(path, LegacyXmlReference.Bytes(expected));
        expected.RemoveChild(expected.FirstChild!);
        const string Title = "Épisode & été";
        LegacyXmlReference.Set(expected, "//episode/title", Title);
        ((XmlElement)expected.SelectSingleNode("//episode")!).SetAttribute("manual_edit", "yes");
        ((XmlElement)expected.SelectSingleNode("//info")!).SetAttribute("episode_edits_pending", "yes");
        using WorkspaceRepository repository = new();
        using MediaCard card = repository.Open(fixture.Root).Cards.Single(card => card.IsSeries);
        card.Episodes[0].Title = Title;
        if (replaceImage)
        {
            string image = Path.Combine(fixture.Root, "episode-source.jpg");
            File.WriteAllBytes(image, [1, 2, 3]);
            new ImageImporter(new CopyArtworkEncoder()).Import(card, ArtworkKind.Episode, image, card.Episodes[0]);
            XmlElement imageNode = expected.CreateElement("image");
            imageNode.InnerText = "";
            expected.SelectSingleNode("//episode")!.AppendChild(imageNode);
            ((XmlElement)expected.SelectSingleNode("//episode")!).SetAttribute("manual_image", "0x1.jpg");
        }
        await repository.SaveAsync(card);
        LegacyXmlReference.AssertFile(expected, path);
        using MediaCard reread = repository.Open(fixture.Root).Cards.Single(card => card.IsSeries);
        Assert.Equal(Title, reread.Episodes[0].Title);
        Assert.True(reread.Episodes[0].IsManual);
        byte[] episodeBytes = File.ReadAllBytes(path);
        await repository.SaveAsync(card);
        Assert.Equal(episodeBytes, File.ReadAllBytes(path));
        card.Title = "Série rééditée";
        expected.InsertBefore(expected.CreateXmlDeclaration("1.0", "utf-8", null), expected.FirstChild);
        LegacyXmlReference.Set(expected, "//info/title", card.Title);
        await repository.SaveAsync(card);
        LegacyXmlReference.AssertFile(expected, path);
    }

    [Fact]
    public void Save_RaccourciEtSource_ConserveOrdreAttributsEtCaracteres()
    {
        using ExportFixture fixture = new();
        const string Source = "Z:\\Été & Films";
        const string Dune = "smb://nas/L'été & \"cinéma\"";
        XmlDocument folders = LegacyXmlReference.Document("<folders><folder><path></path><type>0</type><dunepath></dunepath></folder></folders>");
        LegacyXmlReference.Set(folders, "//path", Source);
        LegacyXmlReference.Set(folders, "//dunepath", Dune);
        XmlDocument shortcuts = LegacyXmlReference.Document("<shortcuts><shortcut type='2' path=''>L'été &amp; cinéma</shortcut></shortcuts>");
        shortcuts.SelectSingleNode("//shortcut")!.Attributes!["path"]!.Value = Dune;
        DuneConfigurationStore store = fixture.ConfigurationStore();
        store.Save(fixture.Root, new(new(), [new(Source, 0, Dune)], XDocument.Parse(LegacyXmlReference.Declaration + LegacyXmlReference.Groups), [new("L'été & cinéma", 2, Dune)]));
        LegacyXmlReference.AssertFile(folders, Path.Combine(fixture.Root, "folders.xml"));
        LegacyXmlReference.AssertFile(shortcuts, Path.Combine(fixture.Root, "shortcuts.xml"));
        ConfigurationSnapshot snapshot = store.Load(fixture.Root);
        Assert.Equal(Source, snapshot.Folders.Single().Path);
        Assert.Equal(Dune, snapshot.Shortcuts.Single().Path);
        byte[] first = File.ReadAllBytes(Path.Combine(fixture.Root, "folders.xml"));
        store.Save(fixture.Root, snapshot);
        Assert.Equal(first, File.ReadAllBytes(Path.Combine(fixture.Root, "folders.xml")));
    }
}
