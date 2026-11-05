using System.Globalization;
using System.Text;
using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

/// <summary>
/// Écrit les fiches sans modifier les fichiers vidéo sources.
/// </summary>
internal sealed class LegacyDuneMediaWriter(
    DuneTemplate template,
    RenderOptions options,
    IExportFileSystem fileSystem
)
{
    private readonly LegacyDuneDefinitions _definitions = new(
        template
    );

    public void Write(
        MediaContext media,
        string directory,
        IReadOnlyList<string> movieUrls,
        IReadOnlyDictionary<XElement, string> episodes
    )
    {
        if (!media.IsSeries)
        {
            WriteMovie(media, directory, movieUrls);
            return;
        }

        Save(directory, _definitions.Series());
        foreach (XElement episode in media.Episodes)
        {
            string key = MediaContext.EpisodeFolder(episode);
            string season = Path.Combine(
                directory,
                LegacyExportPaths.Season(key)
            );
            Save(season, _definitions.Season());
            Save(
                Path.Combine(season, key),
                LegacyDuneDefinitions.Episode(episodes[episode])
            );
        }
    }

    private void WriteMovie(
        MediaContext media,
        string directory,
        IReadOnlyList<string> urls
    )
    {
        string url = urls[0];
        if (urls.Count > 1)
        {
            string source = Path.Combine(
                media.Header.ValueAt("path"),
                media.Header.ValueAt("filename")
            );
            string[] files = urls.Select(value =>
                    value[(value.LastIndexOf('/') + 1)..]
                )
                .ToArray();
            fileSystem.WriteBytes(
                Path.Combine(source, "playlist.m3u"),
                LegacyPlaylistEncoding().GetBytes(
                    string.Join("\r\n", files) + "\r\n"
                )
            );
            url =
                urls[0][..(urls[0].LastIndexOf('/') + 1)]
                + "playlist.m3u";
        }

        Save(directory, _definitions.Movie(url));
    }

    private static Encoding LegacyPlaylistEncoding()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Encoding.UTF8;
        }

        // StreamWriter(..., Encoding.Default) de yaDIS2 utilise
        // la page de code ANSI locale de Windows.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            CultureInfo.CurrentCulture.TextInfo.ANSICodePage
        );
    }

    private void Save(string directory, string content)
    {
        fileSystem.CreateDirectory(directory);
        fileSystem.WriteBytes(
            Path.Combine(directory, "dune_folder.txt"),
            LegacyDuneEncoding.Encode(
                content,
                options.AaiBackgrounds
            )
        );
    }
}
