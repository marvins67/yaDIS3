using YaDIS3.Core;

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
            card.PrepareMovieTitleChange(match.Id);
            foreach (string path in Directory.EnumerateFiles(
                card.DataDirectory, "*.jpg"
            ))
            {
                File.Delete(path);
            }
            await TreatSelectionAsync(
                TreatmentMode.ReloadInformation,
                OperationToken
            );
        });
    /// <summary>Reprend l'identification TVDB après confirmation du dialogue.</summary>
    public Task ChangeSeriesTitleAsync(long seriesId) =>
        RunAsync(async () =>
        {
            MediaCard card = SelectedCard
                ?? throw new InvalidOperationException(
                    Texts.GetString("347")
                );
            card.PrepareSeriesTitleChange(seriesId);
            foreach (string path in Directory.EnumerateFiles(
                card.DataDirectory, "*.jpg"
            ))
            {
                File.Delete(path);
            }
            await TreatSelectionAsync(
                TreatmentMode.ReloadInformation,
                OperationToken
            );
        });
}
