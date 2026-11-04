namespace YaDIS3.Desktop.ViewModels;

public sealed partial class ConfigurationViewModel
{
    /// <summary>
    /// Langue de l’interface et des synopsis.
    /// </summary>
    public string Language
    {
        get => _settings.Language;
        set
        {
            _settings = _settings with { Language = value };
            Notify(null);
        }
    }

    /// <summary>
    /// Mode basique sans catalogue central.
    /// </summary>
    public bool BasicMode
    {
        get => _settings.BasicMode;
        set
        {
            _settings = _settings with { BasicMode = value };
            foreach (SourceFolderViewModel folder in Folders)
            {
                folder.BasicMode = value;
            }
            Notify(null);
        }
    }

    /// <summary>
    /// Destination de l’interface Dune.
    /// </summary>
    public string Destination
    {
        get => _settings.Destination;
        set
        {
            _settings = _settings with { Destination = value };
            Notify(null);
        }
    }

    /// <summary>
    /// Articles ignorés pour le nouveau titre de tri.
    /// </summary>
    public bool IgnoreArticles
    {
        get => _settings.IgnoreArticles;
        set
        {
            _settings = _settings with
            {
                IgnoreArticles = value,
            };
            Notify(null);
        }
    }

    /// <summary>
    /// Liste des articles.
    /// </summary>
    public string Articles
    {
        get => _settings.Articles;
        set
        {
            _settings = _settings with { Articles = value };
            Notify(null);
        }
    }

    /// <summary>
    /// Sélection automatique des nouveaux films.
    /// </summary>
    public bool AutoSelect
    {
        get => _settings.AutoSelect;
        set
        {
            _settings = _settings with { AutoSelect = value };
            Notify(null);
        }
    }

    /// <summary>
    /// Sélection recevant les nouveaux films.
    /// </summary>
    public string AutoSelectName
    {
        get => _settings.AutoSelectName;
        set
        {
            _settings = _settings with
            {
                AutoSelectName = value,
            };
            Notify(null);
        }
    }

    /// <summary>
    /// Boîtiers des films.
    /// </summary>
    public bool UseCases
    {
        get => _settings.Cases;
        set
        {
            _settings = _settings with { Cases = value };
            Notify(null);
        }
    }

    /// <summary>
    /// Cadre de sélection Dune.
    /// </summary>
    public bool SelectionBox
    {
        get => _settings.SelectionBox;
        set
        {
            _settings = _settings with { SelectionBox = value };
            Notify(null);
        }
    }

    /// <summary>
    /// Barre de défilement Dune.
    /// </summary>
    public bool Scrollbar
    {
        get => _settings.Scrollbar;
        set
        {
            _settings = _settings with { Scrollbar = value };
            Notify(null);
        }
    }

    /// <summary>
    /// Fonds AAI.
    /// </summary>
    public bool AaiBackgrounds
    {
        get => _settings.Aai;
        set
        {
            _settings = _settings with { Aai = value };
            Notify(null);
        }
    }

    /// <summary>
    /// Qualité JPEG de 1 à 100.
    /// </summary>
    public int JpegQuality
    {
        get => _settings.JpegQuality;
        set
        {
            _settings = _settings with { JpegQuality = value };
            Notify(null);
        }
    }

    /// <summary>
    /// Période des nouveautés.
    /// </summary>
    public int LastDays
    {
        get => _settings.LastDays;
        set
        {
            _settings = _settings with { LastDays = value };
            Notify(null);
        }
    }

    /// <summary>
    /// Fichier de configuration TVDB.
    /// </summary>
    public string TvdbPath
    {
        get => _settings.TvdbPath;
        set
        {
            _settings = _settings with { TvdbPath = value };
            Notify(null);
        }
    }

    /// <summary>
    /// Position historique des options de base selon le mode.
    /// </summary>
    public double DatabaseTop => BasicMode ? 118 : 231;

    /// <summary>
    /// Complément de l'option AAI.
    /// </summary>
    public bool JpegBackgrounds
    {
        get => !AaiBackgrounds;
        set => AaiBackgrounds = !value;
    }

    /// <summary>
    /// Premier bit des catégories historiques.
    /// </summary>
    public bool RecentEnabled
    {
        get =>
            _settings.Categories.Length > 0
            && _settings.Categories[0] != '0';
        set
        {
            _settings = _settings with
            {
                Categories =
                    (value ? "1" : "0")
                    + _settings.Categories[1..],
            };
            Notify(null);
        }
    }

    /// <summary>
    /// Tri alphabétique (0) ou par date d'ajout (1).
    /// </summary>
    public int RecentSortIndex
    {
        get => _settings.SortRecentByDate ? 1 : 0;
        set
        {
            _settings = _settings with
            {
                SortRecentByDate = value == 1,
            };
            Notify(null);
        }
    }
}
