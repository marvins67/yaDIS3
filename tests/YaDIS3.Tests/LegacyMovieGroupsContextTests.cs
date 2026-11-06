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

    [Fact]
    public void EditSaga_ModificationDesImagesEtOrdre_ConserveLesMembres()
    {
        XDocument groups = Groups();
        LegacyMovieGroups.EditSaga(
            groups, "Avant", "Nouvelle", "Nouv", "b.mkv",
            "a.mkv", ["b.mkv", "a.mkv"]
        );
        XElement saga = groups.Descendants("saga").Single(x =>
            x.Element("name")?.Value == "Nouvelle"
        );
        Assert.Equal("Nouv", saga.Element("sort_name")?.Value);
        Assert.Equal("b.mkv", saga.Element("poster")?.Value);
        Assert.Equal("a.mkv", saga.Element("fanart")?.Value);
        Assert.Equal(
            ["b.mkv", "a.mkv"],
            saga.Elements("movie").Select(x => x.Value)
        );
    }

    [Fact]
    public void DeleteSaga_Existante_NeSupprimePasLesAutresGroupes()
    {
        XDocument groups = Groups();
        LegacyMovieGroups.DeleteSaga(groups, "Avant");
        Assert.Single(groups.Descendants("saga"));
        Assert.Equal(
            "Après",
            groups.Descendants("saga").Single().Element("name")?.Value
        );
    }

    [Fact]
    public void DeleteSelection_Existante_NeModifiePasLesSagas()
    {
        XDocument groups = Groups();
        LegacyMovieGroups.CreateSelection(groups, "Temp", ["a.mkv"]);
        LegacyMovieGroups.DeleteSelection(groups, "Temp");
        Assert.Empty(groups.Descendants("selection"));
        Assert.Equal(2, groups.Descendants("saga").Count());
    }

    [Fact]
    public void ToggleSelection_AppartenanceMixte_AlterneChaqueFilm()
    {
        XDocument groups = Groups();
        LegacyMovieGroups.CreateSelection(groups, "Temp", ["a.mkv"]);
        LegacyMovieGroups.ToggleSelection(
            groups, "Temp", ["a.mkv", "b.mkv"]
        );
        XElement selection = Assert.Single(
            groups.Descendants("selection")
        );
        Assert.Equal("b.mkv", selection.Element("movie")?.Value);
        Assert.DoesNotContain(
            selection.Elements("movie"), item => item.Value == "a.mkv"
        );
    }

    [Fact]
    public void EditSaga_NomInvalide_ConserveLeDocument()
    {
        XDocument groups = Groups();
        XDocument original = new(groups);
        Assert.Throws<ArgumentException>(() =>
            LegacyMovieGroups.EditSaga(
                groups, "Avant", "Interdit/nom", "Tri", "a.mkv",
                "b.mkv", ["a.mkv", "b.mkv"]
            )
        );
        Assert.True(XNode.DeepEquals(original, groups));
    }

}
