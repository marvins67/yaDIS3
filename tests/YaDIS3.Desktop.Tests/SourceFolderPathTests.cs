using Xunit;
using YaDIS3.Desktop.ViewModels;

namespace YaDIS3.Desktop.Tests;

public sealed class SourceFolderPathTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChooseFolderAsync_PickerSelection_PreservesEnteredDunePathEvenInBasicMode(
        bool basicMode
    )
    {
        SourceFolderViewModel draft = new(
            new(@"D:\Films", 1, "nfs://ancien:/Films"),
            new(new FolderPathEnvironment())
        )
        {
            BasicMode = basicMode,
        };
        Assert.Equal("nfs://ancien:/Films", draft.DunePath);
        await draft.ChooseFolderAsync(@"Z:\Séries TV");
        Assert.Equal(@"Z:\Séries TV", draft.Path);
        Assert.Equal(
            "nfs://ancien:/Films",
            draft.DunePath
        );
        Assert.True(draft.CanAccept);
        Assert.Equal(1, draft.Type);
        draft.DunePath = "storage_uuid://1234_abcd/Séries";
        Assert.Equal(draft.DunePath, draft.Snapshot().DunePath);
    }

    [Fact]
    public void DunePath_EmptyComboBoxText_DisablesAccept()
    {
        SourceFolderViewModel draft = new(
            new(@"Z:\Films", 0, "smb://nas/Films")
        );
        draft.DunePath = null!;
        Assert.Equal("", draft.DunePath);
        Assert.False(draft.CanAccept);
    }

    [Fact]
    public async Task ChooseFolderAsync_DnsPending_PreventsAcceptUntilResolutionEnds()
    {
        TaskCompletionSource<string> address = new();
        SourceFolderViewModel draft = new(
            new("", 0, ""),
            new(
                new FolderPathEnvironment() { Address = address }
            )
        );
        Task choosing = draft.ChooseFolderAsync(@"Z:\Films");
        Assert.True(draft.IsResolving);
        Assert.False(draft.CanAccept);
        address.SetResult("192.168.0.20");
        await choosing;
        Assert.False(draft.IsResolving);
        Assert.True(draft.CanAccept);
        Assert.Equal(
            "smb://192.168.0.20/Vidéos/Films",
            draft.DunePath
        );
    }
}
