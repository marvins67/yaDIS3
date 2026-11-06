using Xunit;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;

namespace YaDIS3.Desktop.Tests;

public sealed class LegacyMediaContextMenuStateTests
{
    [Theory]
    [InlineData(MediaProcessingState.Checked)]
    [InlineData(MediaProcessingState.Unchecked)]
    public void From_FilmTraite_ActiveLesActionsHistoriques(
        MediaProcessingState status
    )
    {
        LegacyMediaContextMenuState state =
            LegacyMediaContextMenuState.From(false, status, 1);

        Assert.True(state.ShowMovieGroups);
        Assert.True(state.CanOpenFolders);
        Assert.True(state.CanOpenInformation);
        Assert.True(state.CanChangeTitle);
        Assert.True(state.CanReloadInformation);
        Assert.True(state.CanReloadTechnical);
        Assert.True(state.CanDelete);
        Assert.True(state.CanEditSaga);
        Assert.True(state.CanEditSelection);
        Assert.True(state.CanToggleVerification);
        Assert.False(state.CanCreateManually);
        Assert.False(state.ShowSeriesInformation);
    }

    [Fact]
    public void From_FilmManquant_DesactiveLesActionsInterdites()
    {
        LegacyMediaContextMenuState state =
            LegacyMediaContextMenuState.From(
                false,
                MediaProcessingState.Missing,
                1
            );

        Assert.True(state.CanOpenFolders);
        Assert.True(state.CanDelete);
        Assert.True(state.CanReloadTechnical);
        Assert.True(state.CanEditSelection);
        Assert.False(state.CanOpenInformation);
        Assert.False(state.CanCreateManually);
        Assert.False(state.CanChangeTitle);
        Assert.False(state.CanReloadInformation);
        Assert.False(state.CanEditSaga);
        Assert.False(state.CanToggleVerification);
    }

    [Fact]
    public void From_FilmNonTraite_ActiveLaCreationManuelle()
    {
        LegacyMediaContextMenuState state =
            LegacyMediaContextMenuState.From(
                false,
                MediaProcessingState.Unprocessed,
                1
            );

        Assert.True(state.CanCreateManually);
        Assert.True(state.CanChangeTitle);
        Assert.True(state.CanReloadInformation);
        Assert.False(state.CanOpenInformation);
        Assert.False(state.CanEditSaga);
        Assert.False(state.CanToggleVerification);
    }

    [Fact]
    public void From_SelectionMultiple_ConserveLesReglesDeYaDIS2()
    {
        LegacyMediaContextMenuState state =
            LegacyMediaContextMenuState.From(
                false,
                MediaProcessingState.Missing,
                2
            );

        Assert.False(state.CanOpenFolders);
        Assert.False(state.CanOpenInformation);
        Assert.False(state.CanCreateManually);
        Assert.False(state.CanChangeTitle);
        Assert.False(state.CanEditSaga);
        Assert.True(state.CanReloadInformation);
        Assert.True(state.CanReloadTechnical);
        Assert.True(state.CanDelete);
        Assert.True(state.CanEditSelection);
        Assert.True(state.CanToggleVerification);
    }

    [Fact]
    public void From_Serie_MasqueLesCommandesPropresAuxFilms()
    {
        LegacyMediaContextMenuState state =
            LegacyMediaContextMenuState.From(
                true,
                MediaProcessingState.Checked,
                1
            );

        Assert.False(state.ShowMovieInformation);
        Assert.True(state.ShowSeriesInformation);
        Assert.False(state.ShowMovieGroups);
        Assert.False(state.CanCreateManually);
        Assert.False(state.CanEditSaga);
        Assert.False(state.CanEditSelection);
        Assert.True(state.CanOpenInformation);
    }

    [Fact]
    public void From_SansSelection_AucuneActionDisponible()
    {
        LegacyMediaContextMenuState state =
            LegacyMediaContextMenuState.From(
                false,
                MediaProcessingState.Unknown,
                0
            );

        Assert.False(state.CanDelete);
        Assert.False(state.CanReloadInformation);
        Assert.False(state.CanReloadTechnical);
    }

    [Fact]
    public void From_SerieNonTraitee_ConserveLesReglesHistoriques()
    {
        LegacyMediaContextMenuState state =
            LegacyMediaContextMenuState.From(
                true,
                MediaProcessingState.Unprocessed,
                1
            );

        Assert.True(state.ShowSeriesInformation);
        Assert.False(state.ShowMovieGroups);
        Assert.True(state.CanOpenFolders);
        Assert.True(state.CanChangeTitle);
        Assert.True(state.CanReloadInformation);
        Assert.True(state.CanDelete);
        Assert.False(state.CanOpenInformation);
        Assert.False(state.CanCreateManually);
        Assert.False(state.CanToggleVerification);
    }

    [Fact]
    public void From_SeriesMultiples_ConserveLesActionsDeLot()
    {
        LegacyMediaContextMenuState state =
            LegacyMediaContextMenuState.From(
                true,
                MediaProcessingState.Checked,
                3
            );

        Assert.True(state.ShowSeriesInformation);
        Assert.False(state.ShowMovieGroups);
        Assert.False(state.CanOpenFolders);
        Assert.False(state.CanOpenInformation);
        Assert.False(state.CanChangeTitle);
        Assert.True(state.CanReloadInformation);
        Assert.True(state.CanReloadTechnical);
        Assert.True(state.CanDelete);
        Assert.True(state.CanToggleVerification);
    }
}
