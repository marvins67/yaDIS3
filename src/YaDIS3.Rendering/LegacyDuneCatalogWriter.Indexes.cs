using System.Globalization;
using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

internal sealed partial class LegacyDuneCatalogWriter
{
    private void WriteIndexes(
        TemplateRenderer renderer,
        MediaContext[] movies,
        XElement groups,
        string root,
        string workspace,
        CancellationToken cancellationToken
    )
    {
        LegacyMovieIndexStore store = new(
            template,
            options,
            fileSystem
        );
        for (int index = 4; index <= 11; index++)
        {
            if (Enabled(index))
            {
                store.CopyCategory(
                    workspace,
                    root,
                    index,
                    cancellationToken
                );
            }
        }

        if (Enabled(3))
        {
            WriteAlphabet(renderer, movies, groups, root);
        }
        WriteIndexMenus(renderer, root);
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
            string caption = movie.Info.ValueAt("sort_title");
            // yaDIS2 conserve la première version correspondant au libellé, pas au fichier.
            if (file.ContainsIndexCaption(caption))
            {
                continue;
            }
            AddMovie(
                file,
                index++,
                caption,
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

        WriteAlphabetEntries(movies, groups, root);
    }

    private void WriteAlphabetEntries(
        MediaContext[] movies,
        XElement groups,
        string root
    )
    {
        Dictionary<string, DuneFile> listings = new(
            StringComparer.OrdinalIgnoreCase
        );
        Dictionary<string, int> counts = new(
            StringComparer.OrdinalIgnoreCase
        );
        foreach (MediaContext movie in movies)
        {
            string name = movie.Header.ValueAt("filename");
            if (FindSaga(groups, name) is null)
            {
                AppendAlphabetEntry(
                    movie.Info.ValueAt("sort_title"),
                    LegacyExportPaths.Media(name, false),
                    root,
                    listings,
                    counts
                );
            }
        }

        // ProcessForm ajoute ensuite toutes les sagas dans l'ordre XML,
        // même si aucun de leurs membres ne figure dans la collection.
        foreach (XElement saga in groups.Descendants("saga"))
        {
            AppendAlphabetEntry(
                saga.ValueAt("sort_name"),
                "00/98/" + saga.ValueAt("name"),
                root,
                listings,
                counts
            );
        }

        foreach (KeyValuePair<string, DuneFile> listing in listings)
        {
            listing.Value.Save(
                Output(root, "00/03/" + listing.Key)
            );
        }
    }

    private void AppendAlphabetEntry(
        string caption,
        string target,
        string root,
        Dictionary<string, DuneFile> listings,
        Dictionary<string, int> counts
    )
    {
        if (caption.Length == 0)
        {
            return;
        }

        string letter = LegacyExportPaths
            .Alphabet(caption)
            .ToUpperInvariant();
        // CreateDF ne crée pas de dossier : seules les rubriques préparées
        // par CreateAlphabet sont accessibles, sans normaliser les accents.
        if (
            letter != "0-9"
            && (letter.Length != 1 || letter[0] is < 'A' or > 'Z')
        )
        {
            return;
        }

        if (!listings.TryGetValue(letter, out DuneFile? file))
        {
            file = Document(
                Definition(_definitions.IndexListing("../.."))
            );
            listings.Add(letter, file);
            counts.Add(letter, 0);
        }

        AddMovie(
            file,
            counts[letter],
            caption,
            Output(root, "00/03/" + letter),
            Output(root, target)
        );
        counts[letter]++;
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
                    false,
                    selectedIcon: true
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
                .Save(child);
            Label(
                renderer,
                child,
                letter.ToString(),
                template.Node("menu/index/alphabet/area")
            );
        }
    }
}
