namespace YaDIS3.Desktop.ViewModels;

/// <summary>Choix des listes Média repris de MainForm yaDIS2.</summary>
public sealed partial class MainWindowViewModel
{
    /// <summary>Valeurs historiques de VideoCodec.</summary>
    public IReadOnlyList<string> VideoCodecChoices { get; } =
    [
        "3IVX",
        "AVC",
        "DivX",
        "H.264",
        "MPEG-1",
        "MPEG-2",
        "MPEG-4",
        "Ogg",
        "QuickTime",
        "RealVideo",
        "VC-1",
        "WMV",
        "XviD",
    ];

    /// <summary>Valeurs historiques de Resolution.</summary>
    public IReadOnlyList<string> ResolutionChoices { get; } =
    ["480i", "480p", "576i", "576p", "720p", "1080i", "1080p"];

    /// <summary>Valeurs historiques de FrameRate.</summary>
    public IReadOnlyList<string> FrameRateChoices { get; } =
    ["23.976", "24", "25", "29.97", "30", "50", "59.94", "60"];

    /// <summary>Valeurs historiques de AspectRatio.</summary>
    public IReadOnlyList<string> AspectRatioChoices { get; } =
    [
        "4:3",
        "16:9",
        "1.25:1",
        "1.33:1",
        "1.66:1",
        "1.78:1",
        "1.85:1",
        "2.20:1",
        "2.35:1",
        "2.39:1",
        "2.40:1",
        "2.55:1",
        "2.76:1",
    ];

    /// <summary>Valeurs historiques de ContainerFormat.</summary>
    public IReadOnlyList<string> ContainerFormatChoices { get; } =
    [
        "AVI",
        "Blu-ray",
        "BDAV",
        "DVD",
        "Matroska",
        "MP4",
        "MPEG-PS",
        "MPEG-TS",
        "Ogg",
        "QuickTime",
        "Windows Media",
    ];

    /// <summary>Valeurs historiques de CaseType.</summary>
    public IReadOnlyList<string> CaseTypeChoices { get; } =
    [
        "Blu-ray",
        "Blu-ray 3D",
        "DivX",
        "DVD",
        "HD-DVD",
        "HDTV",
        "Matroska",
    ];

    /// <summary>Valeurs historiques de AudioCodec.</summary>
    public IReadOnlyList<string> AudioCodecChoices { get; } =
    [
        "AAC",
        "AC3",
        "AC3+",
        "DTS",
        "DTS-HD",
        "DTS-HD HRA",
        "DTS-HD MA",
        "FLAC",
        "MP3",
        "PCM",
        "TrueHD",
        "Vorbis",
        "WMA",
    ];

    /// <summary>Valeurs historiques de AudioChannels.</summary>
    public IReadOnlyList<string> AudioChannelsChoices { get; } =
    ["1.0", "2.0", "2.1", "5.0", "5.1", "6.1", "7.1"];
}
