namespace YaDIS3.Core;
public enum ArtworkKind { Poster, Banner, Fanart, Episode }
public interface IJpegEncoder { void Encode(string source, string target); }
public sealed class ImageImporter(IJpegEncoder encoder)
{
    public void Import(MediaCard card, ArtworkKind kind, string source, Episode? episode = null)
    {
        if (kind == ArtworkKind.Episode && (episode is null || !card.Episodes.Contains(episode))) throw new ArgumentException("Épisode absent de cette fiche.");
        var name = kind switch { ArtworkKind.Poster => "poster.jpg", ArtworkKind.Banner => "banner.jpg", ArtworkKind.Fanart => "fanart.jpg", ArtworkKind.Episode => $"{episode!.Season}x{episode.Number}.jpg", _ => throw new ArgumentOutOfRangeException(nameof(kind)) };
        var temporary = Path.Combine(Path.GetTempPath(), "yadis3-artwork-" + Guid.NewGuid().ToString("N") + ".jpg");
        try
        {
            encoder.Encode(source, temporary);
            card.StageImage(name, temporary);
            card.Update(() => { if (kind == ArtworkKind.Episode) episode!.MarkImageManual(); });
        }
        catch { MediaCard.TryDelete(temporary); throw; }
    }
}
