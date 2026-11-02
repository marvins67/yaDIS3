using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

/// <summary>Reproduit l'arborescence de navigation de ProcessForm.</summary>
internal sealed class LegacyDuneCatalogWriter(
    DuneTemplate template,
    RenderOptions options,
    TimeProvider timeProvider,
    IExportFileSystem fileSystem,
    LegacyCatalogOptions catalog
)
{
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

    private void WriteRecent(
        MediaContext[] movies,
        XElement groups,
        string root
    )
    {
        DateTime today = timeProvider.GetLocalNow().Date;
        MediaContext[] recent = movies
            .Where(movie =>
                DateTime.TryParse(
                    movie.Header.ValueAt("date"),
                    CultureInfo.CurrentCulture,
                    DateTimeStyles.None,
                    out DateTime date
                )
                && (today - date.Date).Days <= _catalog.LastDays
            )
            .ToArray();
        WriteMovieListing(
            recent,
            groups,
            root,
            "00/02",
            "../00/" + Background,
            true
        );
    }

    private void WriteSelections(
        TemplateRenderer renderer,
        MediaContext[] movies,
        XElement groups,
        string root
    )
    {
        foreach (
            XElement selection in groups
                .Descendants("selection")
                .Where(node => node.Attr("enabled") == "yes")
        )
        {
            string name = selection.Element("name")!.Value;
            LegacyExportPaths.ValidateName(name);
            string relative = "00/01_" + name;
            HashSet<string> filenames = selection
                .Elements("movie")
                .Select(node => node.Value)
                .ToHashSet(StringComparer.Ordinal);
            WriteMovieListing(
                movies.Where(movie =>
                    filenames.Contains(
                        movie.Header.ValueAt("filename")
                    )
                ),
                groups,
                root,
                relative,
                "../00/" + Background,
                true
            );
        }
    }

    private void WriteSagas(
        TemplateRenderer renderer,
        MediaContext[] movies,
        XElement groups,
        string root
    )
    {
        foreach (XElement saga in groups.Descendants("saga"))
        {
            string name = saga.Element("name")!.Value;
            LegacyExportPaths.ValidateName(name);
            string relative = "00/98/" + name;
            string directory = Output(root, relative);
            renderer.RenderMenu(
                directory,
                "movie",
                template.Node("movie/saga/iconview/area")
            );
            MediaContext[] members = saga.Elements("movie")
                .SelectMany(node =>
                    movies.Where(movie =>
                        movie.Header.ValueAt("filename")
                        == node.Value
                    )
                )
                .ToArray();
            WriteSagaDefinition(members, root, relative);
            string poster = saga.Element("poster")?.Value ?? "";
            CopySagaIcon(members, poster, root, directory);
            MediaContext? posterMedia = members.FirstOrDefault(
                movie =>
                    movie.Header.ValueAt("filename") == poster
            );
            if (posterMedia is not null)
            {
                MediaContext? fanart = movies.FirstOrDefault(
                    movie =>
                        movie.Header.ValueAt("filename")
                        == saga.Element("fanart")?.Value
                );
                renderer.RenderSagaBackground(
                    fanart ?? posterMedia,
                    name,
                    fanart is not null,
                    directory
                );
            }
        }
    }

    private void WriteSagaDefinition(
        MediaContext[] members,
        string root,
        string relative
    )
    {
        XElement area = new(
            template.Node("movie/saga/iconview/area")
        );
        int columns = int.Parse(
            template
                .Node("movie/iconview/area")
                .ValueAt("columns"),
            CultureInfo.InvariantCulture
        );
        if (members.Length > 0 && members.Length < columns)
        {
            int width = (int)
                Math.Round(
                    (double)area.Int("width")
                        / columns
                        * members.Length
                );
            area.SetAttributeValue(
                "x",
                (int)Math.Round((1920 - width) / 2.0)
            );
            area.SetAttributeValue("width", width);
            columns = members.Length;
        }

        DuneFile file = new DuneFile(fileSystem)
            .Set("background_order", "before_all")
            .Set("background_path", Background)
            .Set("use_icon_view", "yes")
            .Grid(area, columns, 1);
        foreach (
            string edge in (string[])
                ["left", "right", "top", "bottom"]
        )
        {
            file.Set("content_box_padding_" + edge, 0);
        }

        file.Set(
                "icon_selection_box_width",
                template
                    .Node("movie/iconview/icon")
                    .Attr("width")
            )
            .Set(
                "icon_selection_box_height",
                template
                    .Node("movie/iconview/icon")
                    .Attr("height")
            );
        foreach (
            string chrome in (string[])
                [
                    "captions",
                    "help_line",
                    "path_box",
                    "content_box_background",
                ]
        )
        {
            file.Set("paint_" + chrome, "no");
        }

        file.Set("system_files", "*.aai,*.jpg");
        string directory = Output(root, relative);
        int index = 0;
        foreach (MediaContext member in members)
        {
            AddMovie(
                file,
                index++,
                member.Info.ValueAt("sort_title"),
                directory,
                Output(
                    root,
                    LegacyExportPaths.Media(
                        member.Header.ValueAt("filename"),
                        false
                    )
                )
            );
        }

        file.Save(directory);
    }

    private void CopySagaIcon(
        MediaContext[] members,
        string poster,
        string root,
        string directory
    )
    {
        MediaContext? member = members.FirstOrDefault(movie =>
            movie.Header.ValueAt("filename") == poster
        );
        if (member is not null)
        {
            fileSystem.CopyFile(
                Path.Combine(
                    Output(
                        root,
                        LegacyExportPaths.Media(poster, false)
                    ),
                    "icon.aai"
                ),
                Path.Combine(directory, "icon.aai"),
                true
            );
        }
    }

    private void WriteIndexes(
        TemplateRenderer renderer,
        MediaContext[] movies,
        XElement groups,
        string root,
        CancellationToken cancellationToken
    )
    {
        foreach (
            IGrouping<
                string,
                (LegacyIndexEntry Entry, MediaContext Movie)
            > group in movies
                .SelectMany(movie =>
                    MovieIndexes(movie)
                        .Select(entry =>
                            (Entry: entry, Movie: movie)
                        )
                )
                .GroupBy(item => item.Entry.Directory)
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (
                !Enabled(
                    int.Parse(
                        group.Key.Split('/')[1],
                        CultureInfo.InvariantCulture
                    )
                )
            )
            {
                continue;
            }

            string directory = Output(root, group.Key);
            WriteIndexListing(
                group.Select(item => item.Movie),
                root,
                group.Key
            );
            string[] components = group.Key.Split('/');
            string kind = IndexKind(components[1]);
            Label(
                renderer,
                directory,
                group.First().Entry.Caption,
                template.Node("menu/index/" + kind + "/area")
            );
        }

        if (Enabled(3))
        {
            WriteAlphabet(renderer, movies, groups, root);
        }
        WriteIndexMenus(renderer, root);
    }

    private static IReadOnlyList<LegacyIndexEntry> MovieIndexes(
        MediaContext movie
    )
    {
        try
        {
            return LegacyMovieIndexes.Create(
                movie.Info,
                // AddToIndex utilise //mediainfo, imbriqué dans info dans yaDIS2.
                movie
                    .Root.Descendants("mediainfo")
                    .FirstOrDefault()
            );
        }
        catch (Exception exception)
            when (exception
                    is InvalidDataException
                        or ArgumentException
                        or IndexOutOfRangeException
                        or OverflowException
            )
        {
            string path = Path.Combine(
                movie.DataDirectory,
                "data.xml"
            );
            throw new InvalidDataException(
                $"Création des index de la fiche « {movie.Title} » ({path}) : {exception.Message}",
                exception
            );
        }
    }

    private void WriteIndexListing(
        IEnumerable<MediaContext> movies,
        string root,
        string relative
    )
    {
        string directory = Output(root, relative);
        string ancestor =
            relative.StartsWith(
                "00/05/",
                StringComparison.Ordinal
            )
            || relative.StartsWith(
                "00/06/",
                StringComparison.Ordinal
            )
                ? "../../.."
                : "../..";
        DuneFile file = Document(
            Definition(_definitions.IndexListing(ancestor))
        );
        int index = 0;
        foreach (
            MediaContext movie in movies.DistinctBy(movie =>
                movie.Header.ValueAt("filename")
            )
        )
        {
            AddMovie(
                file,
                index++,
                movie.Info.ValueAt("sort_title"),
                directory,
                Output(
                    root,
                    LegacyExportPaths.Media(
                        movie.Header.ValueAt("filename"),
                        false
                    )
                )
            );
        }

        file.Save(directory);
    }

    private void WriteAlphabet(
        TemplateRenderer renderer,
        MediaContext[] movies,
        XElement groups,
        string root
    )
    {
        foreach (
            string letter in Enumerable
                .Repeat("0-9", 1)
                .Concat(
                    Enumerable
                        .Range('A', 26)
                        .Select(value =>
                            ((char)value).ToString()
                        )
                )
        )
        {
            string relative = "00/03/" + letter;
            WriteIndexListing([], root, relative);
            Label(
                renderer,
                Output(root, relative),
                letter,
                template.Node("menu/index/alphabet/area")
            );
        }

        foreach (
            IGrouping<string, MediaContext> group in movies
                .Where(movie =>
                    movie.Info.ValueAt("sort_title").Length > 0
                )
                .GroupBy(movie =>
                    LegacyExportPaths.Alphabet(
                        AlphabetCaption(movie, groups)
                    )
                )
        )
        {
            LegacyExportPaths.ValidateName(group.Key);
            WriteMovieListing(
                group,
                groups,
                root,
                "00/03/" + group.Key,
                "../../00/" + Background,
                true
            );
        }
    }

    private static string AlphabetCaption(
        MediaContext movie,
        XElement groups
    )
    {
        XElement? saga = groups
            .Descendants("saga")
            .FirstOrDefault(group =>
                group
                    .Elements("movie")
                    .Any(node =>
                        node.Value
                        == movie.Header.ValueAt("filename")
                    )
            );
        return saga?.Element("sort_name")?.Value
            ?? movie.Info.ValueAt("sort_title");
    }

    private void WriteIndexMenus(
        TemplateRenderer renderer,
        string root
    )
    {
        for (int index = 3; index <= 11; index++)
        {
            if (!Enabled(index))
            {
                continue;
            }

            string relative = FormattableString.Invariant(
                $"00/{index:00}"
            );
            string kind = IndexKind(
                index.ToString(
                    "00",
                    CultureInfo.InvariantCulture
                )
            );
            bool letters = index is 3 or 5 or 6;
            XElement area = template.Node(
                "menu/index/"
                    + (letters ? "alphabet" : kind)
                    + "/area"
            );
            string directory = Output(root, relative);
            renderer.RenderMenu(directory, "menu", area);
            Panel(area, letters, index == 3).Save(directory);
            if (index is 5 or 6)
            {
                WritePeopleLetters(
                    renderer,
                    root,
                    relative,
                    kind
                );
            }
        }
    }

    private void WritePeopleLetters(
        TemplateRenderer renderer,
        string root,
        string relative,
        string kind
    )
    {
        string directory = Output(root, relative);
        renderer.RenderMenu(
            directory,
            "menu",
            template.Node("menu/index/" + kind + "/area")
        );
        fileSystem.CopyFile(
            Path.Combine(directory, Background),
            Path.Combine(
                directory,
                options.AaiBackgrounds
                    ? "background2.aai"
                    : "background2.jpg"
            ),
            true
        );
        renderer.RenderMenu(
            directory,
            "menu",
            template.Node("menu/index/alphabet/area")
        );
        foreach (
            char letter in Enumerable
                .Range('A', 26)
                .Select(value => (char)value)
        )
        {
            string child = Output(root, relative + "/" + letter);
            Panel(
                    template.Node(
                        "menu/index/" + kind + "/area"
                    ),
                    false
                )
                .Set(
                    "background_path",
                    "../"
                        + (
                            options.AaiBackgrounds
                                ? "background2.aai"
                                : "background2.jpg"
                        )
                )
                .Set("icon_sel_path", "icon_sel.aai")
                .Save(child);
            Label(
                renderer,
                child,
                letter.ToString(),
                template.Node("menu/index/alphabet/area")
            );
        }
    }

    private DuneFile Panel(
        XElement source,
        bool alphabet,
        bool includeDigits = false
    )
    {
        XElement area = new(source);
        int margin = area.Int("margin");
        area.SetAttributeValue("x", area.Int("x") + margin);
        area.SetAttributeValue(
            "width",
            area.Int("width") - margin * 2
        );
        if (!alphabet)
        {
            area.SetAttributeValue("y", area.Int("y") + margin);
            area.SetAttributeValue(
                "height",
                area.Int("height") - margin * 2
            );
        }

        int columns = int.Parse(
            source.ValueAt("columns"),
            CultureInfo.InvariantCulture
        );
        int rows = int.Parse(
            source.ValueAt("rows"),
            CultureInfo.InvariantCulture
        );
        return Document(
                Definition(_definitions.MovieMenu())
                    .Replace(
                        "|paint_icon_selection_box=no",
                        "",
                        StringComparison.Ordinal
                    )
            )
            .Grid(
                area,
                includeDigits ? columns + 1 : columns,
                rows
            )
            .Set("system_files", "*.aai,*.jpg");
    }

    private static void Label(
        TemplateRenderer renderer,
        string directory,
        string caption,
        XElement area
    )
    {
        string kind = area.Parent?.Name.LocalName ?? "alphabet";
        renderer.RenderLegacyIndexLabel(
            directory,
            caption,
            kind
        );
    }

    private void WriteShortcuts(
        TemplateRenderer renderer,
        XElement groups,
        XElement shortcuts,
        string root
    )
    {
        int index = 0;
        foreach (
            XElement shortcut in shortcuts.Descendants(
                "shortcut"
            )
        )
        {
            string name = shortcut.Value;
            LegacyExportPaths.ValidateName(name);
            string target = shortcut.Attr("type") switch
            {
                "0"
                    when groups
                        .Descendants("selection")
                        .Any(selection =>
                            selection.Attr("enabled") == "yes"
                            && selection.Element("name")?.Value
                                == name
                        ) => "../00/01_" + name,
                "1" => "../00/04/" + name,
                "0" => "",
                _ => shortcut.Attr("path"),
            };
            string directory = Output(
                root,
                FormattableString.Invariant(
                    $"05_{index++:00}_{name}"
                )
            );
            if (target.Length > 0)
            {
                Document(
                        "media_url="
                            + target
                            + "|media_action=browse|icon_path=icon.aai|icon_valign=center"
                    )
                    .Save(directory);
            }
        }
    }

    private void WriteRoot(
        TemplateRenderer renderer,
        IReadOnlyList<MediaContext> cards,
        XElement groups,
        XElement shortcuts,
        string root
    )
    {
        renderer.RenderMenu(root, "menu");
        Document(Definition(_definitions.Root()))
            .Set(
                "all_children.paint_icon_selection_box",
                _catalog.SelectionBox ? "yes" : "no"
            )
            .Set(
                "all_children.paint_scrollbar",
                _catalog.Scrollbar ? "yes" : "no"
            )
            .Save(root);
        fileSystem.CreateDirectory(Output(root, "00"));
        if (cards.Any(card => card.IsSeries))
        {
            fileSystem.CreateDirectory(Output(root, "01"));
        }

        string hidden = "*.aai,*.jpg,98,99";
        foreach (
            XElement shortcut in shortcuts
                .Descendants("shortcut")
                .Where(node => node.Attr("type") == "0")
        )
        {
            hidden += ",01_" + shortcut.Value;
        }

        Document(Definition(_definitions.MovieMenu()))
            .Set("system_files", hidden)
            .Save(Output(root, "00"));
        renderer.SaveApplicationIcon(
            Path.Combine(root, "icon.aai")
        );
        WriteNavigationBars(renderer, shortcuts, root);
        ComposeBackgrounds(renderer, root);
    }

    private void WriteNavigationBars(
        TemplateRenderer renderer,
        XElement shortcuts,
        string root
    )
    {
        List<(string Directory, string Caption)> top =
        [
            (
                Output(root, "00"),
                options.Language == "fr" ? "Films" : "Movies"
            ),
        ];
        if (fileSystem.DirectoryExists(Output(root, "01")))
        {
            top.Add(
                (
                    Output(root, "01"),
                    options.Language == "fr"
                        ? "Séries TV"
                        : "TV Shows"
                )
            );
        }

        top.AddRange(
            fileSystem
                .EnumerateDirectories(root, "05_*")
                .Order(StringComparer.Ordinal)
                .Select(path =>
                    (path, Path.GetFileName(path)[6..])
                )
        );
        renderer.RenderLegacyNavigation(top, "menu/main");
        List<(string Directory, string Caption)> indexes = [];
        foreach (
            string path in fileSystem
                .EnumerateDirectories(Output(root, "00"))
                .Order(StringComparer.Ordinal)
        )
        {
            string name = Path.GetFileName(path);
            bool hiddenSelection =
                name.StartsWith("01_", StringComparison.Ordinal)
                && shortcuts
                    .Descendants("shortcut")
                    .Any(shortcut =>
                        shortcut.Attr("type") == "0"
                        && shortcut.Value == name[3..]
                    );
            if (
                name is "98" or "99"
                || name.StartsWith('_')
                || hiddenSelection
            )
            {
                continue;
            }

            string caption = name.StartsWith(
                "01_",
                StringComparison.Ordinal
            )
                ? name[3..]
                : CategoryLabel(
                    int.Parse(name, CultureInfo.InvariantCulture)
                );
            indexes.Add((path, caption));
        }

        renderer.RenderLegacyNavigation(indexes, "menu/index");
    }

    private void ComposeBackgrounds(
        TemplateRenderer renderer,
        string root
    )
    {
        foreach (
            string path in fileSystem.EnumerateDirectories(
                Output(root, "00")
            )
        )
        {
            if (
                !fileSystem.FileExists(
                    Path.Combine(
                        path,
                        "background"
                            + (
                                options.AaiBackgrounds
                                    ? ".aai"
                                    : ".jpg"
                            )
                    )
                )
            )
            {
                continue;
            }

            renderer.OverlayNavigation(
                path,
                Path.Combine(root, "00", "icon.aai"),
                template.Node("menu/main/area")
            );
            if (
                fileSystem.FileExists(
                    Path.Combine(path, "icon.aai")
                )
            )
            {
                renderer.OverlayNavigation(
                    path,
                    Path.Combine(path, "icon.aai"),
                    template.Node("menu/index/area")
                );
            }
            string secondary = Path.Combine(
                path,
                options.AaiBackgrounds
                    ? "background2.aai"
                    : "background2.jpg"
            );
            if (fileSystem.FileExists(secondary))
            {
                renderer.OverlayNavigation(
                    path,
                    Path.Combine(root, "00", "icon.aai"),
                    template.Node("menu/main/area"),
                    "background2"
                );
                renderer.OverlayNavigation(
                    path,
                    Path.Combine(path, "icon.aai"),
                    template.Node("menu/index/area"),
                    "background2"
                );
            }
        }

        renderer.OverlayNavigation(
            Output(root, "00"),
            Path.Combine(root, "00", "icon.aai"),
            template.Node("menu/main/area")
        );
        if (fileSystem.DirectoryExists(Output(root, "01")))
        {
            renderer.OverlayNavigation(
                Output(root, "01"),
                Path.Combine(root, "01", "icon.aai"),
                template.Node("menu/main/area")
            );
        }
    }

    private bool Enabled(int category)
    {
        int index = category - 2;
        return index >= 0
            && index < _catalog.Categories.Length
            && _catalog.Categories[index] != '0';
    }

    private string CategoryLabel(int index)
    {
        string[] french =
        [
            "Tous les films",
            "Sélections",
            "Ajouts récents",
            "A-Z",
            "Genres",
            "Acteurs",
            "Réalisateurs",
            "Durées",
            "Pays",
            "Années",
            "Notes",
            "Formats",
        ];
        string[] english =
        [
            "All movies",
            "Selections",
            "Recently added",
            "A-Z",
            "Genres",
            "Actors",
            "Directors",
            "Durations",
            "Countries",
            "Years",
            "Ratings",
            "Formats",
        ];
        return (options.Language == "fr" ? french : english)[
            index
        ];
    }

    private static string IndexKind(string index)
    {
        return index switch
        {
            "03" or "05" or "06" => index == "03" ? "alphabet"
            : index == "05" ? "actors"
            : "directors",
            "04" => "genres",
            "07" => "durations",
            "08" => "countries",
            "09" => "years",
            "10" => "ratings",
            "11" => "types",
            _ => throw new ArgumentOutOfRangeException(
                nameof(index)
            ),
        };
    }
}
