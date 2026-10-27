using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace YaDIS3.Core;

public sealed record StagedArtwork(string Target, string Temporary, string OriginalHash);

/// <summary>A draft of a legacy card. Unknown XML, media paths and technical data survive edits.</summary>
public sealed class MediaCard : INotifyPropertyChanged, IDisposable
{
    private long revision, savedRevision;
    private readonly Dictionary<string, StagedArtwork> artwork = new(StringComparer.Ordinal);
    private IReadOnlyList<Episode> episodes = [];
    internal XDocument Document { get; }
    internal string SourceHash { get; private set; }
    public string FilePath { get; }
    public string DataDirectory => Path.GetDirectoryName(FilePath)!;
    private XElement Root => Document.Root!;
    private XElement Header => Root.Name == "item" ? Root : Root.Element("item")!;
    private XElement? Info => Root.Element("info") ?? Header.Element("info");
    public event PropertyChangedEventHandler? PropertyChanged;

    internal MediaCard(string file, XDocument document, string hash)
    {
        FilePath = file; Document = document; SourceHash = hash;
        if (document.Root is null || Root.Name != "data" && Root.Name != "item") throw new InvalidDataException("Racine XML yaDIS3 inconnue.");
        if (Header?.Element("filename") is null) throw new InvalidDataException("Fiche sans bloc item/filename.");
        RefreshEpisodes();
    }
    public string FolderName => Header.Element("filename")!.Value;
    public bool IsSeries => Header.Element("type")?.Value == "1";
    public string KindLabel => IsSeries ? "Série" : "Film";
    public bool IsDirty => revision != savedRevision;
    public string DisplayName => string.IsNullOrWhiteSpace(Title) ? FolderName : Title;
    public string TvdbId => Read("id");
    public string Title { get => Read("title", FolderName); set => Set("title", value); }
    public string Plot { get => Read("plot"); set => Set("plot", value); }
    public string Year { get => Read("year"); set => Set("year", value); }
    public string Rating { get => Read("rating"); set => Set("rating", value); }
    public string Network { get => Read("network"); set => Set("network", value); }
    public string Order => (string?)Root.Attribute("tvdb_order") ?? "official";
    public IReadOnlyList<Episode> Episodes => episodes;
    public string PosterPath => ArtworkPath("poster.jpg");
    public string BannerPath => ArtworkPath("banner.jpg");
    public string FanartPath => ArtworkPath("fanart.jpg");
    private string Read(string field, string fallback = "") => Info?.Element(field)?.Value ?? fallback;
    private void Set(string field, string? value)
    {
        value ??= "";
        if (Read(field) == value && Info?.Element(field) is not null) return;
        Update(() => Put(EnsureInfo(), field, value));
    }
    internal static void Put(XElement node, string field, string? value)
    {
        if (node.Element(field) is { } existing) existing.Value = value ?? "";
        else node.Add(new XElement(field, value ?? ""));
    }
    internal XElement EnsureInfo()
    {
        if (Info is { } info) return info;
        info = new XElement("info", new XAttribute("manual_blank", "yes"));
        foreach (var field in new[] { "id", "imdb", "title", "sort_title", "plot", "year", "rating", "genres", "network", "actors", "images" })
            info.Add(new XElement(field, field is "title" or "sort_title" ? FolderName : ""));
        Root.Add(info); return info;
    }
    internal void Update(Action action)
    {
        action();
        if (Header.Element("status")?.Value == "0") Header.Element("status")!.Value = "1";
        if (IsSeries) EnsureInfo().SetAttributeValue("episode_edits_pending", "yes");
        revision++; Notify();
    }
    internal void RefreshEpisodes()
    {
        episodes = (Info?.Elements("episode") ?? []).OrderBy(e => (string?)e.Attribute("key"), StringComparer.Ordinal).Select(e => new Episode(this, e)).ToArray();
        Notify();
    }
    public (XDocument Document, long Revision) Snapshot() => (new XDocument(Document), revision);
    internal void MarkSaved(long atRevision, string hash) { SourceHash = hash; savedRevision = atRevision; Notify(); }
    internal string ArtworkPath(string name)
    {
        var target = Path.Combine(DataDirectory, name);
        return artwork.TryGetValue(target, out var staged) ? staged.Temporary : target;
    }
    internal void StageImage(string filename, string temporary)
    {
        var target = Path.Combine(DataDirectory, filename);
        var originalHash = artwork.TryGetValue(target, out var previous) ? previous.OriginalHash : WorkspaceRepository.FileHash(target);
        artwork[target] = new StagedArtwork(target, temporary, originalHash);
        if (previous is not null) TryDelete(previous.Temporary);
    }
    internal IReadOnlyList<StagedArtwork> PendingArtwork() => artwork.Values.ToArray();
    internal void CompleteArtwork(IReadOnlyList<StagedArtwork> saved)
    {
        foreach (var item in saved)
            if (artwork.TryGetValue(item.Target, out var current) && current == item)
            { artwork.Remove(item.Target); TryDelete(item.Temporary); }
    }
    internal static void TryDelete(string file)
    {
        try { File.Delete(file); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    public void Dispose() { foreach (var item in artwork.Values) TryDelete(item.Temporary); artwork.Clear(); }
    internal void Notify()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        foreach (var ep in episodes) ep.Notify();
    }
    public void ApplyTvdb(SeriesDetails series, string order)
    {
        if (!IsSeries) throw new InvalidOperationException("Sélectionnez une série.");
        TvdbClient.ValidateOrder(order);
        Update(() =>
        {
            var info = EnsureInfo(); Root.SetAttributeValue("tvdb_order", order);
            Put(info, "id", series.Id.ToString(CultureInfo.InvariantCulture));
            Put(info, "title", series.Name); Put(info, "sort_title", series.Name);
            Put(info, "plot", series.Overview); Put(info, "network", series.Network);
            Put(info, "year", series.FirstAired.Length >= 4 ? series.FirstAired[..4] : "");
            var byNumber = series.Episodes.GroupBy(e => (e.Season, e.Number)).ToDictionary(g => g.Key, g => g.First());
            foreach (var ep in episodes)
            {
                if (ep.IsManual || !byNumber.TryGetValue((ep.Season, ep.Number), out var match)) continue;
                Put(ep.Node, "title", match.Name); Put(ep.Node, "plot", match.Overview); Put(ep.Node, "aired", match.Aired);
                if (ep.Node.Attribute("manual_image") is null) Put(ep.Node, "image", LegacyArtwork.Encode(match.Image));
            }
        });
    }
}

public sealed class Episode(MediaCard owner, XElement node) : INotifyPropertyChanged
{
    internal XElement Node => node;
    public event PropertyChangedEventHandler? PropertyChanged;
    private static int Numeric(XElement e, string attribute) => int.TryParse((string?)e.Attribute(attribute), out var n) && n >= 0 ? n : 0;
    public int Season => Numeric(node, "season");
    public int Number => Numeric(node, "episode");
    public string Key => (string?)node.Attribute("key") ?? $"{Season:00}_{Number:000}_000";
    public string Label => $"S{Season:00} · E{Number:00} — {Title}";
    public bool IsManual => (string?)node.Attribute("manual_edit") == "yes";
    private string Get(string field) => node.Element(field)?.Value ?? "";
    private void Set(string field, string? value)
    {
        if (Get(field) == (value ?? "")) return;
        owner.Update(() => { MediaCard.Put(node, field, value); node.SetAttributeValue("manual_edit", "yes"); });
    }
    public string Title { get => Get("title"); set => Set("title", value); }
    public string Plot { get => Get("plot"); set => Set("plot", value); }
    public string Aired { get => Get("aired"); set => Set("aired", value); }
    public string Rating { get => Get("rating"); set => Set("rating", value); }
    public string Director { get => Get("director"); set => Set("director", value); }
    public string VideoPath => Get("file");
    public string ImagePath => owner.ArtworkPath($"{Season}x{Number}.jpg");
    internal void MarkImageManual() { MediaCard.Put(node, "image", ""); node.SetAttributeValue("manual_image", $"{Season}x{Number}.jpg"); }
    internal void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}

public static class LegacyArtwork
{
    // Same reversible URL/cache scheme as the existing 2.4 bridge.
    public static string Encode(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var url = Uri.TryCreate(value, UriKind.Absolute, out var absolute) ? absolute : new Uri(new Uri("https://artworks.thetvdb.com/"), value.TrimStart('/'));
        if (url.Scheme is not ("http" or "https")) throw new InvalidDataException("URL d'image invalide.");
        var bytes = Encoding.UTF8.GetBytes(url.AbsoluteUri);
        return $"episodes/v4url_{Convert.ToHexString(bytes)}/{Convert.ToHexString(SHA256.HashData(bytes).AsSpan(0, 8))}-1.jpg";
    }
}
