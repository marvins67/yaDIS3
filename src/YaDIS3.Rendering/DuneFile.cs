using System.Globalization;
using System.Text;
using System.Xml.Linq;
namespace YaDIS3.Rendering;

internal sealed class DuneFile
{
    private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
    public DuneFile Set(string key, object? value)
    { values[key] = DunePaths.CleanValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""); return this; }
    public DuneFile Screen(string background)
    {
        Set("background_order", "before_all").Set("background_path", background).Set("use_icon_view", "yes");
        foreach (var name in new[] { "paint_captions", "paint_help_line", "paint_path_box", "paint_content_box_background", "paint_scrollbar", "paint_icon_selection_box" }) Set(name, "no");
        return Set("system_files", "*").Set("content_box_padding_left", 0).Set("content_box_padding_right", 0).Set("content_box_padding_top", 0).Set("content_box_padding_bottom", 0);
    }
    public DuneFile Grid(XElement area, int columns, int rows)
    {
        Set("num_cols", columns).Set("num_rows", rows);
        foreach (var name in new[] { "x", "y", "width", "height" }) Set("content_box_" + name, area.Int(name)); return this;
    }
    public DuneFile Icon(string path, string scale = "1") => Set("icon_path", path).Set("icon_sel_path", path).Set("icon_valign", "center").Set("icon_scale_factor", scale).Set("icon_sel_scale_factor", "1");
    public DuneFile Item(int index, string caption, string url, string? icon = null, string? selected = null, string scale = "1", bool play = false)
    {
        var prefix = $"item.{index}."; Set(prefix + "caption", caption.Replace('\r', ' ').Replace('\n', ' ')); Set(prefix + "media_url", url).Set(prefix + "media_action", play ? "play" : "browse");
        if (icon is not null) Set(prefix + "icon_path", icon).Set(prefix + "icon_sel_path", selected ?? icon).Set(prefix + "icon_scale_factor", scale).Set(prefix + "icon_sel_scale_factor", "1").Set(prefix + "icon_valign", "center");
        return this;
    }
    public void Save(string directory)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "dune_folder.txt"), "*** NE PAS SUPPRIMER OU MODIFIER CETTE LIGNE - DO NOT DELETE OR MODIFY THIS LINE ***\r\n" + string.Join("\r\n", values.Select(kv => kv.Key + "=" + kv.Value)) + "\r\n", new UTF8Encoding(true));
    }
}
