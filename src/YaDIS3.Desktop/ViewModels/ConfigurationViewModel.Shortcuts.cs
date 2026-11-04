using YaDIS3.Core;

namespace YaDIS3.Desktop.ViewModels;

public sealed partial class ConfigurationViewModel
{
    private int _shortcutType;
    private string _shortcutName = "";
    private string _shortcutPath = "";
    private string? _shortcutSelection;
    private string? _shortcutGenre;

    /// <summary>
    /// Types de raccourcis de SetupForm.
    /// </summary>
    public IReadOnlyList<string> ShortcutTypes { get; }

    /// <summary>
    /// Type du raccourci proposé.
    /// </summary>
    public int ShortcutType
    {
        get => _shortcutType;
        set
        {
            _shortcutType = value;
            _shortcutName = "";
            _shortcutPath = "";
            _shortcutGenre = null;
            Notify(null);
        }
    }

    /// <summary>
    /// Champs de dossier personnalisé.
    /// </summary>
    public bool IsFolderShortcut => ShortcutType == 2;

    /// <summary>
    /// Liste de sélections.
    /// </summary>
    public bool IsSelectionShortcut => ShortcutType == 0;

    /// <summary>
    /// Liste de genres.
    /// </summary>
    public bool IsGenreShortcut => ShortcutType == 1;

    /// <summary>
    /// Nom du dossier personnalisé.
    /// </summary>
    public string ShortcutName
    {
        get => _shortcutName;
        set
        {
            _shortcutName = value;
            Notify(null);
        }
    }

    /// <summary>
    /// Adresse du dossier personnalisé.
    /// </summary>
    public string ShortcutPath
    {
        get => _shortcutPath;
        set
        {
            _shortcutPath = value;
            Notify(null);
        }
    }

    /// <summary>
    /// Sélection proposée.
    /// </summary>
    public string? ShortcutSelection
    {
        get => _shortcutSelection;
        set
        {
            _shortcutSelection = value;
            Notify(null);
        }
    }

    /// <summary>
    /// Genre proposé.
    /// </summary>
    public string? ShortcutGenre
    {
        get => _shortcutGenre;
        set
        {
            _shortcutGenre = value;
            Notify(null);
        }
    }

    /// <summary>
    /// Reprend la validation ChangeAddButtonState.
    /// </summary>
    public bool CanAddShortcut =>
        ShortcutType switch
        {
            0 => ShortcutSelection is not null,
            1 => ShortcutGenre is not null,
            2 => ShortcutName.Length > 0
                && ShortcutPath.Length > 0,
            _ => false,
        };

    /// <summary>
    /// Ajoute sans dédoublonnage, comme dans SetupForm.
    /// </summary>
    public void AddShortcut()
    {
        if (!CanAddShortcut)
        {
            return;
        }
        Shortcuts.Add(
            new(
                ShortcutType switch
                {
                    0 => ShortcutSelection!,
                    1 => ShortcutGenre!,
                    _ => ShortcutName,
                },
                ShortcutType,
                ShortcutType == 2 ? ShortcutPath : ""
            )
        );
    }

    /// <summary>
    /// Déplace l'entrée sans modifier les autres raccourcis.
    /// </summary>
    public void MoveShortcut(int from, int to)
    {
        if (
            from >= 0
            && from < Shortcuts.Count
            && to >= 0
            && to < Shortcuts.Count
            && from != to
        )
        {
            Shortcuts.Move(from, to);
        }
    }

    /// <summary>
    /// Retire uniquement les entrées sélectionnées.
    /// </summary>
    public void RemoveShortcuts(
        IEnumerable<DuneShortcut> shortcuts
    )
    {
        foreach (DuneShortcut shortcut in shortcuts.ToArray())
        {
            Shortcuts.Remove(shortcut);
        }
    }
}
