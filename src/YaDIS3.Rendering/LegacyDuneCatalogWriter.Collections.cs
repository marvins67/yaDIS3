using System.Globalization;
using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

internal sealed partial class LegacyDuneCatalogWriter
{
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
            // ProcessForm isole les échecs de chaque saga : une image
            // manquante ne doit pas supprimer les sagas suivantes.
            try
            {
                WriteSaga(renderer, movies, saga, root);
            }
            catch (Exception exception)
                when (exception is not OperationCanceledException)
            {
                // yaDIS2 continue l'export après une erreur propre à une saga.
            }
        }
    }

    private void WriteSaga(
        TemplateRenderer renderer,
        MediaContext[] movies,
        XElement saga,
        string root
    )
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
        string[] members = saga.Elements("movie")
            .Select(node => node.Value)
            .ToArray();
        WriteSagaDefinition(members, root, relative);
        string poster = saga.Element("poster")?.Value ?? "";
        if (members.Contains(poster, StringComparer.Ordinal))
        {
            string icon = Path.Combine(
                Output(root, LegacyExportPaths.Media(poster, false)),
                "icon.aai"
            );
            if (fileSystem.FileExists(icon))
            {
                renderer.RenderSagaIcon(icon, directory);
            }
        }

        MediaContext? artwork = movies.FirstOrDefault(movie =>
            movie.Header.ValueAt("filename")
                == saga.Element("fanart")?.Value
        );
        MediaContext? posterMedia = movies.FirstOrDefault(movie =>
            movie.Header.ValueAt("filename") == poster
        );
        renderer.RenderSagaBackground(
            artwork ?? posterMedia,
            name,
            artwork is not null,
            directory
        );
    }

    private void WriteSagaDefinition(
        string[] members,
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
        if (members.Length < columns)
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
        // ProcessForm numérote les films dans l’ordre XML de la saga.
        foreach (string member in members)
        {
            AddMovie(
                file,
                index,
                index.ToString("00", CultureInfo.InvariantCulture),
                directory,
                Output(root, LegacyExportPaths.Media(member, false))
            );
            index++;
        }

        file.Save(directory);
    }

}
