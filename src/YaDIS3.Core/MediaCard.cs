using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>
/// Édite une fiche en préservant le XML inconnu et les
/// informations techniques.
/// </summary>
public sealed partial class MediaCard
    : INotifyPropertyChanged,
        IDisposable
{
    private long _revision;

    private long _savedRevision;

    private readonly Dictionary<string, StagedArtwork> _artwork =
        new(StringComparer.Ordinal);

    private IReadOnlyList<Episode> _episodes = [];

    internal XDocument Document { get; }

    internal string SourceHash { get; private set; }

    /// <summary>
    /// Chemin du fichier data.xml de la fiche.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Dossier contenant les métadonnées et les illustrations de
    /// la fiche.
    /// </summary>
    public string DataDirectory =>
        Path.GetDirectoryName(FilePath)!;

    private XElement Root => Document.Root!;

    private XElement Header =>
        Root.Name == "item" ? Root : Root.Element("item")!;

    /// <summary>Présence de métadonnées éditables.</summary>
    public bool HasInformation => Info is not null;

    private XElement? Info =>
        Root.Element("info") ?? Header.Element("info");

    /// <summary>
    /// Signale aux liaisons de données que l'état affiché a
    /// changé.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    internal MediaCard(
        string file,
        XDocument document,
        string hash
    )
    {
        FilePath = file;
        Document = document;
        SourceHash = hash;
        if (
            document.Root is null
            || Root.Name != "data" && Root.Name != "item"
        )
        {
            throw new InvalidDataException(
                "Racine XML yaDIS3 inconnue."
            );
        }

        if (Header?.Element("filename") is null)
        {
            throw new InvalidDataException(
                "Fiche sans bloc item/filename."
            );
        }

        RefreshEpisodes();
    }

    /// <summary>
    /// Nom du dossier d'origine conservé dans l'en-tête.
    /// </summary>
    public string FolderName =>
        Header.Element("filename")!.Value;

    /// <summary>
    /// Indique si la fiche représente une série.
    /// </summary>
    public bool IsSeries => Header.Element("type")?.Value == "1";

    /// <summary>
    /// Libellé localisé du type de média, film ou série.
    /// </summary>
    public string KindLabel => IsSeries ? "Série" : "Film";

    /// <summary>
    /// Indique si la fiche contient des modifications non
    /// enregistrées.
    /// </summary>
    public bool IsDirty => _revision != _savedRevision;

    /// <summary>
    /// Statut historique, indépendant de l'aperçu et des
    /// modifications non enregistrées.
    /// </summary>
    public MediaProcessingState ProcessingState =>
        SourceUnavailable
            ? MediaProcessingState.Missing
            : StoredProcessingState;

    internal bool SourceUnavailable { get; set; }

    internal string SourceDirectory =>
        Header.Element("path")?.Value ?? "";

    /// <summary>Localise le média selon l'indicateur folder de yaDIS2.</summary>
    public string MediaFolderPath =>
        Header.Element("folder")?.Value == "yes"
            ? SourceDirectory
            : Path.Combine(SourceDirectory, FolderName);

    /// <summary>Adresse de la fiche du film ou de la série.</summary>
    public string InformationUrl =>
        IsSeries
            ? "http://thetvdb.com/?tab=series&id="
                + TvdbId
            : Read("url");


    private MediaProcessingState StoredProcessingState =>
        int.TryParse(
            Header.Element("status")?.Value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int status
        ) && Enum.IsDefined(typeof(MediaProcessingState), status)
            ? (MediaProcessingState)status
            : MediaProcessingState.Unknown;

    /// <summary>
    /// Titre de la fiche ou, à défaut, nom du dossier d'origine.
    /// </summary>
    public string DisplayName =>
        string.IsNullOrWhiteSpace(Title) ? FolderName : Title;

    /// <summary>
    /// Identifiant de série conservé dans les métadonnées.
    /// </summary>
    public string TvdbId => Read("id");

    /// <summary>
    /// Titre affiché.
    /// </summary>
    public string Title
    {
        get => Read("title", FolderName);
        set => Set("title", value);
    }

    /// <summary>
    /// Synopsis.
    /// </summary>
    public string Plot
    {
        get => Read("plot");
        set => Set("plot", value);
    }

    /// <summary>
    /// Année de sortie.
    /// </summary>
    public string Year
    {
        get => Read("year");
        set => Set("year", value);
    }

    /// <summary>
    /// Note conservée sous sa forme textuelle.
    /// </summary>
    public string Rating
    {
        get => Read("rating");
        set => Set("rating", value);
    }

    /// <summary>
    /// Nom du diffuseur d'origine.
    /// </summary>
    public string Network
    {
        get => Read("network");
        set => Set("network", value);
    }

    /// <summary>
    /// Ordre des épisodes TVDB enregistré pour cette série.
    /// </summary>
    public string Order =>
        (string?)Root.Attribute("tvdb_order") ?? "official";

    /// <summary>
    /// Épisodes classés dans leur ordre de navigation
    /// enregistré.
    /// </summary>
    public IReadOnlyList<Episode> Episodes => _episodes;

    /// <summary>
    /// Chemin de l'affiche importée ou enregistrée.
    /// </summary>
    public string PosterPath => ArtworkPath("poster.jpg");

    /// <summary>
    /// Chemin de la bannière importée ou enregistrée.
    /// </summary>
    public string BannerPath => ArtworkPath("banner.jpg");

    /// <summary>
    /// Chemin du fanart importé ou enregistré.
    /// </summary>
    public string FanartPath => ArtworkPath("fanart.jpg");

    private string Read(string field, string fallback = "")
    {
        return Info?.Element(field)?.Value ?? fallback;
    }

    private void Set(string field, string? value)
    {
        value ??= "";
        if (
            Read(field) == value
            && Info?.Element(field) is not null
        )
        {
            return;
        }

        Update(() => Put(EnsureInfo(), field, value));
    }

    internal static void Put(
        XElement node,
        string field,
        string? value
    )
    {
        if (node.Element(field) is { } existing)
        {
            existing.Value = value ?? "";
        }
        else
        {
            node.Add(new XElement(field, value ?? ""));
        }
    }

    /// <summary>
    /// Établit le tri des nouveaux films, sans réécrire le
    /// classement existant.
    /// </summary>
    public void EnsureMovieSortTitle(
        DuneConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (
            IsSeries
            || !string.IsNullOrEmpty(
                Info?.Element("sort_title")?.Value
            )
        )
        {
            return;
        }
        Update(() =>
        {
            XElement info = EnsureInfo();
            Put(
                info,
                "sort_title",
                LegacySortTitle.Create(
                    info.Element("title")?.Value ?? FolderName,
                    configuration
                )
            );
        });
    }

    internal XElement EnsureInfo()
    {
        if (Info is { } info)
        {
            return info;
        }

        info = new XElement(
            "info",
            new XAttribute("manual_blank", "yes")
        );
        foreach (
            string field in new[]
            {
                "id",
                "imdb",
                "title",
                "sort_title",
                "plot",
                "year",
                "rating",
                "genres",
                "network",
                "actors",
                "images",
            }
        )
        {
            info.Add(
                new XElement(
                    field,
                    field is "title" or "sort_title"
                        ? FolderName
                        : ""
                )
            );
        }

        Root.Add(info);
        return info;
    }

    internal void Update(Action action)
    {
        action();
        if (Header.Element("status")?.Value == "0")
        {
            Header.Element("status")!.Value = "1";
        }

        if (IsSeries)
        {
            EnsureInfo()
                .SetAttributeValue(
                    "episode_edits_pending",
                    "yes"
                );
        }

        _revision++;
        Notify();
    }

    internal void RefreshEpisodes()
    {
        _episodes = (Info?.Elements("episode") ?? [])
            .OrderBy(
                element => (string?)element.Attribute("key"),
                StringComparer.Ordinal
            )
            .Select(element => new Episode(this, element))
            .ToArray();
        Notify();
    }

    /// <summary>
    /// Copies the card XML together with its current edit
    /// revision.
    /// </summary>
    public (XDocument Document, long Revision) Snapshot()
    {
        return (new XDocument(Document), _revision);
    }

    /// <summary>
    /// Reprend le statut et la date appliqués après un
    /// traitement réussi par yaDIS2.
    /// </summary>
    public void MarkProcessed(DateOnly processingDate)
    {
        string? previousStatus = Header.Element("status")?.Value;
        string? previousDate = Header.Element("date")?.Value;
        // ProcessMovie et ProcessTVShow conservent le statut zéro.
        if (
            int.TryParse(
                previousStatus,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int status
            )
            && status > 0
        )
        {
            Put(Header, "status", "1");
        }
        if (string.IsNullOrEmpty(previousDate))
        {
            Put(
                Header,
                "date",
                processingDate.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture
                )
            );
        }
        if (
            previousStatus != Header.Element("status")?.Value
            || previousDate != Header.Element("date")?.Value
        )
        {
            _revision++;
            Notify();
        }
    }

    /// <summary>Inverse uniquement les statuts 0 et 1, comme yaDIS2.</summary>
    public bool ToggleVerification()
    {
        if (ProcessingState is not (
            MediaProcessingState.Checked
            or MediaProcessingState.Unchecked
        ))
        {
            return false;
        }

        string next =
            ProcessingState == MediaProcessingState.Checked
                ? "1"
                : "0";
        Put(Header, "status", next);
        _revision++;
        Notify();
        return true;
    }

    /// <summary>Prépare une fiche vierge pour la création manuelle.</summary>
    public void BeginManualMovieEdit()
    {
        if (
            IsSeries
            || ProcessingState != MediaProcessingState.Unprocessed
            || HasInformation
        )
        {
            return;
        }
        Update(() => EnsureInfo());
    }

    internal void MarkSaved(long atRevision, string hash)
    {
        SourceHash = hash;
        _savedRevision = atRevision;
        Notify();
    }

    internal string ArtworkPath(string name)
    {
        string target = Path.Combine(DataDirectory, name);
        return _artwork.TryGetValue(target, out var staged)
            ? staged.Temporary
            : target;
    }

    internal void StageImage(string filename, string temporary)
    {
        string target = Path.Combine(DataDirectory, filename);
        string originalHash = _artwork.TryGetValue(
            target,
            out var previous
        )
            ? previous.OriginalHash
            : WorkspaceRepository.FileHash(target);
        _artwork[target] = new StagedArtwork(
            target,
            temporary,
            originalHash
        );
        if (previous is not null)
        {
            TryDelete(previous.Temporary);
        }
    }

    internal IReadOnlyList<StagedArtwork> PendingArtwork()
    {
        return _artwork.Values.ToArray();
    }

    internal void CompleteArtwork(
        IReadOnlyList<StagedArtwork> saved
    )
    {
        foreach (StagedArtwork item in saved)
        {
            if (
                _artwork.TryGetValue(
                    item.Target,
                    out var current
                )
                && current == item
            )
            {
                _artwork.Remove(item.Target);
                TryDelete(item.Temporary);
            }
        }
    }

    internal static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        // Un échec de nettoyage ne doit pas masquer l'erreur de sauvegarde initiale.
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
            )
        {
            Trace.TraceWarning(
                "Nettoyage du fichier temporaire impossible : {0}. {1}",
                file,
                exception.Message
            );
        }
    }

    /// <summary>
    /// Libère les ressources possédées et les illustrations
    /// temporaires.
    /// </summary>
    public void Dispose()
    {
        foreach (StagedArtwork item in _artwork.Values)
        {
            TryDelete(item.Temporary);
        }

        _artwork.Clear();
    }

    internal void Notify()
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(string.Empty)
        );
        foreach (Episode episode in _episodes)
        {
            episode.Notify();
        }
    }

    /// <summary>
    /// Actualise la série en préservant les épisodes modifiés
    /// manuellement.
    /// </summary>
    public void ApplyTvdb(SeriesDetails series, string order)
    {
        if (!IsSeries)
        {
            throw new InvalidOperationException(
                "Sélectionnez une série."
            );
        }

        TvdbClient.ValidateOrder(order);
        Update(() =>
        {
            XElement info = EnsureInfo();
            Root.SetAttributeValue("tvdb_order", order);
            Put(
                info,
                "id",
                series.Id.ToString(CultureInfo.InvariantCulture)
            );
            Put(info, "title", series.Name);
            Put(info, "sort_title", series.Name);
            Put(info, "plot", series.Overview);
            Put(info, "network", series.Network);
            Put(info, "rating", series.Rating);
            ReplaceSeriesNames(info, "genres", "genre", series.Genres);
            ReplaceSeriesNames(info, "actors", "actor", series.Actors);
            Put(
                info,
                "year",
                series.FirstAired.Length >= 4
                    ? series.FirstAired[..4]
                    : ""
            );
            var byNumber = series
                .Episodes.GroupBy(element =>
                    (element.Season, element.Number)
                )
                .ToDictionary(
                    group => group.Key,
                    group => group.First()
                );
            foreach (Episode episode in _episodes)
            {
                if (
                    episode.IsManual
                    || !byNumber.TryGetValue(
                        (episode.Season, episode.Number),
                        out var match
                    )
                )
                {
                    continue;
                }

                Put(episode.Node, "title", match.Name);
                Put(episode.Node, "plot", match.Overview);
                Put(episode.Node, "aired", match.Aired);
                Put(episode.Node, "rating", match.Rating);
                Put(episode.Node, "director", match.Director);
                if (
                    episode.Node.Attribute("manual_image")
                    is null
                )
                {
                    Put(
                        episode.Node,
                        "image",
                        LegacyArtwork.Encode(match.Image)
                    );
                }
            }
        });
    }
}
