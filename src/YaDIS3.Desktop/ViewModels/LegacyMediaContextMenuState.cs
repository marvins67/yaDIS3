using YaDIS3.Core;

namespace YaDIS3.Desktop.ViewModels;

/// <summary>
/// Reproduit les conditions de MenuMedia_Opening de yaDIS2.
/// Les commandes non encore portées restent désactivées dans la vue.
/// </summary>
public sealed record LegacyMediaContextMenuState
{
    public bool ShowMovieInformation { get; init; }

    public bool ShowSeriesInformation { get; init; }

    public bool ShowMovieGroups { get; init; }

    public bool CanOpenFolders { get; init; }

    public bool CanOpenInformation { get; init; }

    public bool CanCreateManually { get; init; }

    public bool CanChangeTitle { get; init; }

    public bool CanReloadInformation { get; init; }

    public bool CanReloadTechnical { get; init; }

    public bool CanDelete { get; init; }

    public bool CanEditSaga { get; init; }

    public bool CanEditSelection { get; init; }

    public bool CanToggleVerification { get; init; }

    public static LegacyMediaContextMenuState From(
        bool isSeries,
        MediaProcessingState status,
        int selectionCount
    )
    {
        if (selectionCount <= 0)
        {
            return new LegacyMediaContextMenuState();
        }

        bool single = selectionCount == 1;
        bool available = status is
            MediaProcessingState.Checked or MediaProcessingState.Unchecked;

        return new LegacyMediaContextMenuState
        {
            ShowMovieInformation = !isSeries,
            ShowSeriesInformation = isSeries,
            ShowMovieGroups = !isSeries,
            CanOpenFolders = single,
            CanOpenInformation = single && available,
            CanCreateManually =
                single && !isSeries
                && status == MediaProcessingState.Unprocessed,
            CanChangeTitle =
                single && status != MediaProcessingState.Missing,
            CanReloadInformation =
                !single || status != MediaProcessingState.Missing,
            CanReloadTechnical = true,
            CanDelete = true,
            CanEditSaga = single && !isSeries && available,
            CanEditSelection = !isSeries,
            CanToggleVerification = !single || available,
        };
    }
}
