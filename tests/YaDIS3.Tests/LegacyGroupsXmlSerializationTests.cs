using System.Xml;
using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class LegacyGroupsXmlSerializationTests
{
    [Fact]
    public void SaveGroups_CycleSaga_ReproduitCreationModificationSuppression()
    {
        using ExportFixture fixture = new();
        DuneConfigurationStore store = fixture.ConfigurationStore();
        XmlDocument expected = LegacyXmlReference.Document(LegacyXmlReference.Groups);
        XDocument groups = XDocument.Parse(expected.OuterXml);
        LegacyMovieGroups.CreateSaga(groups, "Été famille", "a.mkv");
        XmlElement saga = Append(expected, "//sagas", "saga");
        Append(expected, "//saga", "name", "Été famille");
        Append(expected, "//saga", "sort_name", "Été famille");
        Append(expected, "//saga", "poster", "a.mkv");
        Append(expected, "//saga", "fanart", "a.mkv");
        Append(expected, "//saga", "movie", "a.mkv");
        SaveAndAssert(store, fixture.Root, groups, expected);
        LegacyMovieGroups.EditSaga(groups, "Été famille", "Après", "Tri", "b.mkv", "a.mkv", ["b.mkv", "a.mkv"]);
        LegacyXmlReference.Set(expected, "//saga/name", "Après");
        LegacyXmlReference.Set(expected, "//saga/sort_name", "Tri");
        LegacyXmlReference.Set(expected, "//saga/poster", "b.mkv");
        saga.RemoveChild(expected.SelectSingleNode("//saga/movie")!);
        Append(expected, "//saga", "movie", "b.mkv");
        Append(expected, "//saga", "movie", "a.mkv");
        SaveAndAssert(store, fixture.Root, groups, expected);
        LegacyMovieGroups.DeleteSaga(groups, "Après");
        saga.ParentNode!.RemoveChild(saga);
        SaveAndAssert(store, fixture.Root, groups, expected);
    }

    [Fact]
    public void SaveGroups_CycleSelection_ReproduitCreationModificationSuppression()
    {
        using ExportFixture fixture = new();
        DuneConfigurationStore store = fixture.ConfigurationStore();
        XmlDocument expected = LegacyXmlReference.Document(LegacyXmlReference.Groups);
        XDocument groups = XDocument.Parse(expected.OuterXml);
        LegacyMovieGroups.CreateSelection(groups, "Été", ["a.mkv"]);
        XmlElement selection = Append(expected, "//selections", "selection");
        selection.SetAttribute("enabled", "yes");
        Append(expected, "//selection", "name", "Été");
        Append(expected, "//selection", "movie", "a.mkv");
        SaveAndAssert(store, fixture.Root, groups, expected);
        LegacyMovieGroups.ToggleSelection(groups, "Été", ["b.mkv"]);
        Append(expected, "//selection", "movie", "b.mkv");
        SaveAndAssert(store, fixture.Root, groups, expected);
        LegacyMovieGroups.DeleteSelection(groups, "Été");
        selection.ParentNode!.RemoveChild(selection);
        SaveAndAssert(store, fixture.Root, groups, expected);
    }

    [Fact]
    public void Add_SelectionAutomatique_ReproduitLaMemeSauvegarde()
    {
        using ExportFixture fixture = new();
        XmlDocument expected = LegacyXmlReference.Document("<groups><sagas></sagas><selections><selection enabled='yes'><name>Été</name><movie>a.mkv</movie></selection></selections></groups>");
        string path = Path.Combine(fixture.Root, "groups.xml");
        File.WriteAllBytes(path, LegacyXmlReference.Bytes(expected));
        new LegacyAutoSelection().Add(fixture.Root, "Été", "b.mkv");
        Append(expected, "//selection", "movie", "b.mkv");
        LegacyXmlReference.AssertFile(expected, path);
        byte[] first = File.ReadAllBytes(path);
        new LegacyAutoSelection().Add(fixture.Root, "Été", "b.mkv");
        Assert.Equal(first, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<?xml version='1.0' encoding='utf-8'?>")]
    [InlineData("<?xml version='1.0' encoding='utf-16'?>")]
    public void SaveGroups_DeclarationExistante_RespecteEncodageEtFormesVides(string declaration)
    {
        using ExportFixture fixture = new();
        const string Xml = "<groups extension='Été &amp; test'><sagas /><selections>\n</selections><extension xml:space='preserve'>  </extension></groups>";
        XmlDocument expected = new();
        expected.LoadXml(declaration + Xml);
        string path = Path.Combine(fixture.Root, "groups.xml");
        File.WriteAllBytes(path, LegacyXmlReference.Bytes(expected));
        DuneConfigurationStore store = fixture.ConfigurationStore();
        XDocument groups = store.Load(fixture.Root).Groups;
        store.SaveGroups(fixture.Root, groups);
        Assert.Equal(LegacyXmlReference.Bytes(expected), File.ReadAllBytes(path));
        XmlDocument reread = new();
        reread.Load(path);
        Assert.Equal(expected.OuterXml, reread.OuterXml);
    }

    private static XmlElement Append(XmlDocument document, string parent, string name, string? value = null)
    {
        XmlElement element = document.CreateElement(name);
        if (value is not null)
        {
            element.InnerText = value;
        }
        document.SelectSingleNode(parent)!.AppendChild(element);
        return element;
    }

    private static void SaveAndAssert(DuneConfigurationStore store, string workspace, XDocument groups, XmlDocument expected)
    {
        store.SaveGroups(workspace, groups);
        LegacyXmlReference.AssertFile(expected, Path.Combine(workspace, "groups.xml"));
    }
}
