using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

/// <summary>
/// Vérifie que la publication du dernier traitement ne conserve
/// pas les images d'épisodes supprimés.
/// </summary>
public sealed class LegacyProcessedMediaPublisherTests
{
    [Fact]
    public void Publish_EpisodeSupprime_RetireSonAncienDossier()
    {
        using ExportFixture fixture = new();
        MediaContext media = fixture.Cards().Single(card => card.IsSeries);
        string cache = Path.Combine(
            fixture.Root,
            "interface-yaDIS3",
            media.Id
        );
        string episode = Path.Combine(cache, "01", "01_000_001");
        Directory.CreateDirectory(episode);
        File.WriteAllBytes(Path.Combine(episode, "icon.aai"), [1, 2, 3]);
        string output = Path.Combine(
            fixture.Root,
            "interface",
            "01",
            ExportFixture.SeriesName
        );

        LegacyProcessedMediaPublisher.Publish(media, fixture.Root, cache);
        string exportedEpisode = Path.Combine(output, "01", "01_000_001");
        Assert.True(File.Exists(Path.Combine(exportedEpisode, "icon.aai")));
        File.WriteAllText(Path.Combine(output, "dune_folder.txt"), "menu");
        Directory.Delete(episode, true);

        LegacyProcessedMediaPublisher.Publish(media, fixture.Root, cache);

        Assert.False(Directory.Exists(exportedEpisode));
        Assert.True(File.Exists(Path.Combine(output, "dune_folder.txt")));
    }

    [Fact]
    public void Publish_ImageBloquee_IgnoreUnDossierQuiPorteSonNom()
    {
        using ExportFixture fixture = new();
        MediaContext media = fixture.Cards().Single(card => card.IsSeries);
        string cache = Path.Combine(
            fixture.Root,
            "interface-yaDIS3",
            media.Id
        );
        Directory.CreateDirectory(Path.Combine(cache, "icon.aai"));

        LegacyProcessedMediaPublisher.Publish(media, fixture.Root, cache);

        Assert.False(Directory.Exists(
            Path.Combine(
                fixture.Root,
                "interface",
                "01",
                ExportFixture.SeriesName,
                "icon.aai"
            )
        ));
    }
}
