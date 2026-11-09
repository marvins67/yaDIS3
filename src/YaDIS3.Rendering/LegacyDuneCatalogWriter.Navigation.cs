using System.Globalization;
using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

internal sealed partial class LegacyDuneCatalogWriter
{
    private DuneFile Panel(
        XElement source,
        bool alphabet,
        bool includeDigits = false,
        bool selectedIcon = false
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
                    .Replace(
                        "|icon_path=icon.aai|",
                        selectedIcon
                            ? "|icon_path=icon.aai|icon_sel_path=icon_sel.aai|"
                            : "|icon_path=icon.aai|",
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
        WriteTopNavigation(renderer, root);
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

    private void WriteTopNavigation(
        TemplateRenderer renderer,
        string root
    )
    {
        List<(string Directory, string Caption)> top =
        [
            (Output(root, "00"), _texts["63"]),
        ];
        if (fileSystem.DirectoryExists(Output(root, "01")))
        {
            top.Add((Output(root, "01"), _texts["64"]));
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

    private string CategoryLabel(int index) =>
        _texts[
            (40 + index).ToString(CultureInfo.InvariantCulture)
        ];

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
