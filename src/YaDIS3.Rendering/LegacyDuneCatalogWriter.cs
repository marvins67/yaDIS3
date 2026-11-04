using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

/// <summary>
/// Reproduit l'arborescence de navigation de ProcessForm.
/// </summary>
internal sealed partial class LegacyDuneCatalogWriter(
    DuneTemplate template,
    RenderOptions options,
    TimeProvider timeProvider,
    IExportFileSystem fileSystem,
    LegacyCatalogOptions catalog,
    LegacyLanguageCatalog? texts = null
)
{
    private readonly LegacyLanguageCatalog _texts =
        texts ?? LegacyLanguageCatalog.Load(options.Language);

    private readonly LegacyDuneDefinitions _definitions = new(
        template
    );
    private readonly LegacyCatalogOptions _catalog = catalog;
    private string Background =>
        options.AaiBackgrounds
            ? "background.aai"
            : "background.jpg";

    public void Write(
        TemplateRenderer renderer,
        IReadOnlyList<MediaContext> cards,
        string root,
        CancellationToken cancellationToken
    )
    {
        string workspace = WorkspaceDirectory(cards[0]);
        XElement groups = LoadXml(
            Path.Combine(workspace, "groups.xml")
        );
        XElement shortcuts = LoadXml(
            Path.Combine(workspace, "shortcuts.xml")
        );
        MediaContext[] movies = cards
            .Where(card =>
                !card.IsSeries
                && card.Header.ValueAt("status") != "3"
            )
            .ToArray();
        WriteMovies(
            renderer,
            movies,
            groups,
            root,
            workspace,
            cancellationToken
        );
        WriteSeries(renderer, cards, root);
        WriteSelections(renderer, movies, groups, root);
        WriteShortcuts(renderer, groups, shortcuts, root);
        WriteRoot(renderer, cards, groups, shortcuts, root);
    }

    private static string WorkspaceDirectory(MediaContext card)
    {
        return Path.GetDirectoryName(
            Path.GetDirectoryName(card.DataDirectory)
        )!;
    }

    private XElement LoadXml(string path)
    {
        if (!fileSystem.FileExists(path))
        {
            return new XElement("groups");
        }

        using StringReader input = new(
            fileSystem.ReadText(path)
        );
        using XmlReader reader = XmlReader.Create(
            input,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            }
        );
        return XElement.Load(reader);
    }

    private static string Output(string root, string relative)
    {
        return Path.Combine(
            root,
            relative.Replace('/', Path.DirectorySeparatorChar)
        );
    }

    private string Definition(string content)
    {
        return options.AaiBackgrounds
            ? content
            : content
                .Replace(
                    "background.aai",
                    "background.jpg",
                    StringComparison.Ordinal
                )
                .Replace(
                    "background2.aai",
                    "background2.jpg",
                    StringComparison.Ordinal
                );
    }

    private DuneFile Document(string content)
    {
        return DuneFile.FromLegacy(content, fileSystem);
    }

    private DuneFile MovieListing(string background)
    {
        return Document(Definition(_definitions.MovieListing()))
            .Set("background_path", background);
    }

    private void WriteSeries(
        TemplateRenderer renderer,
        IReadOnlyList<MediaContext> cards,
        string root
    )
    {
        if (!cards.Any(card => card.IsSeries))
        {
            return;
        }

        string directory = Output(root, "01");
        renderer.RenderMenu(directory, "tv");
        Document(Definition(_definitions.SeriesListing()))
            .Save(directory);
    }

    private void WriteMovies(
        TemplateRenderer renderer,
        MediaContext[] movies,
        XElement groups,
        string root,
        string workspace,
        CancellationToken cancellationToken
    )
    {
        string directory = Output(root, "00/00");
        renderer.RenderMenu(directory, "movie");
        WriteMovieListing(
            movies,
            groups,
            root,
            "00/00",
            Background,
            collapseSagas: true
        );
        if (Enabled(2))
        {
            WriteRecent(movies, groups, root);
        }
        WriteSagas(renderer, movies, groups, root);
        WriteIndexes(
            renderer,
            movies,
            groups,
            root,
            workspace,
            cancellationToken
        );
        WriteMovieMenu(renderer, root);
    }

    private void WriteMovieMenu(
        TemplateRenderer renderer,
        string root
    )
    {
        string directory = Output(root, "00");
        renderer.RenderMenu(
            directory,
            "menu",
            template.Node("menu/index/area")
        );
        Document(Definition(_definitions.MovieMenu()))
            .Save(directory);
        for (int index = 0; index <= 11; index++)
        {
            if (index != 1 && (index == 0 || Enabled(index)))
            {
                fileSystem.CreateDirectory(
                    Output(root, $"00/{index:00}")
                );
            }
        }
    }

    private void WriteMovieListing(
        IEnumerable<MediaContext> movies,
        XElement groups,
        string root,
        string relative,
        string background,
        bool collapseSagas
    )
    {
        DuneFile file = relative.StartsWith(
            "00/03/",
            StringComparison.Ordinal
        )
            ? Document(
                Definition(_definitions.IndexListing("../.."))
            )
            : MovieListing(background);
        string directory = Output(root, relative);
        HashSet<string> included = new(StringComparer.Ordinal);
        int index = 0;
        foreach (MediaContext movie in movies)
        {
            string name = movie.Header.ValueAt("filename");
            XElement? saga = collapseSagas
                ? groups
                    .Descendants("saga")
                    .FirstOrDefault(group =>
                        group
                            .Elements("movie")
                            .Any(node => node.Value == name)
                    )
                : null;
            string target = saga is null
                ? LegacyExportPaths.Media(name, false)
                : "00/98/" + saga.Element("name")!.Value;
            if (!included.Add(target))
            {
                continue;
            }

            string caption = saga is null
                ? movie.Info.ValueAt("sort_title")
                : saga.Element("sort_name")?.Value ?? "";
            if (
                relative == "00/02"
                && _catalog.SortRecentByDate
                && DateTime.TryParse(
                    movie.Header.ValueAt("date"),
                    CultureInfo.CurrentCulture,
                    DateTimeStyles.None,
                    out DateTime added
                )
            )
            {
                caption = FormattableString.Invariant(
                    $"{(timeProvider.GetLocalNow().Date - added.Date).Days:000}_{caption}"
                );
            }

            AddMovie(
                file,
                index++,
                caption,
                directory,
                Output(root, target)
            );
        }

        file.Save(directory);
    }

    private void AddMovie(
        DuneFile file,
        int index,
        string caption,
        string directory,
        string target
    )
    {
        string url = Path.GetRelativePath(directory, target)
            .Replace(Path.DirectorySeparatorChar, '/');
        file.Item(
            index,
            caption,
            url,
            url + "/icon.aai",
            scale: template
                .Node("movie/iconview/icon")
                .Attr("scale")
        );
    }
}
