using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;

namespace YaDIS3.Desktop.Tests;

internal sealed class ArtworkModelFixture : IDisposable
{
    private readonly WorkspaceRepository _repository = new();

    public string Root { get; } = Path.Combine(
        Path.GetTempPath(), "yadis3-artwork-" + Guid.NewGuid().ToString("N")
    );
    public MainWindowViewModel Model { get; }
    public MediaCard Card { get; }

    public ArtworkModelFixture(ArtworkHandler handler)
    {
        string folder = Path.Combine(Root, "data", "Film");
        Directory.CreateDirectory(folder);
        new XDocument(new XElement("data",
            new XElement("item",
                new XElement("filename", "Film"),
                new XElement("type", "0"),
                new XElement("status", "1")),
            new XElement("info",
                new XElement("title", "Film"),
                new XElement("images",
                    Image("poster", "poster.jpg"),
                    Image("fanart", "first.jpg"),
                    Image("fanart", "second.jpg")))))
            .Save(Path.Combine(folder, "data.xml"));
        Card = LoadCard();
        Model = new(
            _repository,
            new ImageImporter(new ArtworkEncoder()),
            new RecordingHttpClientFactory(handler),
            new DuneConfigurationStore(userConfigurationPath:
                Path.Combine(Root, "user.config")),
            new DuneConfigurationService(),
            new SourceFolderScanner(),
            NullLogger<MainWindowViewModel>.Instance,
            NullLogger<ConfigurationViewModel>.Instance
        );
        Model.SelectedCard = Card;
    }

    private MediaCard LoadCard() =>
        _repository.Open(Root).Cards.Single();

    private static XElement Image(string kind, string name) =>
        new("image", new XAttribute("type", kind),
            new XAttribute("url", "https://image.tmdb.org/t/p/original/" + name));

    public void Dispose()
    {
        Model.Dispose();
        Card.Dispose();
        _repository.Dispose();
        Directory.Delete(Root, true);
    }
}
