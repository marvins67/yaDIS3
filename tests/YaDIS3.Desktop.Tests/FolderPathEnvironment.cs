using YaDIS3.Core;

namespace YaDIS3.Desktop.Tests;

internal sealed class FolderPathEnvironment
    : IDunePathEnvironment
{
    public TaskCompletionSource<string>? Address { get; init; }

    public string Network { get; init; } = @"\\NAS\Vidéos";

    public string NetworkRoot(string drive) => Network;

    public string VolumeLabel(string drive) => "MEDIA";

    public Task<NativeFolderLocation> NativeFolderAsync(
        string folder,
        CancellationToken cancellationToken
    ) =>
        Task.FromResult(
            new NativeFolderLocation("", "MEDIA", "Films")
        );

    public Task<string> FirstAddressAsync(
        string host,
        CancellationToken cancellationToken
    ) => Address?.Task ?? Task.FromResult("192.168.1.10");
}
