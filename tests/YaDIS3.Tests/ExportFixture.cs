using System.Xml.Linq;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

internal sealed class ExportFixture : IDisposable
{
    public string Root { get; } =
        Path.Combine(
            Path.GetTempPath(),
            "yadis3-characterization-"
                + Guid.NewGuid().ToString("N")
        );
    public string Destination => Path.Combine(Root, "export");
    public const string MovieName = "L’été d’Adèle [1080p].mkv";
    public const string SeriesName = "SPY×FAMILY";

    public ExportFixture()
    {
        AddCard(MovieName, false);
        AddCard(SeriesName, true);
    }

    private void AddCard(string name, bool series)
    {
        string directory = Path.Combine(Root, "data", name);
        Directory.CreateDirectory(directory);
        XElement info = new(
            "info",
            new XElement(
                "title",
                series ? SeriesName : "L’été d’Adèle"
            ),
            new XElement(
                "sort_title",
                series ? SeriesName : "Été d’Adèle"
            ),
            new XElement("year", "2023"),
            new XElement("rating", "8.7"),
            new XElement("runtime", "151"),
            new XElement("genre", "Drame"),
            new XElement("actor", "Adèle Exarchopoulos"),
            new XElement("director", "Abdellatif Kechiche"),
            new XElement("country", "France")
        );
        if (series)
        {
            info.Add(
                new XElement(
                    "episode",
                    new XAttribute("key", "01_000_001"),
                    new XAttribute("season", "0"),
                    new XAttribute("episode", "1"),
                    new XElement(
                        "file",
                        "/media/Series/SPY×FAMILY/Bonus [FR].mkv"
                    ),
                    new XElement("title", "L'épisode spécial")
                )
            );
        }

        new XDocument(
            new XElement(
                "data",
                new XElement(
                    "item",
                    new XElement("filename", name),
                    new XElement(
                        "path",
                        series ? "/media/Series" : "/media/Films"
                    ),
                    new XElement(
                        "dunepath",
                        series
                            ? "smb://nas/Series"
                            : "smb://nas/Films"
                    ),
                    new XElement("folder", "no"),
                    new XElement("type", series ? "1" : "0"),
                    new XElement("status", "1"),
                    new XElement("date", "2026-11-01")
                ),
                info,
                new XElement(
                    "mediainfo",
                    new XElement("format", "BDAV")
                )
            )
        ).Save(Path.Combine(directory, "data.xml"));
    }

    public MediaContext[] Cards()
    {
        using WorkspaceRepository repository = new();
        Workspace workspace = repository.Open(Root);
        return workspace
            .Cards.Select(card => new MediaContext(card))
            .ToArray();
    }

    public void ProcessIndexes(string template = "Origins")
    {
        LegacyMovieIndexStore store = new(
            Template(template),
            new RenderOptions()
        );
        foreach (
            MediaContext movie in Cards()
                .Where(card => !card.IsSeries)
        )
        {
            store.Update(movie, Root);
        }
    }

    public static DuneTemplate Template(string name = "Origins")
    {
        string assets = Path.Combine(
            AppContext.BaseDirectory,
            "Assets"
        );
        return DuneTemplate.Load(
            Path.Combine(assets, "Templates", name),
            Path.Combine(assets, "Images")
        );
    }

    public static DuneExporter Exporter(string name = "Origins")
    {
        return new DuneExporter(
            Template(name),
            new RenderOptions(),
            []
        );
    }

    public void Dispose()
    {
        Directory.Delete(Root, true);
    }
}
