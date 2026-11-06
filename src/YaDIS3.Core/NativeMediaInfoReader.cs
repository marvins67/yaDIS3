using System.Globalization;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>Analyse les vidéos avec MediaInfoLib comme yaDIS2 sous Windows.</summary>
internal static class NativeMediaInfoReader
{
    private const string Library = "libmediainfo";
    private const int General = 0;
    private const int Video = 1;
    private const int Audio = 2;
    private const int Text = 3;

    /// <summary>Construit le XML technique historique depuis MediaInfoLib.</summary>
    internal static XElement Read(string file)
    {
        IntPtr handle = CreateHandle();
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Initialisation MediaInfo impossible.");
        }

        try
        {
            if (OpenFile(handle, Path.GetFullPath(file)) == 0)
            {
                throw new InvalidDataException("MediaInfo ne peut pas ouvrir : " + file);
            }

            return ReadTracks(handle);
        }
        finally
        {
            CloseFile(handle);
            DeleteHandle(handle);
        }
    }

    private static XElement ReadTracks(IntPtr handle)
    {
        XElement general = Track(handle, General, 0,
            "Format", "Duration");
        XElement video = Track(handle, Video, 0,
            "Codec", "Format", "Width", "Height",
            "ScanType", "DisplayAspectRatio", "FrameRate");
        if (GetStreamCount(handle, (nuint)Video, -1) == 0)
        {
            throw new InvalidDataException("Aucune piste vidéo détectée par MediaInfo.");
        }

        string duration = general.Element("Duration")?.Value ?? "";
        if (double.TryParse(duration, CultureInfo.InvariantCulture,
            out double milliseconds))
        {
            general.SetElementValue("Duration",
                (milliseconds / 1000).ToString(CultureInfo.InvariantCulture));
        }

        XElement root = new("MediaInfo",
            new XElement("media", general, video));
        for (int index = 0; index < (int)GetStreamCount(handle, (nuint)Audio, -1); index++)
        {
            root.Element("media")!.Add(Track(handle, Audio, index,
                "Codec", "Format", "Format_Profile", "Channels", "Language"));
        }
        for (int index = 0; index < (int)GetStreamCount(handle, (nuint)Text, -1); index++)
        {
            root.Element("media")!.Add(Track(handle, Text, index, "Language"));
        }
        return LegacyMediaInfo.Convert(new XDocument(root));
    }

    private static XElement Track(
        IntPtr handle, int kind, int index, params string[] fields
    )
    {
        string type = kind switch
        {
            General => "General",
            Video => "Video",
            Audio => "Audio",
            _ => "Text",
        };
        return new XElement("track",
            new XAttribute("type", type),
            fields.Select(field => new XElement(field,
                Marshal.PtrToStringUni(GetField(
                    handle, (nuint)kind, (nuint)index, field, 1, 0
                )) ?? "")));
    }

    [DllImport(Library, EntryPoint = "MediaInfo_New")]
    private static extern IntPtr CreateHandle();

    [DllImport(Library, EntryPoint = "MediaInfo_Delete")]
    private static extern void DeleteHandle(IntPtr handle);

    [DllImport(Library, EntryPoint = "MediaInfo_Open", CharSet = CharSet.Unicode)]
    private static extern nuint OpenFile(
        IntPtr handle, [MarshalAs(UnmanagedType.LPWStr)] string path);

    [DllImport(Library, EntryPoint = "MediaInfo_Close")]
    private static extern void CloseFile(IntPtr handle);

    [DllImport(Library, EntryPoint = "MediaInfo_Get", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetField(
        IntPtr handle, nuint kind, nuint index,
        [MarshalAs(UnmanagedType.LPWStr)] string field,
        nuint information, nuint search);

    [DllImport(Library, EntryPoint = "MediaInfo_Count_Get")]
    private static extern nuint GetStreamCount(
        IntPtr handle, nuint kind, nint index);
}
