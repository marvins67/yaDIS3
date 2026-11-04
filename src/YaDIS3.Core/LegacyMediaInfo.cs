using System.Globalization;
using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>Conversions de ProcessForm.GetMediaInfo de yaDIS2.</summary>
public static class LegacyMediaInfo
{
    /// <summary>Bloc historique utilisé quand l'analyse échoue.</summary>
    public static XElement Empty() => XElement.Parse(
        "<mediainfo><format/><source/><video><codec/><resolution/><framerate/><aspectratio/></video><audio><codec/><channels/><languages/></audio><subtitles><languages/></subtitles></mediainfo>"
    );

    /// <summary>Adapte les pistes XML modernes aux valeurs historiques.</summary>
    public static XElement Convert(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        XElement[] tracks = document.Descendants()
            .Where(node => node.Name.LocalName == "track").ToArray();
        XElement? Track(string type) => tracks.FirstOrDefault(
            node => (string?)node.Attribute("type") == type);
        XElement? general = Track("General");
        XElement? video = Track("Video");
        XElement? audio = Track("Audio");
        if (video is null)
        {
            return Empty();
        }
        string format = Format(Value(general, "Format"));
        string height = Value(video, "Width") switch
        {
            "1920" => "1080",
            "1280" => "720",
            _ => Value(video, "Height"),
        };
        XElement result = Empty();
        result.SetElementValue("format", format);
        result.SetElementValue("source", format switch
        {
            "DVD" or "Blu-ray" or "Matroska" => format,
            "BDAV" => "Blu-ray",
            _ => "",
        });
        if (double.TryParse(Value(general, "Duration"),
            CultureInfo.InvariantCulture, out double seconds))
        {
            result.AddFirst(new XElement("duration", Math.Truncate(seconds * 1000)));
        }
        XElement v = result.Element("video")!;
        v.SetElementValue("codec", VideoCodec(Codec(video)));
        v.SetElementValue("resolution", height +
            (Value(video, "ScanType").StartsWith("P", StringComparison.OrdinalIgnoreCase) ? "p" : "i"));
        v.SetElementValue("framerate", Value(video, "FrameRate").TrimEnd('0').TrimEnd('.'));
        v.SetElementValue("aspectratio", Ratio(Value(video, "DisplayAspectRatio")));
        XElement a = result.Element("audio")!;
        a.SetElementValue("codec", AudioCodec(Codec(audio), Value(audio, "Format_Profile")));
        a.SetElementValue("channels", Channels(Value(audio, "Channels")));
        a.SetElementValue("languages", Languages(tracks, "Audio"));
        result.Element("subtitles")!.SetElementValue("languages", Languages(tracks, "Text"));
        return result;
    }

    private static string Value(XElement? track, string name) =>
        track?.Elements().FirstOrDefault(node => node.Name.LocalName == name)?.Value ?? "";

    private static string Codec(XElement? track) =>
        string.IsNullOrEmpty(Value(track, "Codec"))
            ? Value(track, "Format") : Value(track, "Codec");

    private static string Languages(IEnumerable<XElement> tracks, string type) =>
        string.Join("|", tracks.Where(t => (string?)t.Attribute("type") == type)
            .Select(t => Value(t, "Language"))).Trim('|');

    private static string Format(string value) => value.Contains("DVD", StringComparison.Ordinal)
        ? "DVD" : value.Contains("Blu-ray", StringComparison.Ordinal) ? "Blu-ray"
        : value switch { "MPEG-4" => "MP4", "OGG" => "Ogg", _ => value };

    private static string Ratio(string value) => (value.Length >= 3 ? value[..3] : value) switch
    {
        "1.2" => "1.25:1",
        "1.3" => "4:3",
        "1.5" => "1.50:1",
        "1.6" => "1.66:1",
        "1.7" => "16:9",
        "1.8" => "1.85:1",
        "2.3" => "2.35:1",
        "2.4" => "2.40:1",
        _ => value,
    };

    private static string Channels(string value) => value is "1" or "2" or "5"
        ? value + ".0" : double.TryParse(value, CultureInfo.InvariantCulture, out double count)
            ? (count - 1).ToString(CultureInfo.InvariantCulture) + ".1" : "";

    private static string VideoCodec(string value)
    {
        string code = value.ToUpperInvariant();
        if ("8BPS AVDJ AVRN RLE RP2A A2PR SMC ZYGO JPEG H263".Split(' ').Contains(code))
        {
            return "QuickTime";
        }
        if ("BLZ0 DIGI DXGM EM4A EPHV FMP4 FVFW HDX4 M4CC M4S2 MP4S MP4V MVXM RMP4 SEDG SMP4 UMP4 WV1F GEOX".Split(' ').Contains(code))
        {
            return "MPEG-4";
        }
        if (code.Contains("AVC", StringComparison.Ordinal))
        {
            return "AVC";
        }
        if (code is "H264" or "X264" or "VSSH")
        {
            return "H.264";
        }
        if (code.Contains("3IV", StringComparison.Ordinal))
        {
            return "3IVX";
        }
        if (code.Contains("VC1", StringComparison.Ordinal))
        {
            return "VC-1";
        }
        if (code.Contains("DIV", StringComparison.Ordinal) || code == "DX50")
        {
            return "DivX";
        }
        if (code.Contains("XVI", StringComparison.Ordinal))
        {
            return "XviD";
        }
        if (code.Contains("MPEG-1", StringComparison.Ordinal) || code.Contains("MPEG1", StringComparison.Ordinal) || code.Contains("PIM1", StringComparison.Ordinal))
        {
            return "MPEG-1";
        }
        if (code.Contains("MPEG-2", StringComparison.Ordinal) || code.Contains("MPEG2", StringComparison.Ordinal) || code.Contains("MMES", StringComparison.Ordinal))
        {
            return "MPEG-2";
        }
        if (code.Contains("WMV", StringComparison.Ordinal) || code == "WVP2")
        {
            return "WMV";
        }
        if (code is "RV10" or "RV13" or "RV20" or "RV30" or "RV40")
        {
            return "RealVideo";
        }
        return code == "THEORA" ? "Ogg" : code;
    }

    private static string AudioCodec(string value, string profile)
    {
        string code = value.ToUpperInvariant();
        // Le XML récent expose parfois Format à la place de Codec.
        code = code switch
        {
            "AC-3" => "AC3",
            "E-AC-3" or "TRUEHD" => "AC3+",
            "MPEG AUDIO" when profile.Contains('3') => "MP3",
            _ => code,
        };
        if (code == "DTS-HD" && profile.StartsWith("HRA", StringComparison.Ordinal))
        {
            return "DTS-HD HRA";
        }
        if (code == "DTS-HD" && profile.StartsWith("MA", StringComparison.Ordinal))
        {
            return "DTS-HD MA";
        }
        if (code.Contains("AAC", StringComparison.Ordinal))
        {
            return "AAC";
        }
        if (code == "A_TRUEHD")
        {
            return "AC3+";
        }
        if (code.Contains("OGG", StringComparison.Ordinal) || code.Contains("VORBIS", StringComparison.Ordinal))
        {
            return "Vorbis";
        }
        if (code is "160" or "161" or "162")
        {
            return "WMA";
        }
        if (code.Contains("PCM", StringComparison.Ordinal))
        {
            return "PCM";
        }
        return code.Contains("MPA", StringComparison.Ordinal) || code == "55" ? "MP3" : code;
    }
}
