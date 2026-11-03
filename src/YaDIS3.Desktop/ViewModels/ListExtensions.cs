namespace YaDIS3.Desktop.ViewModels;

internal static class ListExtensions
{
    /// <summary>
    /// Recherche l'indice d'un élément avec le comparateur
    /// d'égalité par défaut.
    /// </summary>
    public static int IndexOf<T>(
        this IReadOnlyList<T> source,
        T value
    )
    {
        for (var i = 0; i < source.Count; i++)
        {
            if (
                EqualityComparer<T>.Default.Equals(
                    source[i],
                    value
                )
            )
            {
                return i;
            }
        }

        return -1;
    }
}
