using YaDIS3.Core;

namespace YaDIS3.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>
    /// Charge les identifiants TVDB depuis le fichier de
    /// configuration choisi.
    /// </summary>
    public void ConfigureTvdb(string file)
    {
        _tvdb?.Dispose();
        _tvdb = new TvdbClient(_http, TvdbSettings.Load(file));
        CredentialsLabel = "TVDB v4 configuré";
        Status = "Configuration TVDB chargée.";
    }

    /// <summary>
    /// Recherche les titres de séries TVDB dans la langue
    /// configurée.
    /// </summary>
    public async Task SearchAsync()
    {
        TvdbClient api =
            _tvdb
            ?? throw new InvalidOperationException(
                "Chargez votre tvdb-v4.json avec Configuration TVDB."
            );
        IReadOnlyList<SeriesMatch> found = await api.SearchAsync(
            Query,
            cancellationToken: OperationToken
        );
        SearchResults.Clear();
        Orders.Clear();
        SelectedMatch = null;
        SelectedOrder = null;
        foreach (SeriesMatch match in found)
        {
            SearchResults.Add(match);
        }

        Status =
            found.Count == 0
                ? "Aucune série trouvée. Vous pouvez ajouter une série manuelle depuis son dossier."
                : $"{found.Count} résultat(s) TVDB.";
    }

    /// <summary>
    /// Actualise les ordres d'épisodes proposés pour le résultat
    /// sélectionné.
    /// </summary>
    public async Task LoadOrdersAsync()
    {
        SeriesMatch? match = SelectedMatch;
        if (match is null || _tvdb is null)
        {
            return;
        }

        Orders.Clear();
        SelectedOrder = null;
        IReadOnlyList<EpisodeOrder> available =
            await _tvdb.AvailableOrdersAsync(
                match.Id,
                OperationToken
            );
        if (SelectedMatch != match)
        {
            return;
        }

        foreach (EpisodeOrder order in available)
        {
            Orders.Add(order);
        }

        SelectedOrder =
            available.FirstOrDefault(order =>
                order.Code == SelectedCard?.Order
            ) ?? (available.Count > 0 ? available[0] : null);
    }

    /// <summary>
    /// Applique les métadonnées de la série TVDB sélectionnée à
    /// la fiche courante.
    /// </summary>
    public async Task ApplySeriesAsync()
    {
        TvdbClient api =
            _tvdb
            ?? throw new InvalidOperationException(
                "TVDB non configuré."
            );
        MediaCard card =
            SelectedCard
            ?? throw new InvalidOperationException(
                "Sélectionnez une série."
            );
        SeriesMatch match =
            SelectedMatch
            ?? throw new InvalidOperationException(
                "Choisissez une série dans les résultats."
            );
        EpisodeOrder order =
            SelectedOrder
            ?? throw new InvalidOperationException(
                "Choisissez un ordre disponible."
            );
        SeriesDetails result = await api.SeriesAsync(
            match.Id,
            order.Code,
            cancellationToken: OperationToken
        );
        card.ApplyTvdb(result, order.Code);
        Notify(nameof(SelectedCard));
        Status =
            "Informations TVDB appliquées au brouillon. Cliquez sur Enregistrer.";
    }
}
