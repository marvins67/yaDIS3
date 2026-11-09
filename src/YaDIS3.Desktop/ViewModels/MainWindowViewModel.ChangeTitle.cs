using Avalonia.Media.Imaging;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Desktop.ViewModels;

/// <summary>Identification des films via le menu contextuel.</summary>
public sealed partial class MainWindowViewModel
{
    /// <summary>Recherche les titres proposés par TMDB.</summary>
    public Task<IReadOnlyList<MovieTitleMatch>> SearchMovieTitlesAsync(
        string title
    )
    {
        OnlineTreatmentMetadata metadata = new(
            _http,
            _tvdb,
            Environment.GetEnvironmentVariable("YADIS_TMDB_TOKEN"),
            TemplateLanguage,
            _importer
        );
        return metadata.SearchMovieTitlesAsync(title, OperationToken);
    }

    /// <summary>Recherche directement un film à partir d'un identifiant IMDb.</summary>
    public Task<MovieTitleMatch?> FindMovieByImdbAsync(string imdbId)
    {
        OnlineTreatmentMetadata metadata = new(
            _http,
            _tvdb,
            Environment.GetEnvironmentVariable("YADIS_TMDB_TOKEN"),
            TemplateLanguage,
            _importer
        );
        return metadata.FindMovieByImdbAsync(imdbId, OperationToken);
    }

    /// <summary>Charge la vignette d'un résultat sans modifier la fiche.</summary>
    public async Task<Bitmap?> LoadTitlePosterAsync(string posterUrl)
    {
        if (string.IsNullOrWhiteSpace(posterUrl))
        {
            return null;
        }
        string url = posterUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? posterUrl
            : "https://image.tmdb.org/t/p/w154" + posterUrl;
        try
        {
            byte[] bytes = await _http.GetByteArrayAsync(url, OperationToken);
            using MemoryStream data = new(bytes);
            return new Bitmap(data);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>Retraite le film à partir d'un nouvel identifiant.</summary>
    public Task ChangeMovieTitleAsync(MovieTitleMatch match) =>
        RunAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(match);
            MediaCard card = SelectedCard
                ?? throw new InvalidOperationException(
                    Texts.GetString("330")
                );
            if (card.IsSeries)
            {
                throw new InvalidOperationException(
                    "La série doit être identifiée par TVDB."
                );
            }
            // Comme ChangeForm.DoChange, retirer les anciens critères
            // avant d'effacer l'identification et de retraiter le film.
            new LegacyMovieTitleChangeCleanup().Remove(
                card, OperationToken
            );
            card.PrepareMovieTitleChange(match.Id);
            foreach (string path in Directory.EnumerateFiles(
                card.DataDirectory, "*.jpg"
            ))
            {
                File.Delete(path);
            }
            await TreatSelectionAsync(
                TreatmentMode.ReloadInformation,
                OperationToken,
                [card]
            );
        });
    /// <summary>Reprend l'identification TVDB après confirmation du dialogue.</summary>
    public Task ChangeSeriesTitleAsync(long seriesId, string? order = null) =>
        RunAsync(async () =>
        {
            MediaCard card = SelectedCard
                ?? throw new InvalidOperationException(
                    Texts.GetString("347")
                );
            card.PrepareSeriesTitleChange(seriesId, order);
            foreach (string path in Directory.EnumerateFiles(
                card.DataDirectory, "*.jpg"
            ))
            {
                File.Delete(path);
            }
            await TreatSelectionAsync(
                TreatmentMode.ReloadInformation,
                OperationToken,
                [card]
            );
        });
}
