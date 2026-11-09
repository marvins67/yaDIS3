namespace YaDIS3.Desktop.ViewModels;

/// <summary>
/// Recherche incrémentale des titres d'une liste, comme ListView WinForms.
/// </summary>
public sealed class LibraryTypeAheadSearch
{
    private static readonly TimeSpan s_typingTimeout =
        TimeSpan.FromSeconds(1);

    private string _prefix = "";

    private DateTimeOffset _lastInput;

    /// <summary>Renvoie l'index du premier titre commençant par la saisie.</summary>
    public int Find(
        IReadOnlyList<string> titles,
        char character,
        DateTimeOffset timestamp
    )
    {
        ArgumentNullException.ThrowIfNull(titles);
        string next = timestamp - _lastInput <= s_typingTimeout
            ? _prefix + character
            : character.ToString();
        int index = FindPrefix(titles, next);
        if (index < 0)
        {
            next = character.ToString();
            index = FindPrefix(titles, next);
        }
        _prefix = next;
        _lastInput = timestamp;
        return index;
    }

    private static int FindPrefix(
        IReadOnlyList<string> titles,
        string prefix
    )
    {
        for (int index = 0; index < titles.Count; index++)
        {
            if (titles[index].StartsWith(
                prefix,
                StringComparison.CurrentCultureIgnoreCase
            ))
            {
                return index;
            }
        }
        return -1;
    }
}
