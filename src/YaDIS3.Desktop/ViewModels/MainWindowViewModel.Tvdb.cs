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
        CredentialsLabel = Texts.GetString("343");
        Status = Texts.GetString("344");
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
                ? Texts.GetString("345")
                : Texts.Format("374", found.Count);
    }

    /// <summary>Récupère les ordres d'une série sans modifier l'état partagé.</summary>
    public Task<IReadOnlyList<EpisodeOrder>> GetSeriesOrdersAsync(long seriesId)
    {
        TvdbClient api = _tvdb
            ?? throw new InvalidOperationException(
                "Chargez votre tvdb-v4.json avec Configuration TVDB."
            );
        return api.AvailableOrdersAsync(seriesId, OperationToken);
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
                Texts.GetString("346")
            );
        MediaCard card =
            SelectedCard
            ?? throw new InvalidOperationException(
                Texts.GetString("347")
            );
        SeriesMatch match =
            SelectedMatch
            ?? throw new InvalidOperationException(
                Texts.GetString("348")
            );
        EpisodeOrder order =
            SelectedOrder
            ?? throw new InvalidOperationException(
                "Choisissez un ordre disponible."
            );
        SeriesDetails result = await api.SeriesAsync(
            match.Id,
            order.Code,
            "fra",
            card.Episodes.Select(episode =>
                (episode.Season, episode.Number)).ToHashSet(),
            OperationToken
        );
        card.ApplyTvdb(result, order.Code);
        Notify(nameof(SelectedCard));
        Status = Texts.GetString("349");
    }
}
