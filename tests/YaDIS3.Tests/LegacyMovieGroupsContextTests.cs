using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class LegacyMovieGroupsContextTests
{
    private static XDocument Groups() => new(
        new XElement(
            "groups",
            new XElement(
                "sagas",
                new XElement(
                    "saga",
                    new XElement("name", "Avant"),
                    new XElement("sort_name", "Avant"),
                    new XElement("poster", "a.mkv"),
                    new XElement("fanart", "a.mkv"),
                    new XElement("movie", "a.mkv"),
                    new XElement("movie", "b.mkv")
                ),
                new XElement(
                    "saga",
                    new XElement("name", "Après"),
                    new XElement("sort_name", "Après"),
                    new XElement("poster", "c.mkv"),
                    new XElement("fanart", "c.mkv"),
                    new XElement("movie", "c.mkv")
                )
            ),
            new XElement("selections")
        )
    );

    [Fact]
    public void ToggleSaga_Deplacement_ActualiseImagesEtAppartenance()
    {
        XDocument groups = Groups();
        LegacyMovieGroups.ToggleSaga(groups, "a.mkv", "Après");
        XElement old = groups.Descendants("saga").First();
        Assert.Equal("b.mkv", old.Element("poster")?.Value);
        Assert.Equal("b.mkv", old.Element("fanart")?.Value);
        Assert.Equal(
            "Après",
            groups.Descendants("saga").Single(item =>
                item.Elements("movie").Any(m => m.Value == "a.mkv")
            ).Element("name")?.Value
        );
    }

    [Fact]
    public void ToggleSaga_DernierFilm_SupprimeSagaVide()
    {
        XDocument groups = Groups();
        LegacyMovieGroups.ToggleSaga(groups, "c.mkv", "Après");
        Assert.DoesNotContain(
            groups.Descendants("saga"),
            saga => saga.Element("name")?.Value == "Après"
        );
    }

    [Fact]
    public void CreateSaga_FilmDejaAffecte_DeplaceLeFilm()
    {
        XDocument groups = Groups();
        LegacyMovieGroups.CreateSaga(groups, "Nouvelle", "a.mkv");
        Assert.Equal(
            "Nouvelle",
            groups.Descendants("saga").Single(saga =>
                saga.Elements("movie").Any(movie =>
                    movie.Value == "a.mkv"
                )
            ).Element("name")?.Value
        );
    }

    [Fact]
    public void ToggleSelection_SelectionMultiple_BasculeLesMembres()
    {
        XDocument groups = Groups();
        LegacyMovieGroups.CreateSelection(
            groups, "Essais", ["a.mkv", "b.mkv"]
        );
        LegacyMovieGroups.ToggleSelection(
            groups, "Essais", ["a.mkv"]
        );
        Assert.Equal(
            "b.mkv",
            groups.Descendants("selection").Single()
                .Element("movie")?.Value
        );
    }

    [Fact]
    public void CreateSelection_CaractereInterdit_RejetteLeNom()
    {
        XDocument groups = Groups();
        Assert.Throws<ArgumentException>(() =>
            LegacyMovieGroups.CreateSelection(
                groups, "Essais/Tests", ["a.mkv"]
            )
        );
    }
}
