using System.Globalization;
using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

internal sealed class DuneFile(
    IExportFileSystem? fileSystem = null
)
{
    private readonly IExportFileSystem _fileSystem =
        fileSystem ?? new ExportFileSystem();

    private readonly Dictionary<string, string> _values = new(
        StringComparer.Ordinal
    );

    /// <summary>
    /// Adds or replaces a validated Dune configuration value.
    /// </summary>
    public DuneFile Set(string key, object? value)
    {
        _values[key] = DunePaths.CleanValue(
            Convert.ToString(value, CultureInfo.InvariantCulture)
                ?? ""
        );
        return this;
    }

    /// <summary>
    /// Configures a background and hides the standard Dune
    /// chrome.
    /// </summary>
    public DuneFile Screen(string background)
    {
        Set("background_order", "before_all")
            .Set("background_path", background)
            .Set("use_icon_view", "yes");
        foreach (
            string name in new[]
            {
                "paint_captions",
                "paint_help_line",
                "paint_path_box",
                "paint_content_box_background",
                "paint_scrollbar",
                "paint_icon_selection_box",
            }
        )
        {
            Set(name, "no");
        }

        return Set("system_files", "*")
            .Set("content_box_padding_left", 0)
            .Set("content_box_padding_right", 0)
            .Set("content_box_padding_top", 0)
            .Set("content_box_padding_bottom", 0);
    }

    /// <summary>
    /// Configures the catalog grid from its template area.
    /// </summary>
    public DuneFile Grid(XElement area, int columns, int rows)
    {
        Set("num_cols", columns).Set("num_rows", rows);
        foreach (
            string name in new[] { "x", "y", "width", "height" }
        )
        {
            Set("content_box_" + name, area.Int(name));
        }

        return this;
    }

    /// <summary>
    /// Configures normal and selected artwork for a catalog
    /// entry.
    /// </summary>
    public DuneFile Icon(string path, string scale = "1")
    {
        return Set("icon_path", path)
            .Set("icon_sel_path", path)
            .Set("icon_valign", "center")
            .Set("icon_scale_factor", scale)
            .Set("icon_sel_scale_factor", "1");
    }

    /// <summary>
    /// Adds a navigation or playback item to the Dune
    /// configuration.
    /// </summary>
    public DuneFile Item(
        int index,
        string caption,
        string url,
        string? icon = null,
        string? selected = null,
        string scale = "1",
        bool play = false
    )
    {
        string prefix = $"item.{index}.";
        Set(
            prefix + "caption",
            caption.Replace('\r', ' ').Replace('\n', ' ')
        );
        Set(prefix + "media_action", play ? "play" : "browse")
            .Set(prefix + "media_url", url);
        if (icon is not null)
        {
            Set(prefix + "icon_path", icon)
                .Set(prefix + "icon_sel_path", selected ?? icon)
                .Set(prefix + "icon_scale_factor", scale)
                .Set(prefix + "icon_sel_scale_factor", "1")
                .Set(prefix + "icon_valign", "center");
        }

        return this;
    }

    /// <summary>Reprend la recherche littérale de CreateIndexItem, limitée aux entrées.</summary>
    public bool ContainsIndexCaption(string caption)
    {
        string marker = caption + "|";
        return _values.Any(entry =>
            entry.Key.StartsWith(
                "item.",
                StringComparison.Ordinal
            )
            && (entry.Key + "=" + entry.Value + "|").Contains(
                marker,
                StringComparison.Ordinal
            )
        );
    }

    /// <summary>Conserve les entrées existantes avec leur numérotation historique.</summary>
    public DuneFile ImportIndexItems(string content)
    {
        foreach (string line in content.Split('\n'))
        {
            int separator = line.IndexOf('=');
            if (
                separator > 0
                && line.StartsWith(
                    "item.",
                    StringComparison.Ordinal
                )
            )
            {
                Set(
                    line[..separator],
                    line[(separator + 1)..].TrimEnd('\r')
                );
            }
        }
        return this;
    }

    /// <summary>Ajoute une version seulement si son libellé n'existe pas déjà.</summary>
    public void AddIndexMovie(
        string caption,
        string url,
        string scale
    )
    {
        if (ContainsIndexCaption(caption))
        {
            RefreshIndexScale(caption, scale);
            return;
        }
        Item(
            NextIndex(),
            caption,
            url,
            url + "/icon.aai",
            scale: scale
        );
    }

    private int NextIndex()
    {
        return _values
                .Keys.Where(key =>
                    key.StartsWith(
                        "item.",
                        StringComparison.Ordinal
                    )
                )
                .Select(key =>
                    int.Parse(
                        key.Split('.')[1],
                        CultureInfo.InvariantCulture
                    )
                )
                .DefaultIfEmpty(-1)
                .Last() + 1;
    }

    private void RefreshIndexScale(string caption, string scale)
    {
        foreach (string key in _values.Keys.ToArray())
        {
            if (
                key.EndsWith(
                    ".caption",
                    StringComparison.Ordinal
                )
                && _values[key] == caption
            )
            {
                string prefix = key[..^"caption".Length];
                Set(prefix + "icon_scale_factor", scale);
                Set(prefix + "icon_sel_scale_factor", "1");
                break;
            }
        }
    }

    /// <summary>Importe les propriétés dans l'ordre exact de yaDIS2.</summary>
    public static DuneFile FromLegacy(
        string content,
        IExportFileSystem? fileSystem = null
    )
    {
        DuneFile file = new(fileSystem);
        foreach (string line in content.Split('|'))
        {
            int separator = line.IndexOf('=');
            if (separator >= 0)
            {
                file.Set(
                    line[..separator],
                    line[(separator + 1)..]
                );
            }
        }

        return file;
    }

    /// <summary>Écrit le format historique, BOM et CRLF inclus.</summary>
    public void Save(string directory)
    {
        _fileSystem.CreateDirectory(directory);
        string content = string.Join(
            '|',
            _values.Select(entry =>
                entry.Key + "=" + entry.Value
            )
        );
        _fileSystem.WriteBytes(
            Path.Combine(directory, "dune_folder.txt"),
            LegacyDuneEncoding.Encode(content, true)
        );
    }
}
