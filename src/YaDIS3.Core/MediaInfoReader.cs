using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>Appelle MediaInfo sans dépendance native liée à l'application.</summary>
public sealed class MediaInfoReader(
    string executable = "mediainfo"
) : IVideoInformationReader
{
    /// <inheritdoc />
    public async Task<XElement> ReadAsync(
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
            return ConvertTracks(XDocument.Parse(xml));
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

    private static XElement ConvertTracks(XDocument document)
    {
        XElement[] tracks = document
            .Descendants()
            .Where(node => node.Name.LocalName == "track")
            .ToArray();
        XElement? general = Track(tracks, "General");
        XElement? video = Track(tracks, "Video");
        XElement? audio = Track(tracks, "Audio");
        if (video is null)
        {
            throw new InvalidDataException(
                "MediaInfo ne signale aucune piste vidéo."
            );
        }
        string duration = Value(general, "Duration");
        double.TryParse(
            duration,
            CultureInfo.InvariantCulture,
            out double seconds
        );
        return new XElement(
            "mediainfo",
            new XElement("format", Value(general, "Format")),
            new XElement("duration", Math.Round(seconds * 1000)),
            new XElement(
                "video",
                new XElement("codec", Value(video, "Format")),
                new XElement(
                    "resolution",
                    Value(video, "Height")
                        + (
                            Value(video, "ScanType")
                            == "Interlaced"
                                ? "i"
                                : "p"
                        )
                ),
                new XElement(
                    "aspectratio",
                    Value(video, "DisplayAspectRatio")
                ),
                new XElement(
                    "framerate",
                    Value(video, "FrameRate")
                )
            ),
            new XElement(
                "audio",
                new XElement("codec", Value(audio, "Format")),
                new XElement(
                    "channels",
                    Value(audio, "Channels")
                ),
                Languages(tracks, "Audio")
            ),
            new XElement("subtitles", Languages(tracks, "Text"))
        );
    }

    private static XElement? Track(
        IEnumerable<XElement> tracks,
        string type
    ) =>
        tracks.FirstOrDefault(node =>
            (string?)node.Attribute("type") == type
        );

    private static string Value(XElement? track, string name) =>
        track
            ?.Elements()
            .FirstOrDefault(node => node.Name.LocalName == name)
            ?.Value
        ?? "";

    private static XElement Languages(
        IEnumerable<XElement> tracks,
        string type
    ) =>
        new(
            "languages",
            string.Join(
                "|",
                tracks
                    .Where(node =>
                        (string?)node.Attribute("type") == type
                    )
                    .Select(node => Value(node, "Language"))
                    .Where(value => value.Length > 0)
                    .Distinct(StringComparer.Ordinal)
            )
        );
}
