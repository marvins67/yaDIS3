using System.Xml;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class LegacyXmlCorpusTests
{
    [Fact]
    public void Scan_QuatreSourcesReelles_RegenereLes511XmlOctetPourOctet()
    {
        using LegacyXmlCorpus corpus = new();
        using WorkspaceRepository repository = new();
        repository.Create(corpus.Root);
        LegacyXmlSourceFileSystem files = new(corpus);
        DuneConfigurationStore store = new(files, corpus.UserConfig);
        store.Save(corpus.Root, store.Load(corpus.Root) with { Folders = corpus.Folders() });
        new SourceFolderScanner(files).Scan(corpus.Root, corpus.Folders(), false);
        string[] different = corpus.Documents.Where(pair => !pair.Value.SequenceEqual(corpus.Actual(pair.Key))).Select(pair => pair.Key).ToArray();
        Assert.True(different.Length == 0, $"Identiques : {corpus.Documents.Count - different.Length}/511. Premiers écarts : {string.Join(", ", different.Take(5))}");
        Assert.Equal(511, corpus.Documents.Count);
        foreach ((string path, byte[] expected) in corpus.Documents)
        {
            Assert.Equal(expected, corpus.Actual(path));
            XmlDocument legacy = new();
            using MemoryStream stream = new(corpus.Actual(path));
            legacy.Load(stream);
            Assert.NotNull(legacy.DocumentElement);
        }
        Dictionary<string, DateTime> timestamps = corpus.Documents.Keys.ToDictionary(path => path,
            path => File.GetLastWriteTimeUtc(Path.Combine(corpus.Root, path.Replace('/', Path.DirectorySeparatorChar))), StringComparer.Ordinal);
        new SourceFolderScanner(files).Scan(corpus.Root, corpus.Folders(), false);
        foreach ((string path, DateTime timestamp) in timestamps)
        {
            Assert.Equal(timestamp, File.GetLastWriteTimeUtc(Path.Combine(corpus.Root, path.Replace('/', Path.DirectorySeparatorChar))));
            Assert.Equal(corpus.Documents[path], corpus.Actual(path));
        }
        Workspace workspace = repository.Open(corpus.Root);
        Assert.Empty(workspace.Warnings);
        Assert.Equal(473, workspace.Cards.Count(card => !card.IsSeries));
        Assert.Equal(35, workspace.Cards.Count(card => card.IsSeries));
        foreach (MediaCard card in workspace.Cards)
        {
            card.Dispose();
        }
    }

    [Fact]
    public async Task SaveAsync_FichesYaDis2SansModification_ConserveLes508Fichiers()
    {
        using LegacyXmlCorpus corpus = new();
        foreach ((string path, byte[] bytes) in corpus.Documents)
        {
            string file = Path.Combine(corpus.Root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file, bytes);
        }
        using WorkspaceRepository repository = new();
        Workspace workspace = repository.Open(corpus.Root);
        Assert.Empty(workspace.Warnings);
        foreach (MediaCard card in workspace.Cards)
        {
            using (card)
            {
                string relative = Path.GetRelativePath(corpus.Root, card.FilePath).Replace(Path.DirectorySeparatorChar, '/');
                await repository.SaveAsync(card);
                Assert.Equal(corpus.Documents[relative], corpus.Actual(relative));
            }
        }
        Assert.Equal(508, workspace.Cards.Count);
    }

    [Fact]
    public async Task SaveAsync_DateVideIndentée_AppliqueLaDateDuTraitement()
    {
        using LegacyXmlCorpus corpus = new();
        var reference = corpus.Documents.First(pair => pair.Key.EndsWith("/data.xml", StringComparison.Ordinal));
        string path = Path.Combine(corpus.Root, reference.Key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, reference.Value);
        using WorkspaceRepository repository = new();
        using MediaCard card = Assert.Single(repository.Open(corpus.Root).Cards);
        Assert.Equal("", card.Snapshot().Document.Descendants("date").Single().Value);
        card.MarkProcessed(new DateOnly(2026, 11, 13));
        await repository.SaveAsync(card);
        XmlDocument legacy = new();
        legacy.Load(path);
        Assert.Equal("2026-11-13", legacy.SelectSingleNode("//item/date")!.InnerText);
    }

    [Fact]
    public void Create_WorkspaceVide_ReprendLesLitterauxHistoriques()
    {
        using LegacyXmlCorpus corpus = new();
        using WorkspaceRepository repository = new();
        repository.Create(corpus.Root);
        LegacyXmlReference.AssertFile(LegacyXmlReference.Document("<folders></folders>"), Path.Combine(corpus.Root, "folders.xml"));
        Assert.Equal(corpus.Documents["groups.xml"], corpus.Actual("groups.xml"));
        Assert.False(File.Exists(Path.Combine(corpus.Root, "shortcuts.xml")));
        byte[] first = corpus.Actual("groups.xml");
        repository.Create(corpus.Root);
        Assert.Equal(first, corpus.Actual("groups.xml"));
    }
}
