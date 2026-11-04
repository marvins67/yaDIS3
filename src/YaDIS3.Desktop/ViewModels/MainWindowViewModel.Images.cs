using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>
    /// Imports artwork into the current card's unsaved draft.
    /// </summary>
    public void ImportImage(ArtworkKind kind, string path)
    {
        MediaCard card =
            SelectedCard
            ?? throw new InvalidOperationException(
                Texts.GetString("330")
            );
        _importer.Import(card, kind, path, SelectedEpisode);
        RefreshImages();
        Status = Texts.GetString("331");
    }

    /// <summary>
    /// Enregistre l'épisode avant de passer à l'épisode voisin.
    /// </summary>
    public async Task NavigateAndSaveAsync(int direction)
    {
        if (SelectedCard is null || SelectedEpisode is null)
        {
            return;
        }

        int index =
            SelectedCard.Episodes.IndexOf(SelectedEpisode)
            + direction;
        if (index < 0 || index >= SelectedCard.Episodes.Count)
        {
            return;
        }

        if (SelectedCard.IsDirty)
        {
            await new LegacyIndexedCardSaver(
                _repository
            ).SaveAsync(SelectedCard);
        }

        SelectedEpisode = SelectedCard.Episodes[index];
        Status = Texts.GetString("332");
    }

    /// <summary>
    /// Reloads artwork and the rendered preview for the
    /// selection.
    /// </summary>
    public void RefreshImages()
    {
        Poster = LoadImage(SelectedCard?.PosterPath);
        Banner = LoadImage(SelectedCard?.BannerPath);
        Fanart = LoadImage(SelectedCard?.FanartPath);
        EpisodeImage = LoadImage(SelectedEpisode?.ImagePath);
        RefreshRenderedPreview();
    }

    private static Bitmap? LoadImage(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(
                File.ReadAllBytes(path)
            );
            return new Bitmap(stream);
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or ArgumentException
                        or InvalidDataException
            )
        {
            return null;
        }
    }

    private void Replace(
        ref Bitmap? field,
        Bitmap? value,
        [CallerMemberName] string? name = null
    )
    {
        Bitmap? previous = field;
        field = value;
        Notify(name);
        previous?.Dispose();
    }
}
