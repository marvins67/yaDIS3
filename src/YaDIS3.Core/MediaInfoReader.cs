using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace YaDIS3.Core;

/// <summary>Appelle MediaInfo sans dépendance native liée à l'application.</summary>
public sealed partial class MediaInfoReader(
    string executable = "mediainfo",
    ILogger? logger = null
) : IVideoInformationReader
{
    /// <inheritdoc />
    public async Task<XElement> ReadAsync(
        string file,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await ReadCoreAsync(file, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException or XmlException)
        {
            AnalysisFailed(logger ?? NullLogger<MediaInfoReader>.Instance, exception);
            return LegacyMediaInfo.Empty();
        }
    }


    private async Task<XElement> ReadCoreAsync(
        string file,
        CancellationToken cancellationToken
    )
    {
        ProcessStartInfo start = new(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("--Output=XML");
        start.ArgumentList.Add(Path.GetFullPath(file));
        using Process process = Start(start);
        try
        {
            Task<string> output =
                process.StandardOutput.ReadToEndAsync(
                    cancellationToken
                );
            Task<string> error =
                process.StandardError.ReadToEndAsync(
                    cancellationToken
                );
            await process.WaitForExitAsync(cancellationToken);
            string xml = await output;
            string diagnostic = await error;
            if (
                process.ExitCode != 0
                || string.IsNullOrWhiteSpace(xml)
            )
            {
                throw new InvalidDataException(
                    "Échec MediaInfo : " + diagnostic
                );
            }
            return LegacyMediaInfo.Convert(XDocument.Parse(xml));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    private static Process Start(ProcessStartInfo start)
    {
        try
        {
            return Process.Start(start)
                ?? throw new InvalidOperationException(
                    "Impossible de lancer MediaInfo."
                );
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                "Installez la version en ligne de commande de MediaInfo et ajoutez-la au PATH.",
                exception
            );
        }
    }

    [LoggerMessage(6, LogLevel.Warning, "Analyse MediaInfo impossible")]
    private static partial void AnalysisFailed(
        ILogger logger,
        Exception exception
    );
}
