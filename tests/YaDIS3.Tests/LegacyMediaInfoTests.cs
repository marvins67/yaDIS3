using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class LegacyMediaInfoTests
{
    [Theory]
    [InlineData("1920", "800", "6", "1080p", "5.1")]
    [InlineData("1280", "544", "2", "720p", "2.0")]
    [InlineData("720", "576", "8", "576p", "7.1")]
    public void Convert_PistesVideo_ReprendLesValeursYaDIS2(
        string width, string height, string channels,
        string resolution, string layout
    )
    {
        XDocument input = XDocument.Parse($"""
            <MediaInfo><media>
              <track type="General"><Format>Matroska</Format><Duration>7200.1239</Duration></track>
              <track type="Video"><Format>AVC</Format><Width>{width}</Width><Height>{height}</Height><ScanType>Progressive</ScanType><DisplayAspectRatio>1.778</DisplayAspectRatio><FrameRate>23.976000</FrameRate></track>
              <track type="Audio"><Format>AAC LC</Format><Channels>{channels}</Channels><Language>fr</Language></track>
            </media></MediaInfo>
            """);
        XElement result = LegacyMediaInfo.Convert(input);
        Assert.Equal(resolution, result.Element("video")!.Element("resolution")!.Value);
        Assert.Equal(layout, result.Element("audio")!.Element("channels")!.Value);
        Assert.Equal("Matroska", result.Element("source")!.Value);
        Assert.Equal("16:9", result.Element("video")!.Element("aspectratio")!.Value);
        Assert.Equal("23.976", result.Element("video")!.Element("framerate")!.Value);
        Assert.Equal("7200123", result.Element("duration")!.Value);
        Assert.Equal("AAC", result.Element("audio")!.Element("codec")!.Value);
    }

    [Theory]
    [InlineData("X264", "A_TRUEHD", "MPEG-4", "2.400", "H.264", "AC3+", "MP4", "", "2.40:1")]
    [InlineData("AVC", "AC-3", "Matroska", "1.778", "AVC", "AC3", "Matroska", "Matroska", "16:9")]
    [InlineData("AVC", "E-AC-3", "Matroska", "1.778", "AVC", "AC3+", "Matroska", "Matroska", "16:9")]
    [InlineData("DX50", "VORBIS", "OGG", "1.333", "DivX", "Vorbis", "Ogg", "", "4:3")]
    [InlineData("VC1", "PCM", "BDAV", "1.850", "VC-1", "PCM", "BDAV", "Blu-ray", "1.85:1")]
    [InlineData("MPEG2", "55", "DVD Video", "1.666", "MPEG-2", "MP3", "DVD", "DVD", "1.66:1")]
    public void Convert_FormatsEtCodecs_RespecteLesConversionsHistoriques(
        string video, string audio, string format, string ratio,
        string expectedVideo, string expectedAudio, string expectedFormat,
        string expectedSource, string expectedRatio
    )
    {
        XDocument input = XDocument.Parse($"""
            <MediaInfo><media>
              <track type="General"><Format>{format}</Format></track>
              <track type="Video"><Codec>{video}</Codec><Width>720</Width><Height>576</Height><ScanType>Interlaced</ScanType><DisplayAspectRatio>{ratio}</DisplayAspectRatio></track>
              <track type="Audio"><Codec>{audio}</Codec><Channels>6</Channels></track>
            </media></MediaInfo>
            """);
        XElement result = LegacyMediaInfo.Convert(input);
        Assert.Equal(expectedVideo, result.Element("video")!.Element("codec")!.Value);
        Assert.Equal(expectedAudio, result.Element("audio")!.Element("codec")!.Value);
        Assert.Equal(expectedFormat, result.Element("format")!.Value);
        Assert.Equal(expectedSource, result.Element("source")!.Value);
        Assert.Equal(expectedRatio, result.Element("video")!.Element("aspectratio")!.Value);
        Assert.Equal("576i", result.Element("video")!.Element("resolution")!.Value);
    }

    [Fact]
    public void NativeMediaInfoReader_FichierInexistant_BibliothequeDisponibleSousWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Throws<InvalidDataException>(() =>
            NativeMediaInfoReader.Read(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mkv")
            ));
    }

    [Fact]
    public async Task ReadAsync_ExecutableAbsent_SignaleLEchecAuLieuDeMarquerLeFilmTraite()
    {
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new MediaInfoReader("absent-" + Guid.NewGuid())
                .ReadAsync("film.mkv", CancellationToken.None)
        );
        Assert.Contains("MediaInfo CLI", exception.Message, StringComparison.Ordinal);
    }
}
