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
        // Générer les raccourcis avant les index : une erreur de
        // rendu ultérieure ne doit pas les faire disparaître.
        WriteSelections(renderer, movies, groups, root);
        WriteShortcuts(renderer, groups, shortcuts, root);
        fileSystem.CreateDirectory(Output(root, "00"));
        if (cards.Any(card => card.IsSeries))
        {
            fileSystem.CreateDirectory(Output(root, "01"));
        }

        WriteTopNavigation(renderer, root);
        WriteMovies(
            renderer,
            movies,
            groups,
            root,
            workspace,
            cancellationToken
        );
        WriteSeries(renderer, cards, root);
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
        WriteGeneralMovieListing(movies, groups, root, directory);
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

    private void WriteGeneralMovieListing(
        IReadOnlyList<MediaContext> cards,
        XElement groups,
        string root,
        string directory
    )
    {
        // yaDIS2 dédoublonne avant que CreateDF choisisse le format du fond.
        DuneFile file = Document(_definitions.MovieListing())
            .Set("background_path", "background.aai");
        int index = 0;
        foreach (MediaContext card in cards)
        {
            if (
                card.Header.ValueAt("type") != "0"
                || !int.TryParse(
                    card.Header.ValueAt("status"),
                    CultureInfo.InvariantCulture,
                    out int status
                )
                || status == (int)MediaProcessingState.Unprocessed
            )
            {
                continue;
            }

            if (WriteGeneralMovie(file, index, card, groups, root, directory))
            {
                index++;
            }
        }

        file.Set("background_path", Background).Save(directory);
    }

    private bool WriteGeneralMovie(
        DuneFile file,
        int index,
        MediaContext movie,
        XElement groups,
        string root,
        string directory
    )
    {
        string name = movie.Header.ValueAt("filename");
        XElement? saga = FindSaga(groups, name);
        string? sagaName = saga?.Element("name")?.Value;
        if (sagaName is not null && file.ContainsDefinitionSuffix(sagaName))
        {
            // ProcessForm avance le compteur même si la saga est déjà représentée.
            return true;
        }

        XElement? caption = saga is null
            ? movie.Info.Element("sort_title")
            : saga.Element("sort_name");
        if (caption is null || (saga is not null && sagaName is null))
        {
            return false;
        }

        string target = sagaName is null
            ? LegacyExportPaths.Media(name, false)
            : "00/98/" + sagaName;
        AddMovie(file, index, caption.Value, directory, Output(root, target));
        return true;
    }

    private static XElement? FindSaga(XElement groups, string filename) =>
        groups.Descendants("saga").FirstOrDefault(saga =>
            saga.Elements("movie").Any(movie => movie.Value == filename)
        );

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
            if (index == 1)
            {
                continue;
            }

            string categoryDirectory = Output(root, $"00/{index:00}");
            if (index == 0 || Enabled(index))
            {
                fileSystem.CreateDirectory(categoryDirectory);
                continue;
            }

            // yaDIS2 renomme les catégories désactivées pour que
            // SyncInterface ne les publie pas dans la destination.
            if (!fileSystem.DirectoryExists(categoryDirectory))
            {
                continue;
            }

            string hidden = Output(root, $"00/_{index:00}");
            if (fileSystem.DirectoryExists(hidden))
            {
                fileSystem.DeleteDirectory(hidden);
            }

            fileSystem.MoveDirectory(categoryDirectory, hidden);
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
        const string LegacyBackground = "../00/background.aai";
        bool alphabetListing = relative.StartsWith(
            "00/03/",
            StringComparison.Ordinal
        );
        // ProcessForm recherche les doublons avant la conversion du fond par CreateDF.
        DuneFile file = alphabetListing
            ? Document(
                Definition(_definitions.IndexListing("../.."))
            )
            : MovieListing(LegacyBackground);
        string directory = Output(root, relative);
        int index = 0;
        foreach (MediaContext movie in movies)
        {
            string name = movie.Header.ValueAt("filename");
            XElement? saga = collapseSagas
                ? FindSaga(groups, name)
                : null;
            string target = saga is null
                ? LegacyExportPaths.Media(name, false)
                : "00/98/" + saga.Element("name")!.Value;
            if (
                saga is not null
                && file.ContainsDefinitionSuffix(saga.Element("name")!.Value)
            )
            {
                // ProcessForm consomme les doublons dans les sélections,
                // mais les ignore avant d'avancer le compteur des récents.
                if (relative != "00/02")
                {
                    index++;
                }
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

        if (!alphabetListing)
        {
            file.Set("background_path", background);
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
