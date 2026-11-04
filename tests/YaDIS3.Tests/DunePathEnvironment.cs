using YaDIS3.Core;

namespace YaDIS3.Tests;

internal sealed class DunePathEnvironment : IDunePathEnvironment
{
    public string Network { get; init; } = "";
    public string Label { get; init; } = "FILMS";
    public string Address { get; init; } = "192.168.1.10";
    public string? Host { get; private set; }
    public Exception? Failure { get; init; }
    public NativeFolderLocation Native { get; init; } =
        new("", "Films", "");

    public string NetworkRoot(string drive) => Network;

    public string VolumeLabel(string drive) => Label;

    public Task<NativeFolderLocation> NativeFolderAsync(
        string folder,
        CancellationToken cancellationToken
    ) => Task.FromResult(Native);

    public Task<string> FirstAddressAsync(
        string host,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        Host = host;
        return Failure is null
            ? Task.FromResult(Address)
            : Task.FromException<string>(Failure);
    }
}
