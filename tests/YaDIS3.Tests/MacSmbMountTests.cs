using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class MacSmbMountTests
{
    private const string Mounts =
        "//greg@NAS/Vid%C3%A9os on /Volumes/Vidéos (smbfs, nodev, nosuid)\n"
        + "//greg@DUNE/DuneHDD_26b6a642067197ba on /Volumes/Dune (smbfs, nodev)\n";

    [Theory]
    [InlineData(
        "/Volumes/Vidéos/Séries TV",
        @"\\nas\Vidéos\Séries TV"
    )]
    [InlineData(
        "/Volumes/Dune/Films",
        @"\\dune\DuneHDD_26b6a642067197ba\Films"
    )]
    [InlineData("/Volumes/Vidéos", @"\\nas\Vidéos")]
    [InlineData("/Volumes/Vidéos-1/Films", "")]
    [InlineData("/Users/greg/Films", "")]
    public void NetworkPath_FinderSmbMount_ProducesEquivalentUnc(
        string folder,
        string expected
    )
    {
        Assert.Equal(
            expected,
            MacSmbMounts.NetworkPath(folder, Mounts)
        );
    }
}
