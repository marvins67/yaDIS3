using System.Net.Sockets;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class LegacyDunePathTests
{
    // Résultats figés de MainModule.GetDunePath de yaDIS2 :
    // conserver les espaces, les accents et les anomalies historiques.
    [Theory]
    [InlineData(@"\\NAS\Films", "smb://192.168.1.10/Films")]
    [InlineData(@"\\NAS\Films\", "smb://192.168.1.10/Films")]
    [InlineData(
        @"\\NAS\Films\L'été & l'hiver (2026)",
        "smb://192.168.1.10/Films/L'été & l'hiver (2026)"
    )]
    [InlineData(
        @"\\192.168.1.10\Films\Séries TV\",
        "smb://192.168.1.10/Films/Séries TV/"
    )]
    public async Task ResolveAsync_Unc_UsesFirstDnsAddress(
        string folder,
        string expected
    )
    {
        DunePathEnvironment environment = new();
        Assert.Equal(
            expected,
            await new LegacyDunePathResolver(
                environment
            ).ResolveAsync(folder)
        );
        Assert.NotNull(environment.Host);
    }

    [Theory]
    [InlineData(@"D:\", "storage_label://FILMS")]
    [InlineData(
        @"D:\Films\Wall·E.iso",
        "storage_label://FILMS/Films/Wall·E.iso"
    )]
    [InlineData(@"D:\Séries\", "storage_label://FILMS/Séries/")]
    public async Task ResolveAsync_LocalDrive_UsesVolumeLabel(
        string folder,
        string expected
    )
    {
        DunePathEnvironment environment = new();
        Assert.Equal(
            expected,
            await new LegacyDunePathResolver(
                environment
            ).ResolveAsync(folder)
        );
        Assert.Null(environment.Host);
    }

    [Theory]
    [InlineData(
        @"\\DUNE\DuneHDD_26b6a642067197ba\Films",
        "storage_uuid://26b6a642067197ba/Films"
    )]
    [InlineData(
        @"\\dune-hd\USB_1234_abcd\Séries TV",
        "storage_uuid://1234_abcd/Séries TV"
    )]
    [InlineData(
        @"\\DUNE\Disk_12345678_1234_abcd_5678_123456789abc\Films",
        "storage_uuid://12345678_1234_abcd_5678_123456789abc/Films"
    )]
    [InlineData(
        @"\\DUNE\Disk_26b6a642067197ba",
        "storage_uuid://26b6a642067197ba"
    )]
    [InlineData(@"\\DUNE\Disque\Films", "<label not found>")]
    [InlineData(
        @"\\DUNE\DuneHDD_26B6A642067197BA\Films",
        "storage_uuid://26b6a642067197ba/6B6A642067197BA/Films"
    )]
    [InlineData(
        @"\\DUNE\Disk_1234_abcd\folder_26b6a642067197ba\Films",
        "storage_uuid://26b6a642067197ba/Films"
    )]
    [InlineData(
        @"\\DUNE\Disk_26b6a642067197ba\12345678_1234_abcd_5678_123456789abc\Films",
        "storage_uuid://12345678_1234_abcd_5678_123456789abc/Films"
    )]
    public async Task ResolveAsync_DuneShare_PreservesUuidRules(
        string folder,
        string expected
    )
    {
        DunePathEnvironment environment = new();
        Assert.Equal(
            expected,
            await new LegacyDunePathResolver(
                environment
            ).ResolveAsync(folder)
        );
        Assert.Null(environment.Host);
    }

    [Theory]
    [InlineData(
        @"\\NAS\Vidéos",
        "smb://192.168.1.10/Vidéos/Séries"
    )]
    [InlineData(
        @"\\DUNE\DuneHDD_26b6a642067197ba",
        "storage_uuid://26b6a642067197ba/Séries"
    )]
    [InlineData(@"\\DUNE\Disque", "<label not found>/Séries")]
    public async Task ResolveAsync_MappedDrive_ResolvesBeforeConverting(
        string network,
        string expected
    )
    {
        DunePathEnvironment environment = new()
        {
            Network = network,
        };
        Assert.Equal(
            expected,
            await new LegacyDunePathResolver(
                environment
            ).ResolveAsync(@"Z:\Séries")
        );
    }

    [Fact]
    public async Task ResolveAsync_Ipv6_DoesNotSubstituteAnIpv4Address()
    {
        DunePathEnvironment environment = new()
        {
            Address = "fe80::1234",
        };
        Assert.Equal(
            "smb://fe80::1234/Films",
            await new LegacyDunePathResolver(
                environment
            ).ResolveAsync(@"\\NAS\Films")
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("Z:")]
    [InlineData(@"\\NAS")]
    public async Task ResolveAsync_InvalidPath_ReturnsOriginalFailureLabel(
        string folder
    )
    {
        Assert.Equal(
            "À saisir",
            await new LegacyDunePathResolver(
                new DunePathEnvironment()
            ).ResolveAsync(folder, "À saisir")
        );
    }

    [Fact]
    public async Task ResolveAsync_DnsFailure_ReturnsOriginalFailureLabel()
    {
        DunePathEnvironment environment = new()
        {
            Failure = new SocketException(),
        };
        Assert.Equal(
            "À saisir",
            await new LegacyDunePathResolver(
                environment
            ).ResolveAsync(@"\\Absent\Films", "À saisir")
        );
    }

    [Fact]
    public async Task ResolveAsync_Cancelled_DoesNotHideCancellation()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () =>
                new LegacyDunePathResolver(
                    new DunePathEnvironment()
                ).ResolveAsync(
                    @"\\NAS\Films",
                    cancellationToken: cancellation.Token
                )
        );
    }

    [Fact]
    public async Task ResolveAsync_NativeSmbMount_UsesTheSameUncConversion()
    {
        DunePathEnvironment environment = new()
        {
            Native = new(@"\\NAS\Vidéos\Séries", "", ""),
        };
        Assert.Equal(
            "smb://192.168.1.10/Vidéos/Séries",
            await new LegacyDunePathResolver(
                environment
            ).ResolveAsync("/Volumes/Vidéos/Séries")
        );
    }

    [Fact]
    public async Task ResolveAsync_NativeLocalVolume_UsesItsLabelAndRelativeFolder()
    {
        DunePathEnvironment environment = new()
        {
            Native = new(
                "",
                "Dune USB",
                "Films/L'été & l'hiver"
            ),
        };
        Assert.Equal(
            "storage_label://Dune USB/Films/L'été & l'hiver",
            await new LegacyDunePathResolver(
                environment
            ).ResolveAsync(
                "/Volumes/Dune USB/Films/L'été & l'hiver"
            )
        );
    }
}
