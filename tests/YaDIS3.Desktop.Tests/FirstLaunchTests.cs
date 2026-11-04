using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;

namespace YaDIS3.Desktop.Tests;

public sealed class FirstLaunchTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    public void Editor_CardStatus_UsesLegacyActivation(
        int status,
        bool enabled
    )
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString()
        );
        string folder = Path.Combine(
            root,
            "data",
            "Un film.mkv"
        );
        Directory.CreateDirectory(folder);
        try
        {
            new XDocument(
                new XElement(
                    "data",
                    new XElement(
                        "item",
                        new XElement("filename", "Un film.mkv"),
                        new XElement("type", "0"),
                        new XElement("status", status)
                    ),
                    new XElement(
                        "info",
                        new XElement("title", "Un film")
                    )
                )
            ).Save(Path.Combine(folder, "data.xml"));
            using WorkspaceRepository repository = new();
            using MainWindowViewModel model = new(
                Path.Combine(root, "user.config")
            );
            model.SelectedCard = Assert.Single(
                repository.Open(root).Cards
            );
            Assert.Equal(enabled, model.CanEditSelection);
            Assert.Equal(status != 2, model.CanProcessSelection);
            Assert.Equal(
                enabled,
                model.EditableCard is not null
            );
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ExportDestination_Empty_DisablesExport()
    {
        string file = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString(),
            "user.config"
        );
        using MainWindowViewModel model = new(file);
        Assert.False(model.CanExport);
        model.ExportParentPath = "C:/Dune/Export";
        Assert.True(model.CanExport);
        model.ExportParentPath = "";
        Assert.False(model.CanExport);
    }

    [Fact]
    public void MediaSelector_FirstLaunchAndToggle_ReproducesFilmSeriesChoices()
    {
        using MainWindowViewModel model = new(
            Path.Combine(
                Path.GetTempPath(),
                "unused-user.config"
            )
        );
        Assert.Equal(1, model.MediaFilterIndex);
        Assert.Equal("Film", model.MediaTabTitle);
        Assert.False(model.CanEditSelection);
        model.ToggleMediaFilterCommand.Execute(null);
        Assert.True(model.IsSeries);
        Assert.Equal("Série TV", model.MediaTabTitle);
        Assert.Equal("Séries TV", model.MediaFilterLabel);
        model.SelectMoviesCommand.Execute(null);
        Assert.False(model.IsSeries);
        Assert.Equal("Films", model.MediaFilterLabel);
    }
}
