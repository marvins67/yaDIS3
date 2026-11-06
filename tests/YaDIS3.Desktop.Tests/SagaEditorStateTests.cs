using Xunit;
using YaDIS3.Desktop.ViewModels;

namespace YaDIS3.Desktop.Tests;

public sealed class SagaEditorStateTests
{
    private static SagaContextDraft Original() =>
        new(
            "Batman",
            "Batman",
            "Premier.mkv",
            "Second.mkv",
            ["Premier.mkv", "Second.mkv", "Troisième.mkv"]
        );

    [Fact]
    public void NextArtwork_ClicSurImage_FaitDefilerChaqueReference()
    {
        SagaEditorState state = new(Original());
        state.NextArtwork(true);
        Assert.Equal("Second.mkv", state.Poster);
        state.NextArtwork(true);
        Assert.Equal("Troisième.mkv", state.Poster);
        state.NextArtwork(true);
        Assert.Equal("Premier.mkv", state.Poster);
        state.NextArtwork(false);
        Assert.Equal("Troisième.mkv", state.Fanart);
        Assert.Equal("Premier.mkv", state.Poster);
    }

    [Fact]
    public void Remove_MembreSelectionne_ReaffecteLesDeuxImages()
    {
        SagaEditorState state = new(Original());
        state.Remove(["Premier.mkv", "Second.mkv"]);
        Assert.Equal(["Troisième.mkv"], state.Members);
        Assert.Equal("Troisième.mkv", state.Poster);
        Assert.Equal("Troisième.mkv", state.Fanart);
        state.Remove(["Troisième.mkv"]);
        Assert.Empty(state.Members);
        Assert.Empty(state.Poster);
        Assert.Empty(state.Fanart);
    }

    [Fact]
    public void Move_GlisserDeposer_ChangeOrdreSansChangerLesImages()
    {
        SagaEditorState state = new(Original());
        state.Move(0, 2);
        Assert.Equal(
            ["Second.mkv", "Troisième.mkv", "Premier.mkv"],
            state.Members
        );
        Assert.Equal("Premier.mkv", state.Poster);
        Assert.Equal("Second.mkv", state.Fanart);
    }

    [Fact]
    public void Annuler_NeModifiePasLaSagaInitiale()
    {
        SagaContextDraft original = Original();
        SagaEditorState state = new(original);
        state.Name = "Autre nom";
        state.NextArtwork(true);
        state.Move(0, 2);
        state.Remove(["Second.mkv"]);

        Assert.Equal("Batman", original.Name);
        Assert.Equal("Premier.mkv", original.Poster);
        Assert.Equal("Second.mkv", original.Fanart);
        Assert.Equal(
            ["Premier.mkv", "Second.mkv", "Troisième.mkv"],
            original.Members
        );
    }

    [Fact]
    public void ToDraft_ConserveLesChampsEtLOrdreChoisis()
    {
        SagaEditorState state = new(Original())
        {
            Name = "Le Chevalier noir",
            SortName = "Chevalier noir",
        };
        state.Move(2, 0);
        SagaContextDraft draft = state.ToDraft();
        Assert.Equal("Le Chevalier noir", draft.Name);
        Assert.Equal("Chevalier noir", draft.SortName);
        Assert.Equal(
            ["Troisième.mkv", "Premier.mkv", "Second.mkv"],
            draft.Members
        );
    }
}
