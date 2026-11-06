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
        string[] folderNames = ["Films", "Series"];
        new XDocument(
            new XElement(
                "folders",
                folderNames.Select(
                    (name, type) =>
                        new XElement(
                            "folder",
                            new XElement(
                                "path",
                                Path.Combine(_directory, name)
                            ),
                            new XElement("type", type),
                            new XElement(
                                "dunepath",
                                "smb://nas/" + name
                            )
                        )
                )
            )
        ).Save(Path.Combine(_directory, "folders.xml"));
    }

    [Fact]
    public async Task CycleStateFilterCommand_Clic_ParcourtLesEtats()
    {
        using MainWindowViewModel model = new(
            Path.Combine(_directory, "user.config")
        );
        await model.OpenAsync(_directory);
        model.MediaFilterIndex = 0;
        string[] labels =
        [
            "Vérifiés",
            "Non vérifiés",
            "Manquants",
            "Non traités",
            "Tous",
        ];
        foreach (string label in labels)
        {
            model.CycleStateFilterCommand.Execute(null);
            Assert.Equal(label, model.StateFilterLabel);
            Assert.Equal(
                label == "Tous" ? 4 : 1,
                model.VisibleCards.Count
            );
        }
        model.InvertStateFilter = true;
        Assert.Empty(model.VisibleCards);
    }

    [Fact]
    public async Task StateFilters_TypeEtRecherche_CumuleLesPredicats()
    {
        using MainWindowViewModel model = new(
            Path.Combine(_directory, "user.config")
        );
        await model.OpenAsync(_directory);
        model.MediaFilterIndex = 0;
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
        using MainWindowViewModel model = new(
            Path.Combine(_directory, "user.config")
        );
        await model.OpenAsync(_directory);
        model.MediaFilterIndex = 0;
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
        using MainWindowViewModel model = new(
            Path.Combine(_directory, "user.config")
        );
        await model.OpenAsync(_directory);
        model.MediaFilterIndex = 0;
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
        Assert.Equal("Tous", model.StateFilterLabel);
        Assert.Equal(4, model.VisibleCards.Count);
    }

    [Fact]
    public async Task SagaContextChoices_ClicSurNom_AjoutePuisRetireLeFilm()
    {
        using MainWindowViewModel model = new(
            Path.Combine(_directory, "user.config")
        );
        await model.OpenAsync(_directory);
        model.SelectedCard = model.Cards.Single(card =>
            card.FolderName == "Wall·E.iso"
        );
        model.TreatmentSelection.Clear();

        GroupContextChoice saga = model.SagaContextChoices.Single(
            choice => choice.Label == "Épopée d'Howl"
        );
        Assert.Equal(2, saga.Children.Count);
        Assert.False(saga.IsChecked);

        saga.SelectCommand.Execute(null);
        await WaitForSagaMembershipAsync(model, "Wall·E.iso", true);
        Assert.True(model.SagaContextChoices.Single(
            choice => choice.Label == "Épopée d'Howl"
        ).IsChecked);

        saga = model.SagaContextChoices.Single(
            choice => choice.Label == "Épopée d'Howl"
        );
        saga.SelectCommand.Execute(null);
        await WaitForSagaMembershipAsync(model, "Wall·E.iso", false);
        Assert.False(model.SagaContextChoices.Single(
            choice => choice.Label == "Épopée d'Howl"
        ).IsChecked);
        Assert.Contains(
            XDocument.Load(Path.Combine(_directory, "groups.xml"))
                .Descendants("saga"),
            entry => entry.Element("name")?.Value == "Épopée d'Howl"
        );
    }

    private async Task WaitForSagaMembershipAsync(
        MainWindowViewModel model,
        string movie,
        bool expected
    )
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            bool exists = XDocument.Load(
                Path.Combine(_directory, "groups.xml")
            ).Descendants("saga").Elements("movie")
                .Any(item => item.Value == movie);
            if (exists == expected && !model.IsBusy)
            {
                return;
            }
            await Task.Delay(20);
        }
        Assert.Fail("L'appartenance n'a pas été sauvegardée.");
    }

    [Fact]
    public async Task SagaContextChoices_SousMenuParSaga_ConserveClicDirectEtEdition()
    {
        using MainWindowViewModel model = new(
            Path.Combine(_directory, "user.config")
        );
        await model.OpenAsync(_directory);
        model.SelectedCard = model.Cards.Single(card =>
            card.FolderName == "Wall·E.iso"
        );

        GroupContextChoice saga = model.SagaContextChoices.Single(
            choice => choice.Label == "Épopée d'Howl"
        );
        Assert.Equal(2, saga.Children.Count);
        Assert.Equal(
            model.Texts.GetString("87"),
            saga.Children[0].Label
        );
        Assert.Equal(
            model.Texts.GetString("61"),
            saga.Children[1].Label
        );

        string? edited = null;
        model.SagaEditingRequested += name => edited = name;
        saga.Children[0].SelectCommand.Execute(null);
        Assert.Equal("Épopée d'Howl", edited);

        saga.SelectCommand.Execute(null);
        await WaitForSagaMembershipAsync(model, "Wall·E.iso", true);
    }

    [Fact]
    public async Task SagaContextChoices_ModifierSaga_OuvreLaCommandeDediee()
    {
        using MainWindowViewModel model = new(
            Path.Combine(_directory, "user.config")
        );
        await model.OpenAsync(_directory);
        model.SelectedCard = model.Cards.Single(card =>
            card.FolderName == "Wall·E.iso"
        );

        string? requestedSaga = null;
        model.SagaEditingRequested += name => requestedSaga = name;

        GroupContextChoice saga = model.SagaContextChoices.Single(
            choice => choice.Label == "Épopée d'Howl"
        );
        Assert.Equal(2, saga.Children.Count);
        Assert.Equal(
            model.Texts.GetString("87"),
            saga.Children[0].Label
        );
        saga.Children[0].SelectCommand.Execute(null);

        Assert.Equal("Épopée d'Howl", requestedSaga);
    }

    [Fact]
    public async Task GenreFilters_ChangementDeType_ReinitialiseLeGroupe()
    {
        using MainWindowViewModel model = new(
            Path.Combine(_directory, "user.config")
        );
        await model.OpenAsync(_directory);
        model.MediaFilterIndex = 0;
        model
            .GenreFilters.Single(item => item.Label == "Drame")
            .SelectCommand.Execute(null);
        Assert.Single(model.VisibleCards);
        model.MediaFilterIndex = 2;
        Assert.Equal("Tous", model.StateFilterLabel);
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
        using MainWindowViewModel model = new(
            Path.Combine(_directory, "user.config")
        );
        await model.OpenAsync(_directory);
        model.MediaFilterIndex = 0;
        string other = Path.Combine(_directory, "other");
        Directory.CreateDirectory(Path.Combine(other, "data"));
        await model.OpenAsync(other);
        Assert.False(model.ShowSagaFilters);
        Assert.False(model.ShowSelectionFilters);
        Assert.False(model.ShowGenreFilters);
    }

    [Fact]
    public async Task GenreChoices_Closing_AppliesCheckedMovieGenresOnly()
    {
        using MainWindowViewModel model = new(
            Path.Combine(_directory, "user.config")
        );
        await model.OpenAsync(_directory);
        MediaCard card = model.Cards.Single(item =>
            item.ProcessingState == MediaProcessingState.Checked
        );
        model.SelectedCard = card;
        model.PrepareGenreChoices();
        Assert.Equal("Animation", card.Genres);
        Assert.Contains(
            model.GenreChoices,
            item => item.Name == "Science-fiction"
        );
        foreach (GenreChoice choice in model.GenreChoices)
        {
            choice.IsSelected = choice.Name == "Science-fiction";
        }
        Assert.Equal("Animation", card.Genres);
        model.ApplyGenreChoices();
        Assert.Equal("Science-fiction", card.Genres);
        Assert.True(card.IsDirty);
    }

    private void AddCard(
        string name,
        int status,
        bool series,
        string genre
    )
    {
        string source = Path.Combine(
            _directory,
            series ? "Series" : "Films"
        );
        Directory.CreateDirectory(source);
        if (series)
        {
            Directory.CreateDirectory(
                Path.Combine(source, name)
            );
        }
        else
        {
            File.WriteAllBytes(Path.Combine(source, name), []);
        }
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
                    new XElement("path", source),
                    new XElement(
                        "folder",
                        series ? "yes" : "no"
                    ),
                    new XElement(
                        "dunepath",
                        "smb://nas/"
                            + (series ? "Series" : "Films")
                    ),
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

    [Fact]
    public async Task DeleteMediaCommand_FilmSelectionne_SupprimeFicheSansSource()
    {
        using MainWindowViewModel model = new(
            Path.Combine(_directory, "user.config")
        );
        await model.OpenAsync(_directory);
        MediaCard card = model.Cards.Single(item =>
            item.FolderName == "Wall·E.iso"
        );
        model.SelectedCard = card;
        string source = Path.Combine(_directory, "Films", card.FolderName);
        Assert.True(File.Exists(source));

        model.DeleteMediaCommand.Execute(null);
        for (int attempt = 0; attempt < 150
            && model.Cards.Contains(card); attempt++)
        {
            await Task.Delay(20);
        }

        Assert.DoesNotContain(card, model.Cards);
        Assert.False(File.Exists(card.FilePath));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public async Task ToggleVerificationCommand_FilmTraite_ConserveLeStatut()
    {
        using MainWindowViewModel model = new(
            Path.Combine(_directory, "user.config")
        );
        await model.OpenAsync(_directory);
        MediaCard card = model.Cards.Single(item =>
            item.FolderName == "Wall·E.iso"
        );
        model.SelectedCard = card;
        model.TreatmentSelection.Clear();
        model.ToggleVerificationCommand.Execute(null);
        await WaitForVerificationAsync(
            card,
            MediaProcessingState.Checked
        );
        Assert.Equal(
            "0",
            XDocument.Load(card.FilePath)
                .Descendants("status").Single().Value
        );
    }

    private static async Task WaitForVerificationAsync(
        MediaCard card,
        MediaProcessingState expected
    )
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (card.ProcessingState == expected
                && !card.IsDirty)
            {
                return;
            }
            await Task.Delay(10);
        }

        Assert.Fail("Le statut vérifié n'a pas été enregistré.");
    }
}
