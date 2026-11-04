using YaDIS3.Core;

namespace YaDIS3.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Cases proposées par le menu historique des genres.</summary>
    public IReadOnlyList<GenreChoice> GenreChoices
    {
        get;
        private set;
    } = [];

    /// <summary>Réunit les genres saisis et ceux des médias du même type.</summary>
    public void PrepareGenreChoices()
    {
        string[] selected = (EditableCard?.Genres ?? "").Split(
            ',',
            StringSplitOptions.TrimEntries
        );
        GenreChoices = Cards
            .Where(MatchesMediaCategory)
            .SelectMany(LegacyLibraryFilter.Genres)
            .Concat(selected)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.CurrentCulture)
            .Select(name => new GenreChoice(
                name,
                selected.Contains(name, StringComparer.Ordinal)
            ))
            .ToArray();
        Notify(nameof(GenreChoices));
    }

    /// <summary>Applique les cases à la fermeture, comme ButtonGenres_Leave.</summary>
    public void ApplyGenreChoices()
    {
        if (EditableCard is { } card)
        {
            card.Genres = string.Join(
                ", ",
                GenreChoices
                    .Where(choice => choice.IsSelected)
                    .Select(choice => choice.Name)
            );
        }
    }
}
