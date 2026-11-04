using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Tests;

internal sealed class SeriesTreatmentMetadata : ITreatmentMetadata
{
    public Task<XElement?> FindMovieAsync(MediaCard card, CancellationToken cancellationToken) =>
        throw new InvalidOperationException();

    public Task CompleteArtworkAsync(MediaCard card, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<SeriesDetails?> FindSeriesAsync(MediaCard card, CancellationToken cancellationToken) =>
        Task.FromResult<SeriesDetails?>(new SeriesDetails(42, "Titre fournisseur", "Résumé", "2020-01-01", "Chaîne",
            [new(1, 1, "Épisode fournisseur", "Résumé", "2020-01-02", "") { Rating = "8", Director = "Réalisateur" },
             new(1, 2, "Nouvel épisode", "Résumé", "2020-01-03", "") { Rating = "9", Director = "Autre réalisateur" }])
        { Rating = "7", Genres = ["Comédie"], Actors = ["Interprète"] });
}
