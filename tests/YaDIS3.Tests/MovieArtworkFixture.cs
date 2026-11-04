using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Tests;

internal sealed class MovieArtworkFixture : IDisposable
{
    private readonly ExportFixture _fixture = new();
    private readonly WorkspaceRepository _repository = new();

    public string Root => _fixture.Root;
    public MediaCard Card { get; }
    public ImageImporter Importer { get; } = new(new CopyArtworkEncoder());

    public MovieArtworkFixture()
    {
        string file = Path.Combine(
            Root, "data", ExportFixture.MovieName, "data.xml"
        );
        XDocument xml = XDocument.Load(file);
        xml.Root!.Element("item")!.SetElementValue("path", Root);
        XElement info = xml.Root.Element("info")!;
        info.SetElementValue("id", "42");
        info.SetElementValue("source", "0");
        info.Add(ExpectedImages());
        xml.Save(file);
        File.WriteAllBytes(Path.Combine(Root, ExportFixture.MovieName), []);
        Card = _repository.Open(Root).Cards.Single(card => !card.IsSeries);
    }

    public static XElement ExpectedImages() => XElement.Load(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Metadata", "yadis2-images.xml")
    );

    public OnlineTreatmentMetadata Metadata(HttpClient http) =>
        new(http, null, "test-only-token", "fr", Importer);

    public Task SaveAsync() => _repository.SaveAsync(Card);

    public void Dispose()
    {
        Card.Dispose();
        _repository.Dispose();
        _fixture.Dispose();
    }
}
