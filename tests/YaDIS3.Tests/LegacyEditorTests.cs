using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class LegacyEditorTests
{
    private static readonly string[] s_countries =
    [
        "Japon",
        "France",
    ];
    private static readonly string[] s_actors =
    [
        "Chieko Baishō",
        "Takuya Kimura",
    ];
    private static readonly string[] s_genres =
    [
        "Animation",
        "Aventure",
    ];

    [Fact]
    public async Task FilmFields_EditAndSave_PreservesLegacyXml()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        MediaCard card = repository
            .Open(fixture.Root)
            .Cards.Single(item => !item.IsSeries);
        card.OriginalTitle = "Le Château ambulant";
        card.SortTitle = "Château ambulant";
        card.Runtime = "119";
        card.Certification = "Tous publics";
        card.Tagline = "Une aventure extraordinaire";
        card.Actors = "Chieko Baishō, Takuya Kimura";
        card.Directors = "Hayao Miyazaki";
        card.Countries = "Japon, France";
        card.Studios = "Studio Ghibli";
        card.Genres = "Animation, Aventure";
        await repository.SaveAsync(card);
        XElement info = XDocument
            .Load(card.FilePath)
            .Root!.Element("info")!;
        Assert.Equal("119", info.Element("runtime")!.Value);
        Assert.Equal(
            "Le Château ambulant",
            info.Element("original_title")!.Value
        );
        Assert.Equal(
            "Château ambulant",
            info.Element("sort_title")!.Value
        );
        Assert.Equal(
            "Tous publics",
            info.Element("certification")!.Value
        );
        Assert.Equal(
            "Une aventure extraordinaire",
            info.Element("tagline")!.Value
        );
        Assert.Equal(
            s_countries,
            info.Element("countries")!
                .Elements("country")
                .Select(node => node.Value)
        );
        Assert.Equal(
            "Studio Ghibli",
            info.Element("studios")!.Element("studio")!.Value
        );
        Assert.Equal(
            "Hayao Miyazaki",
            info.Element("directors")!.Element("director")!.Value
        );
        Assert.Equal(
            s_actors,
            info.Element("actors")!
                .Elements("actor")
                .Select(node => node.Value)
        );
        Assert.Equal(
            s_genres,
            info.Element("genres")!
                .Elements("genre")
                .Select(node => node.Value)
        );
    }

    [Fact]
    public void TechnicalFields_Edit_UsesExistingMediaInfoOnly()
    {
        using ExportFixture fixture = new();
        string file = Path.Combine(
            fixture.Root,
            "data",
            ExportFixture.MovieName,
            "data.xml"
        );
        XDocument document = XDocument.Load(file);
        document
            .Root!.Element("mediainfo")!
            .Add(
                new XElement("source", "DVD"),
                new XElement(
                    "video",
                    new XElement("codec", "MPEG-2"),
                    new XElement("resolution", "576p"),
                    new XElement("framerate", "25"),
                    new XElement("aspectratio", "16:9")
                ),
                new XElement(
                    "audio",
                    new XElement("codec", "AC3"),
                    new XElement("channels", "5.1"),
                    new XElement("languages", "FR|EN")
                ),
                new XElement(
                    "subtitles",
                    new XElement("languages", "FR")
                )
            );
        document.Save(file);
        using WorkspaceRepository repository = new();
        MediaCard card = repository
            .Open(fixture.Root)
            .Cards.Single(item => !item.IsSeries);
        Assert.Equal("FR, EN", card.AudioLanguages);
        card.VideoCodec = "H.264";
        card.Resolution = "1080p";
        card.FrameRate = "23.976";
        card.AspectRatio = "2.35:1";
        card.ContainerFormat = "Matroska";
        card.CaseType = "Blu-ray";
        card.AudioCodec = "DTS";
        card.AudioChannels = "7.1";
        card.AudioLanguages = "fr, ja";
        card.SubtitleLanguages = "en, fr";
        XElement media = card.Snapshot()
            .Document.Root!.Element("mediainfo")!;
        Assert.Equal(
            "H.264",
            media.Element("video")!.Element("codec")!.Value
        );
        Assert.Equal("1080p", card.Resolution);
        Assert.Equal("23.976", card.FrameRate);
        Assert.Equal("2.35:1", card.AspectRatio);
        Assert.Equal("Matroska", card.ContainerFormat);
        Assert.Equal("Blu-ray", card.CaseType);
        Assert.Equal("DTS", card.AudioCodec);
        Assert.Equal("7.1", card.AudioChannels);
        Assert.Equal(
            "FR|JA",
            media.Element("audio")!.Element("languages")!.Value
        );
        Assert.Equal(
            "EN|FR",
            media
                .Element("subtitles")!
                .Element("languages")!
                .Value
        );
        MediaCard series = repository
            .Open(fixture.Root)
            .Cards.Single(item => item.IsSeries);
        series.VideoCodec = "H.264";
        Assert.Equal("", series.VideoCodec);
        Assert.False(series.IsDirty);
    }
}
