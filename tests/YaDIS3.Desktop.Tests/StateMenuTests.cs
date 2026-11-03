using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;

namespace YaDIS3.Desktop.Tests;

public sealed class StateMenuTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "yadis3-states-" + Guid.NewGuid()
    );

    public StateMenuTests()
    {
        AddCard(
            "Le Château d'Howl (2004).mkv",
            0,
            false,
            "Animation"
        );
        AddCard("Wall·E.iso", 1, false, "Science-fiction");
        AddCard("L'été & l'hiver.mkv", 2, false, "Drame");
        AddCard("Dragon Ball Yabai", 3, true, "Animation");
        XDocument groups = new(
            new XElement(
                "groups",
                new XElement(
                    "sagas",
                    new XElement(
                        "saga",
                        new XElement("name", "Épopée d'Howl"),
                        new XElement(
                            "movie",
                            "Le Château d'Howl (2004).mkv"
                        )
                    )
                ),
                new XElement(
                    "selections",
                    new XElement(
                        "selection",
                        new XAttribute("enabled", "no"),
                        new XElement("name", "À regarder"),
                        new XElement("movie", "Wall·E.iso")
                    )
                )
            )
        );
        groups.Save(Path.Combine(_directory, "groups.xml"));
    }

    [Fact]
    public async Task CycleStateFilterCommand_Clic_ParcourtLesEtats()
    {
        using MainWindowViewModel model = new();
        await model.OpenAsync(_directory);
        string[] labels =
        [
            "Vérifiés",
            "Non vérifiés",
            "Manquants",
            "Non traités",
            "Tous les états",
        ];
        foreach (string label in labels)
        {
            model.CycleStateFilterCommand.Execute(null);
            Assert.Equal(label, model.StateFilterLabel);
            Assert.Equal(
                label == "Tous les états" ? 4 : 1,
                model.VisibleCards.Count
            );
        }
        model.InvertStateFilter = true;
        Assert.Empty(model.VisibleCards);
    }

    [Fact]
    public async Task StateFilters_TypeEtRecherche_CumuleLesPredicats()
    {
        using MainWindowViewModel model = new();
        await model.OpenAsync(_directory);
        model.StateFilters[1].SelectCommand.Execute(null);
        model.InvertStateFilter = true;
        model.MediaFilterIndex = 1;
        model.Filter = "wall";
        Assert.Equal(
            "Wall·E.iso",
            Assert.Single(model.VisibleCards).FolderName
        );
        model.InvertStateFilter = false;
        Assert.Empty(model.VisibleCards);
    }

    [Fact]
    public async Task StateFilters_Edition_ConserveLaFichePendantLaSaisie()
    {
        using MainWindowViewModel model = new();
        await model.OpenAsync(_directory);
        model.StateFilters[1].SelectCommand.Execute(null);
        MediaCard card = Assert.Single(model.VisibleCards);
        card.Title = "Un nouveau titre";
        Assert.Same(card, model.SelectedCard);
        Assert.Contains(card, model.VisibleCards);
        model.StateFilters[1].SelectCommand.Execute(null);
        Assert.Empty(model.VisibleCards);
        Assert.Null(model.SelectedCard);
        model.StateFilters[2].SelectCommand.Execute(null);
        Assert.Contains(card, model.VisibleCards);
    }

    [Fact]
    public async Task GroupFilters_ChoixEtClicPrincipal_ReprendYaDis2()
    {
        using MainWindowViewModel model = new();
        await model.OpenAsync(_directory);
        Assert.True(model.ShowSagaFilters);
        Assert.True(model.ShowSelectionFilters);
        model.SagaFilters[1].SelectCommand.Execute(null);
        Assert.Equal(
            "Le Château d'Howl (2004).mkv",
            Assert.Single(model.VisibleCards).FolderName
        );
        model.SelectionFilters[0].SelectCommand.Execute(null);
        Assert.Equal(
            "Wall·E.iso",
            Assert.Single(model.VisibleCards).FolderName
        );
        model.CycleStateFilterCommand.Execute(null);
        Assert.Equal("Tous les états", model.StateFilterLabel);
        Assert.Equal(4, model.VisibleCards.Count);
    }

    [Fact]
    public async Task GenreFilters_ChangementDeType_ReinitialiseLeGroupe()
    {
        using MainWindowViewModel model = new();
        await model.OpenAsync(_directory);
        model
            .GenreFilters.Single(item => item.Label == "Drame")
            .SelectCommand.Execute(null);
        Assert.Single(model.VisibleCards);
        model.MediaFilterIndex = 2;
        Assert.Equal("Tous les états", model.StateFilterLabel);
        Assert.False(model.ShowSagaFilters);
        Assert.False(model.ShowSelectionFilters);
        Assert.Equal(
            "Animation",
            Assert.Single(model.GenreFilters).Label
        );
        Assert.Equal(
            "Dragon Ball Yabai",
            Assert.Single(model.VisibleCards).FolderName
        );
    }

    [Fact]
    public async Task OpenAsync_SecondWorkspace_NeConservePasLesGroupes()
    {
        using MainWindowViewModel model = new();
        await model.OpenAsync(_directory);
        string other = Path.Combine(_directory, "other");
        Directory.CreateDirectory(Path.Combine(other, "data"));
        await model.OpenAsync(other);
        Assert.False(model.ShowSagaFilters);
        Assert.False(model.ShowSelectionFilters);
        Assert.False(model.ShowGenreFilters);
    }

    private void AddCard(
        string name,
        int status,
        bool series,
        string genre
    )
    {
        string directory = Path.Combine(
            _directory,
            "data",
            name
        );
        Directory.CreateDirectory(directory);
        string xml = new XDocument(
            new XElement(
                "data",
                new XElement(
                    "item",
                    new XElement("filename", name),
                    new XElement("type", series ? "1" : "0"),
                    new XElement(
                        "status",
                        status.ToString(
                            CultureInfo.InvariantCulture
                        )
                    )
                ),
                new XElement(
                    "info",
                    new XElement("title", name),
                    new XElement(
                        "genres",
                        new XElement("genre", genre)
                    )
                )
            )
        ).ToString();
        File.WriteAllText(
            Path.Combine(directory, "data.xml"),
            xml,
            Encoding.UTF8
        );
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
