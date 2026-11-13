using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;

namespace YaDIS3.Desktop.Tests;

public sealed class ManualDunePathTests
{
    [Theory]
    [InlineData(@"Z:\Séries")]
    [InlineData(@"Z:\Autres séries")]
    public async Task ChooseFolderAsync_CheminLocalEnregistre_ConserveLaConfiguration(string selected)
    {
        TaskCompletionSource<string> address = new();
        SourceFolderViewModel draft = Draft("/Séries", address);
        Task choosing = draft.ChooseFolderAsync(selected);
        Assert.True(choosing.IsCompletedSuccessfully);
        await choosing;
        Assert.Equal(selected, draft.Path);
        Assert.Equal("/Séries", draft.DunePath);
        Assert.Equal("/Séries", draft.Snapshot().DunePath);
    }

    [Fact]
    public async Task ChooseFolderAsync_CheminSaisiAvantSelection_ConserveLaSaisie()
    {
        SourceFolderViewModel draft = Draft("");
        draft.DunePath = "/Séries";
        await draft.ChooseFolderAsync(@"Z:\Séries");
        Assert.Equal("/Séries", draft.DunePath);
        Assert.True(draft.CanAccept);
    }

    [Fact]
    public async Task ChooseFolderAsync_PropositionNonModifiee_SuitLeDossierSuivant()
    {
        SourceFolderViewModel draft = Draft("");
        await draft.ChooseFolderAsync(@"Z:\Films");
        Assert.Equal("smb://192.168.1.10/DuneHDD_26b6a642067197ba/Films", draft.DunePath);
        await draft.ChooseFolderAsync(@"Z:\Séries");
        Assert.Equal("smb://192.168.1.10/DuneHDD_26b6a642067197ba/Séries", draft.DunePath);
    }

    [Fact]
    public async Task ChooseFolderAsync_SaisiePendantDns_NeRemplacePasLaSaisie()
    {
        TaskCompletionSource<string> address = new();
        SourceFolderViewModel draft = Draft("", address);
        Task choosing = draft.ChooseFolderAsync(@"Z:\Séries");
        Assert.True(draft.IsResolving);
        draft.DunePath = "/Séries";
        address.SetResult("192.168.1.10");
        await choosing;
        Assert.Equal("/Séries", draft.DunePath);
        Assert.True(draft.CanAccept);
    }

    [Fact]
    public async Task ChooseFolderAsync_CheminEfface_ReactiveLaProposition()
    {
        SourceFolderViewModel draft = Draft("/Séries");
        draft.DunePath = "";
        await draft.ChooseFolderAsync(@"Z:\Séries");
        Assert.Equal("smb://192.168.1.10/DuneHDD_26b6a642067197ba/Séries", draft.DunePath);
    }

    [Fact]
    public async Task Snapshot_CheminManuel_EnregistreLaMemeValeurDansLesXml()
    {
        string root = Path.Combine(Path.GetTempPath(), "yadis-manual-path-" + Guid.NewGuid().ToString("N"));
        try
        {
            string source = Path.Combine(root, "source");
            Directory.CreateDirectory(Path.Combine(source, "Une série"));
            SourceFolderViewModel draft = Draft("");
            draft.DunePath = "/Séries";
            await draft.ChooseFolderAsync(source);
            MediaSourceFolder[] folders = [draft.Snapshot()];
            DuneConfigurationStore store = new(userConfigurationPath: Path.Combine(root, "user.config"));
            store.Save(root, store.Load(root) with { Folders = folders });
            new SourceFolderScanner().Scan(root, folders, false);
            Assert.Equal("/Séries", XDocument.Load(Path.Combine(root, "folders.xml")).Descendants("dunepath").Single().Value);
            Assert.Equal("/Séries", XDocument.Load(Path.Combine(root, "data", "Une série", "data.xml")).Descendants("dunepath").Single().Value);
            Assert.Equal("/Séries", store.Load(root).Folders.Single().DunePath);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static SourceFolderViewModel Draft(string dunePath, TaskCompletionSource<string>? address = null) => new(
        new(@"Z:\Séries", 1, dunePath),
        new(new FolderPathEnvironment
        {
            Network = @"\\192.168.1.10\DuneHDD_26b6a642067197ba",
            Address = address,
        }));
}
