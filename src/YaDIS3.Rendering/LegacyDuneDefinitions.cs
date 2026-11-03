namespace YaDIS3.Rendering;

/// <summary>
/// Conserve les définitions de navigation écrites par yaDIS2.
/// </summary>
internal sealed class LegacyDuneDefinitions(
    DuneTemplate template
)
{
    private string Attribute(string path, string name) =>
        template.Node(path).Attr(name);

    private string Text(string path) =>
        template.Node(path).Value;

    // ProcessForm.cs:3890, contrat yaDIS2 conservé.
    public string Movie(string playbackUrl)
    {
        return "background_order=before_all"
            + "|background_path=background.aai"
            + "|use_icon_view=yes"
            + "|icon_path=icon.aai"
            + "|icon_sel_path=icon.aai"
            + "|icon_scale_factor="
            + Attribute("movie/iconview/icon", "scale")
            + "|icon_sel_scale_factor=1"
            + "|icon_valign=center"
            + "|paint_captions=no"
            + "|paint_help_line=no"
            + "|paint_path_box=no"
            + "|paint_content_box_background=no"
            + "|paint_scrollbar=no"
            + "|paint_icon_selection_box=no"
            + "|paint_icons=no"
            + "|system_files=*.aai,*.jpg"
            + "|item.0.caption=Play"
            + "|item.0.media_url="
            + playbackUrl;
    }

    // ProcessForm.cs:4932, contrat yaDIS2 conservé.
    public string Series()
    {
        return "background_order=before_all"
            + "|background_path=background.aai"
            + "|use_icon_view=yes"
            + "|icon_path=icon.aai"
            + "|icon_sel_path=icon.aai"
            + "|icon_scale_factor="
            + Attribute("tv/iconview/icon", "scale")
            + "|icon_sel_scale_factor=1|icon_valign=center|num_cols="
            + Text("tv/synopsis/season/seasonicon/area/columns")
            + "|num_rows="
            + Text("tv/synopsis/season/seasonicon/area/rows")
            + "|content_box_x="
            + Attribute(
                "tv/synopsis/season/seasonicon/area",
                "x"
            )
            + "|content_box_y="
            + Attribute(
                "tv/synopsis/season/seasonicon/area",
                "y"
            )
            + "|content_box_width="
            + Attribute(
                "tv/synopsis/season/seasonicon/area",
                "width"
            )
            + "|content_box_height="
            + Attribute(
                "tv/synopsis/season/seasonicon/area",
                "height"
            )
            + "|content_box_padding_left=0"
            + "|content_box_padding_right=0"
            + "|content_box_padding_top=0"
            + "|content_box_padding_bottom=0"
            + "|icon_selection_box_width="
            + Attribute(
                "tv/synopsis/season/seasonicon/icon",
                "width"
            )
            + "|icon_selection_box_height="
            + Attribute(
                "tv/synopsis/season/seasonicon/icon",
                "height"
            )
            + "|paint_captions=no"
            + "|paint_help_line=no"
            + "|paint_path_box=no"
            + "|paint_content_box_background=no"
            + "|paint_scrollbar=no"
            + "|system_files=*.aai,*.jpg";
    }

    // ProcessForm.cs:4975, contrat yaDIS2 conservé.
    public string Season()
    {
        return "background_order=before_all"
            + "|background_path=../background2.aai"
            + "|use_icon_view=yes"
            + "|icon_path=icon.aai"
            + "|icon_sel_path=icon.aai"
            + "|icon_scale_factor="
            + Attribute(
                "tv/synopsis/season/seasonicon/icon",
                "scale"
            )
            + "|icon_sel_scale_factor=1|icon_valign=center|num_cols=1|num_rows=1|content_box_x="
            + Attribute(
                "tv/synopsis/episode/episodeicon/area",
                "x"
            )
            + "|content_box_y="
            + Attribute(
                "tv/synopsis/episode/episodeicon/area",
                "y"
            )
            + "|content_box_width="
            + Attribute(
                "tv/synopsis/episode/episodeicon/area",
                "width"
            )
            + "|content_box_height="
            + Attribute(
                "tv/synopsis/episode/episodeicon/area",
                "height"
            )
            + "|content_box_padding_left=0"
            + "|content_box_padding_right=0"
            + "|content_box_padding_top=0"
            + "|content_box_padding_bottom=0"
            + "|paint_captions=no"
            + "|paint_help_line=no"
            + "|paint_path_box=no"
            + "|paint_content_box_background=no"
            + "|paint_scrollbar=no"
            + "|paint_icon_selection_box=no"
            + "|system_files=*.aai,*.jpg";
    }

    // ProcessForm.cs:5732, contrat yaDIS2 conservé.
    public static string Episode(string playbackUrl)
    {
        return "icon_path=icon.aai|icon_valign=center|media_url="
            + playbackUrl;
    }

    // ProcessForm.cs:1710, contrat yaDIS2 conservé.
    public string SeriesListing()
    {
        return "background_order=before_all"
            + "|background_path=background.aai"
            + "|use_icon_view=yes"
            + "|icon_path=icon.aai"
            + "|icon_valign=center"
            + "|num_cols="
            + Text("tv/iconview/area/columns")
            + "|num_rows="
            + Text("tv/iconview/area/rows")
            + "|content_box_x="
            + Attribute("tv/iconview/area", "x")
            + "|content_box_y="
            + Attribute("tv/iconview/area", "y")
            + "|content_box_width="
            + Attribute("tv/iconview/area", "width")
            + "|content_box_height="
            + Attribute("tv/iconview/area", "height")
            + "|content_box_padding_left=0"
            + "|content_box_padding_right=0"
            + "|content_box_padding_top=0"
            + "|content_box_padding_bottom=0"
            + "|icon_selection_box_width="
            + Attribute("tv/iconview/icon", "width")
            + "|icon_selection_box_height="
            + Attribute("tv/iconview/icon", "height")
            + "|paint_captions=no|paint_help_line=no|paint_path_box=no|paint_content_box_background=no|system_files=*.aai,*.jpg";
    }

    // ProcessForm.cs:1499, contrat yaDIS2 conservé.
    public string MovieListing()
    {
        return "background_order=before_all"
            + "|background_path=../00/background.aai"
            + "|use_icon_view=yes"
            + "|icon_path=icon.aai"
            + "|icon_valign=center"
            + "|num_cols="
            + Text("movie/iconview/area/columns")
            + "|num_rows="
            + Text("movie/iconview/area/rows")
            + "|content_box_x="
            + Attribute("movie/iconview/area", "x")
            + "|content_box_y="
            + Attribute("movie/iconview/area", "y")
            + "|content_box_width="
            + Attribute("movie/iconview/area", "width")
            + "|content_box_height="
            + Attribute("movie/iconview/area", "height")
            + "|content_box_padding_left=0"
            + "|content_box_padding_right=0"
            + "|content_box_padding_top=0"
            + "|content_box_padding_bottom=0"
            + "|icon_selection_box_width="
            + Attribute("movie/iconview/icon", "width")
            + "|icon_selection_box_height="
            + Attribute("movie/iconview/icon", "height")
            + "|paint_captions=no|paint_help_line=no|paint_path_box=no|paint_content_box_background=no|system_files=*.aai,*.jpg";
    }

    // ProcessForm.cs:1394, contrat yaDIS2 conservé.
    public string MovieMenu()
    {
        return "background_order=before_all"
            + "|background_path=background.aai"
            + "|use_icon_view=yes"
            + "|icon_path=icon.aai"
            + "|icon_valign=center"
            + "|num_cols=1"
            + "|num_rows=1"
            + "|content_box_x="
            + Attribute("menu/index/area", "x")
            + "|content_box_y="
            + Attribute("menu/index/area", "y")
            + "|content_box_width="
            + Attribute("menu/index/area", "width")
            + "|content_box_height="
            + Attribute("menu/index/area", "height")
            + "|content_box_padding_left=0"
            + "|content_box_padding_right=0"
            + "|content_box_padding_top=0"
            + "|content_box_padding_bottom=0"
            + "|paint_captions=no"
            + "|paint_help_line=no"
            + "|paint_path_box=no"
            + "|paint_icon_selection_box=no"
            + "|paint_content_box_background=no"
            + "|paint_scrollbar=no"
            + "|system_files=*.aai,*.jpg,98,99";
    }

    // ProcessForm.cs:700, contrat yaDIS2 conservé.
    public string Root()
    {
        return "background_order=before_all"
            + "|background_path=background.aai"
            + "|use_icon_view=yes"
            + "|icon_path=icon.aai"
            + "|icon_valign=center"
            + "|num_cols=1"
            + "|num_rows=1"
            + "|content_box_x="
            + Attribute("menu/main/area", "x")
            + "|content_box_y="
            + Attribute("menu/main/area", "y")
            + "|content_box_width="
            + Attribute("menu/main/area", "width")
            + "|content_box_height="
            + Attribute("menu/main/area", "height")
            + "|content_box_padding_left=0"
            + "|content_box_padding_right=0"
            + "|content_box_padding_top=0"
            + "|content_box_padding_bottom=0"
            + "|paint_captions=no"
            + "|paint_help_line=no"
            + "|paint_path_box=no"
            + "|paint_icon_selection_box=no"
            + "|paint_content_box_background=no"
            + "|paint_scrollbar=no"
            + "|storage_caption=yaDIS"
            + "|system_files=*.aai,*.jpg"
            + "|all_children.paint_icon_selection_box="
            + "no|all_children.paint_scrollbar=no";
    }

    // ProcessForm.CreateIndexItem : conserver notamment le chemin /00.
    public string IndexListing(string ancestor)
    {
        return "background_order=before_all|background_path="
            + ancestor
            + "/00/background.aai|use_icon_view=yes|icon_path=icon.aai|icon_sel_path=icon_sel.aai|icon_valign=center|num_cols="
            + Text("movie/iconview/area/columns")
            + "|num_rows="
            + Text("movie/iconview/area/rows")
            + "|content_box_x="
            + Attribute("movie/iconview/area", "x")
            + "|content_box_y="
            + Attribute("movie/iconview/area", "y")
            + "|content_box_width="
            + Attribute("movie/iconview/area", "width")
            + "|content_box_height="
            + Attribute("movie/iconview/area", "height")
            + "|content_box_padding_left=0"
            + "|content_box_padding_right=0"
            + "|content_box_padding_top=0"
            + "|content_box_padding_bottom=0"
            + "|icon_selection_box_width="
            + Attribute("movie/iconview/icon", "width")
            + "|icon_selection_box_height="
            + Attribute("movie/iconview/icon", "height")
            + "|paint_captions=no|paint_help_line=no|paint_path_box=no|paint_content_box_background=no|system_files=*.aai,*.jpg";
    }
}
