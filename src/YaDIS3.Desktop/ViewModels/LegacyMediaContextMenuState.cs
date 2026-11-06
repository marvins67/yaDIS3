using YaDIS3.Core;

namespace YaDIS3.Desktop.ViewModels;

/// <summary>
/// Reproduit les conditions de MenuMedia_Opening de yaDIS2.
/// Les commandes non encore portées restent désactivées dans la vue.
/// </summary>
public sealed record LegacyMediaContextMenuState
{
    /// <summary>Affiche le lien vers la fiche du film.</summary>
    public bool ShowMovieInformation { get; init; }

    /// <summary>Affiche le lien vers la fiche de la série.</summary>
    public bool ShowSeriesInformation { get; init; }

    /// <summary>Affiche les sous-menus réservés aux films.</summary>
    public bool ShowMovieGroups { get; init; }

    /// <summary>Autorise l'ouverture des dossiers pour une sélection unique.</summary>
    public bool CanOpenFolders { get; init; }

    /// <summary>Autorise l'ouverture de la fiche d'information en ligne.</summary>
    public bool CanOpenInformation { get; init; }

    /// <summary>Autorise la création manuelle d'une fiche de film.</summary>
    public bool CanCreateManually { get; init; }

    /// <summary>Autorise le changement du titre sélectionné.</summary>
    public bool CanChangeTitle { get; init; }

    /// <summary>Autorise le rechargement des informations du média.</summary>
    public bool CanReloadInformation { get; init; }

    /// <summary>Autorise le rechargement des informations techniques.</summary>
    public bool CanReloadTechnical { get; init; }

    /// <summary>Autorise la suppression des titres sélectionnés.</summary>
    public bool CanDelete { get; init; }

    /// <summary>Autorise la modification de l'appartenance à une saga.</summary>
    public bool CanEditSaga { get; init; }

    /// <summary>Autorise la modification des sélections de films.</summary>
    public bool CanEditSelection { get; init; }

    /// <summary>Autorise le basculement de l'état de vérification.</summary>
    public bool CanToggleVerification { get; init; }

    /// <summary>Détermine les états du menu selon la sélection historique.</summary>
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
