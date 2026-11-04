using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace YaDIS3.Core;

/// <summary>
/// Porte MainModule.GetDunePath sans normaliser les noms ni
/// modifier l'ordre des adresses DNS et des motifs UUID.
/// </summary>
public sealed partial class LegacyDunePathResolver(
    IDunePathEnvironment environment,
    ILogger? logger = null,
    LegacyLanguageCatalog? texts = null
)
{
    private readonly IDunePathEnvironment _environment =
        environment
        ?? throw new ArgumentNullException(nameof(environment));

    private readonly ILogger _logger =
        logger ?? NullLogger.Instance;

    private readonly LegacyLanguageCatalog _texts =
        texts ?? LegacyLanguageCatalog.Load();

    /// <summary>Déduit le chemin de lecture selon yaDIS2.</summary>
    public async Task<string> ResolveAsync(
        string folder,
        string? unavailableLabel = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(folder);
        unavailableLabel ??= _texts.GetString("31");
        try
        {
            if (folder.StartsWith('/'))
            {
                return await ResolveNativeAsync(
                        folder,
                        unavailableLabel,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            string initialRelative = "";
            string result = folder;
            if (
                !folder.StartsWith(
                    @"\\",
                    StringComparison.Ordinal
                )
            )
            {
                string drive = folder[..2];
                string relative = folder[3..];
                initialRelative = relative;
                string network = _environment.NetworkRoot(drive);
                if (network.Length == 0)
                {
                    return Slashes(
                        "storage_label://"
                            + _environment.VolumeLabel(drive)
                            + (
                                relative.Length > 0
                                    ? "/" + relative
                                    : ""
                            )
                    );
                }
                result = network + "\\" + relative;
            }
            return await ResolveNetworkAsync(
                    result,
                    unavailableLabel,
                    initialRelative,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or IOException
                        or UnauthorizedAccessException
                        or SocketException
                        or IndexOutOfRangeException
                        or NotSupportedException
            )
        {
            // Le programme original rend le libellé 31 en cas d'échec.
            ResolveFailed(_logger, folder, exception);
            return unavailableLabel;
        }
    }

    private async Task<string> ResolveNativeAsync(
        string folder,
        string unavailableLabel,
        CancellationToken cancellationToken
    )
    {
        NativeFolderLocation location = await environment
            .NativeFolderAsync(folder, cancellationToken)
            .ConfigureAwait(false);
        if (location.NetworkPath.Length > 0)
        {
            return await ResolveNetworkAsync(
                    location.NetworkPath,
                    unavailableLabel,
                    "",
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        return Slashes(
            "storage_label://"
                + location.VolumeLabel
                + (
                    location.RelativePath.Length > 0
                        ? "/" + location.RelativePath
                        : ""
                )
        );
    }

    private async Task<string> ResolveNetworkAsync(
        string folder,
        string unavailableLabel,
        string initialRelative,
        CancellationToken cancellationToken
    )
    {
        // GetDunePath examine exactement les quatre premières lettres.
        if (
            folder
                .Substring(2, 4)
                .Equals(
                    "DUNE",
                    StringComparison.OrdinalIgnoreCase
                )
        )
        {
            return DuneDiskPath(
                folder,
                unavailableLabel,
                initialRelative
            );
        }
        string remaining = folder[2..];
        int separator = remaining.IndexOf('\\');
        string host = remaining[..separator];
        remaining = remaining[(separator + 1)..];
        separator = remaining.IndexOf('\\');
        string share =
            separator < 0 ? remaining : remaining[..separator];
        string relative =
            separator < 0 ? "" : remaining[(separator + 1)..];
        string address = await _environment
            .FirstAddressAsync(host, cancellationToken)
            .ConfigureAwait(false);
        return Slashes(
            "smb://"
                + address
                + "/"
                + share
                + (relative.Length > 0 ? "/" + relative : "")
        );
    }

    private static string DuneDiskPath(
        string folder,
        string unavailableLabel,
        string initialRelative
    )
    {
        string lower = folder.ToLowerInvariant();
        Match match = LongUuid().Match(lower);
        if (!match.Success)
        {
            match = NtfsUuid().Match(lower);
        }
        if (!match.Success)
        {
            match = FatUuid().Match(lower);
        }
        if (!match.Success)
        {
            return Slashes(
                unavailableLabel
                    + (
                        initialRelative.Length > 0
                            ? "/" + initialRelative
                            : ""
                    )
            );
        }
        // L'original recherche le UUID minuscule dans le chemin original :
        // préserver aussi ce résultat pour un partage écrit en majuscules.
        int start =
            folder.IndexOf(
                match.Value,
                StringComparison.CurrentCulture
            )
            + match.Length
            + 1;
        string relative = initialRelative;
        try
        {
            relative = folder[start..];
        }
        catch (ArgumentOutOfRangeException)
        {
            // À la racine, le séparateur final peut être absent.
        }
        return Slashes(
            "storage_uuid://"
                + match.Value
                + (relative.Length > 0 ? "/" + relative : "")
        );
    }

    private static string Slashes(string value) =>
        value.Replace('\\', '/');

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Déduction du chemin Dune impossible pour {Folder}."
    )]
    private static partial void ResolveFailed(
        ILogger logger,
        string folder,
        Exception exception
    );

    [GeneratedRegex(
        "[0-9a-f]{8}_[0-9a-f]{4}_[0-9a-f]{4}_[0-9a-f]{4}_[0-9a-f]{12}"
    )]
    private static partial Regex LongUuid();

    [GeneratedRegex("[0-9a-f]{16}")]
    private static partial Regex NtfsUuid();

    [GeneratedRegex("[0-9a-f]{4}_[0-9a-f]{4}")]
    private static partial Regex FatUuid();
}
