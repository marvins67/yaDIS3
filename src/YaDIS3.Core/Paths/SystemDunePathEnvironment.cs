using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace YaDIS3.Core;

/// <summary>Adaptateur des lecteurs Windows employés par GetDunePath.</summary>
public sealed class SystemDunePathEnvironment
    : IDunePathEnvironment
{
    /// <inheritdoc />
    public string NetworkRoot(string drive) =>
        OperatingSystem.IsWindows()
            ? WindowsNetworkRoot(drive)
            : "";

    /// <inheritdoc />
    public string VolumeLabel(string drive)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Lecteur Windows requis."
            );
        }
        return new DriveInfo(drive).VolumeLabel;
    }

    /// <inheritdoc />
    public async Task<string> FirstAddressAsync(
        string host,
        CancellationToken cancellationToken
    )
    {
        IPHostEntry entry = await Dns.GetHostEntryAsync(
                host,
                cancellationToken
            )
            .ConfigureAwait(false);
        return entry.AddressList[0].ToString();
    }

    /// <inheritdoc />
    public async Task<NativeFolderLocation> NativeFolderAsync(
        string folder,
        CancellationToken cancellationToken
    )
    {
        if (OperatingSystem.IsMacOS())
        {
            string network = MacSmbMounts.NetworkPath(
                folder,
                await ReadMacMountsAsync(cancellationToken)
                    .ConfigureAwait(false)
            );
            if (network.Length > 0)
            {
                return new(network, "", "");
            }
        }
        DriveInfo drive = DriveInfo
            .GetDrives()
            .Where(item =>
                folder == item.Name.TrimEnd('/')
                || folder.StartsWith(
                    item.Name.TrimEnd('/') + "/",
                    StringComparison.Ordinal
                )
            )
            .OrderByDescending(item => item.Name.Length)
            .First();
        return new(
            "",
            drive.VolumeLabel,
            folder[drive.Name.TrimEnd('/').Length..]
                .TrimStart('/')
        );
    }

    private static async Task<string> ReadMacMountsAsync(
        CancellationToken cancellationToken
    )
    {
        using Process mount = new()
        {
            StartInfo = new("/sbin/mount")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        mount.Start();
        Task<string> output =
            mount.StandardOutput.ReadToEndAsync(
                cancellationToken
            );
        Task<string> error = mount.StandardError.ReadToEndAsync(
            cancellationToken
        );
        await mount
            .WaitForExitAsync(cancellationToken)
            .ConfigureAwait(false);
        string errors = await error.ConfigureAwait(false);
        if (mount.ExitCode != 0)
        {
            throw new IOException(errors);
        }
        return await output.ConfigureAwait(false);
    }

    [SupportedOSPlatform("windows")]
    private static string WindowsNetworkRoot(string drive)
    {
        int length = 256;
        char[] buffer = new char[length];
        int status = WNetGetConnection(
            drive,
            buffer,
            ref length
        );
        const int MoreData = 234;
        if (status == MoreData)
        {
            buffer = new char[length];
            status = WNetGetConnection(
                drive,
                buffer,
                ref length
            );
        }
        return status == 0
            ? new string(buffer).TrimEnd('\0')
            : "";
    }

    [DllImport(
        "mpr.dll",
        EntryPoint = "WNetGetConnectionW",
        CharSet = CharSet.Unicode
    )]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [SupportedOSPlatform("windows")]
    private static extern int WNetGetConnection(
        string localName,
        [Out] char[] remoteName,
        ref int length
    );
}
