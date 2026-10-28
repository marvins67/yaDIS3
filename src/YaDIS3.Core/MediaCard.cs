using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>
/// Tracks an artwork replacement and its original file
/// hash.
/// </summary>
public sealed record StagedArtwork(
    string Target,
    string Temporary,
    string OriginalHash
);

/// <summary>
/// Edits a card while preserving unknown XML and technical
/// metadata.
/// </summary>
public sealed class MediaCard
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
    /// Gets the path to the card's data.xml file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the directory containing the card metadata and
    /// artwork.
    /// </summary>
    public string DataDirectory =>
        Path.GetDirectoryName(FilePath)!;

    private XElement Root => Document.Root!;

    private XElement Header =>
        Root.Name == "item" ? Root : Root.Element("item")!;

    private XElement? Info =>
        Root.Element("info") ?? Header.Element("info");

    /// <summary>
    /// Notifies bindings when the displayed state changes.
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
    /// Gets the original folder name stored in the card header.
    /// </summary>
    public string FolderName =>
        Header.Element("filename")!.Value;

    /// <summary>
    /// Indicates whether the current card represents a series.
    /// </summary>
    public bool IsSeries => Header.Element("type")?.Value == "1";

    /// <summary>
    /// Gets the localized film or series label.
    /// </summary>
    public string KindLabel => IsSeries ? "Série" : "Film";

    /// <summary>
    /// Indicates whether the card has unsaved metadata edits.
    /// </summary>
    public bool IsDirty => _revision != _savedRevision;

    /// <summary>
    /// Gets the title, falling back to the original folder
    /// name.
    /// </summary>
    public string DisplayName =>
        string.IsNullOrWhiteSpace(Title) ? FolderName : Title;

    /// <summary>
    /// Gets the series identifier stored in the metadata.
    /// </summary>
    public string TvdbId => Read("id");

    /// <summary>
    /// Gets or sets the displayed title.
    /// </summary>
    public string Title
    {
        get => Read("title", FolderName);
        set => Set("title", value);
    }

    /// <summary>
    /// Gets or sets the synopsis.
    /// </summary>
    public string Plot
    {
        get => Read("plot");
        set => Set("plot", value);
    }

    /// <summary>
    /// Gets or sets the release year.
    /// </summary>
    public string Year
    {
        get => Read("year");
        set => Set("year", value);
    }

    /// <summary>
    /// Gets or sets the stored rating text.
    /// </summary>
    public string Rating
    {
        get => Read("rating");
        set => Set("rating", value);
    }

    /// <summary>
    /// Gets or sets the original broadcaster name.
    /// </summary>
    public string Network
    {
        get => Read("network");
        set => Set("network", value);
    }

    /// <summary>
    /// Gets the TVDB episode order saved with the series.
    /// </summary>
    public string Order =>
        (string?)Root.Attribute("tvdb_order") ?? "official";

    /// <summary>
    /// Gets the card's episodes in their stored navigation
    /// order.
    /// </summary>
    public IReadOnlyList<Episode> Episodes => _episodes;

    /// <summary>
    /// Gets the staged or saved poster image path.
    /// </summary>
    public string PosterPath => ArtworkPath("poster.jpg");

    /// <summary>
    /// Gets the staged or saved banner image path.
    /// </summary>
    public string BannerPath => ArtworkPath("banner.jpg");

    /// <summary>
    /// Gets the staged or saved fanart image path.
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
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Releases owned resources and temporary artwork, where
    /// applicable.
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
    /// Updates series metadata while preserving manually edited
    /// episodes.
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

/// <summary>
/// Edits one episode within its owning media card.
/// </summary>
public sealed class Episode(MediaCard owner, XElement node)
    : INotifyPropertyChanged
{
    internal XElement Node => node;

    /// <summary>
    /// Notifies bindings when the displayed state changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    private static int Numeric(
        XElement element,
        string attribute
    )
    {
        return
            int.TryParse(
                (string?)element.Attribute(attribute),
                out var number
            )
            && number >= 0
            ? number
            : 0;
    }

    /// <summary>
    /// Gets the season number stored for an episode.
    /// </summary>
    public int Season => Numeric(node, "season");

    /// <summary>
    /// Gets the episode number within its season.
    /// </summary>
    public int Number => Numeric(node, "episode");

    /// <summary>
    /// Gets the episode's navigation key.
    /// </summary>
    public string Key =>
        (string?)node.Attribute("key")
        ?? $"{Season:00}_{Number:000}_000";

    /// <summary>
    /// Gets the display label for this selection.
    /// </summary>
    public string Label =>
        $"S{Season:00} · E{Number:00} — {Title}";

    /// <summary>
    /// Indicates whether metadata was edited manually.
    /// </summary>
    public bool IsManual =>
        (string?)node.Attribute("manual_edit") == "yes";

    private string Get(string field)
    {
        return node.Element(field)?.Value ?? "";
    }

    private void Set(string field, string? value)
    {
        if (Get(field) == (value ?? ""))
        {
            return;
        }

        owner.Update(() =>
        {
            MediaCard.Put(node, field, value);
            node.SetAttributeValue("manual_edit", "yes");
        });
    }

    /// <summary>
    /// Gets or sets the displayed title.
    /// </summary>
    public string Title
    {
        get => Get("title");
        set => Set("title", value);
    }

    /// <summary>
    /// Gets or sets the synopsis.
    /// </summary>
    public string Plot
    {
        get => Get("plot");
        set => Set("plot", value);
    }

    /// <summary>
    /// Gets or sets the episode's original air date.
    /// </summary>
    public string Aired
    {
        get => Get("aired");
        set => Set("aired", value);
    }

    /// <summary>
    /// Gets or sets the stored rating text.
    /// </summary>
    public string Rating
    {
        get => Get("rating");
        set => Set("rating", value);
    }

    /// <summary>
    /// Gets or sets the episode director names.
    /// </summary>
    public string Director
    {
        get => Get("director");
        set => Set("director", value);
    }

    /// <summary>
    /// Gets the local video path stored for the episode.
    /// </summary>
    public string VideoPath => Get("file");

    /// <summary>
    /// Gets the staged or saved episode thumbnail path.
    /// </summary>
    public string ImagePath =>
        owner.ArtworkPath($"{Season}x{Number}.jpg");

    internal void MarkImageManual()
    {
        MediaCard.Put(node, "image", "");
        node.SetAttributeValue(
            "manual_image",
            $"{Season}x{Number}.jpg"
        );
    }

    internal void Notify()
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(string.Empty)
        );
    }
}

/// <summary>
/// Encodes artwork URLs in the existing cache-compatible
/// format.
/// </summary>
public static class LegacyArtwork
{
    // Same reversible URL/cache scheme as the existing 2.4
    // bridge.

    /// <summary>
    /// Encodes an artwork URL in the legacy episode cache
    /// format.
    /// </summary>
    public static string Encode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        Uri url = Uri.TryCreate(
            value,
            UriKind.Absolute,
            out var absolute
        )
            ? absolute
            : new Uri(
                new Uri("https://artworks.thetvdb.com/"),
                value.TrimStart('/')
            );
        if (url.Scheme is not ("http" or "https"))
        {
            throw new InvalidDataException(
                "URL d'image invalide."
            );
        }

        byte[] bytes = Encoding.UTF8.GetBytes(url.AbsoluteUri);
        return $"episodes/v4url_{Convert.ToHexString(bytes)}/{Convert.ToHexString(SHA256.HashData(bytes).AsSpan(0, 8))}-1.jpg";
    }
}
