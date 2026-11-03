using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

public sealed class MediaTreatmentTests
{
    [Fact]
    public async Task TreatAsync_PremierFilm_IdentifieAnalyseEtRend()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(
            fixture,
            repository,
            series: false
        );
        TreatmentInputs inputs = new();
        Assert.True(
            await TreatAsync(fixture, repository, card, inputs)
        );
        Assert.Equal("Film identifié", card.Title);
        Assert.Equal(1, inputs.MovieRequests);
        Assert.Equal(1, inputs.TechnicalReads);
        Assert.Equal(
            MediaProcessingState.Unchecked,
            card.ProcessingState
        );
        Assert.True(
            File.Exists(
                Path.Combine(card.DataDirectory, "preview.jpg")
            )
        );
    }

    [Fact]
    public async Task TreatAsync_Retraitement_ConserveLesRetouchesEtAnalyseSeulementLesAjouts()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(
            fixture,
            repository,
            series: true
        );
        TreatmentInputs inputs = new();
        Assert.True(
            await TreatAsync(fixture, repository, card, inputs)
        );
        Assert.Equal(
            "Épisode identifié",
            Assert.Single(card.Episodes).Title
        );
        card.Title = "Titre corrigé";
        Assert.True(
            await TreatAsync(fixture, repository, card, inputs)
        );
        Assert.Equal("Titre corrigé", card.Title);
        Assert.Equal(1, inputs.TechnicalReads);
        Assert.Equal(2, inputs.SeriesRequests);
    }

    [Fact]
    public async Task TreatAsync_ReloadInformation_RechargeLesDonneesDuFournisseur()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(
            fixture,
            repository,
            series: false
        );
        TreatmentInputs inputs = new();
        Assert.True(
            await TreatAsync(fixture, repository, card, inputs)
        );
        card.Title = "Ancien titre";
        Assert.True(
            await TreatAsync(
                fixture,
                repository,
                card,
                inputs,
                TreatmentMode.ReloadInformation
            )
        );
        Assert.Equal("Film identifié", card.Title);
        Assert.Equal(2, inputs.MovieRequests);
    }

    [Fact]
    public async Task TreatAsync_ReloadTechnical_RelitLaVideoSansRechercheDeTitre()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(
            fixture,
            repository,
            series: false
        );
        TreatmentInputs inputs = new();
        Assert.True(
            await TreatAsync(fixture, repository, card, inputs)
        );
        Assert.True(
            await TreatAsync(
                fixture,
                repository,
                card,
                inputs,
                TreatmentMode.ReloadTechnicalInformation
            )
        );
        Assert.Equal(1, inputs.MovieRequests);
        Assert.Equal(2, inputs.TechnicalReads);
    }

    [Fact]
    public async Task TreatAsync_SerieIntrouvable_CreeUneFicheManuelleAvecEpisodesLocaux()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(
            fixture,
            repository,
            series: true
        );
        TreatmentInputs inputs = new() { SeriesNotFound = true };
        Assert.True(
            await TreatAsync(fixture, repository, card, inputs)
        );
        Assert.Equal("", card.TvdbId);
        Assert.Single(card.Episodes);
        Assert.Equal(
            "yes",
            (string?)
                card.Snapshot()
                    .Document.Root!.Element("info")!
                    .Attribute("manual_blank")
        );
        Assert.True(
            await TreatAsync(fixture, repository, card, inputs)
        );
        Assert.Equal(1, inputs.SeriesRequests);
        Assert.Equal(1, inputs.TechnicalReads);
    }

    [Fact]
    public async Task TreatAsync_PanneTvdb_NeCreePasDeFicheManuelleNiDeRendu()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(
            fixture,
            repository,
            series: true
        );
        TreatmentInputs inputs = new() { FailSearch = true };
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            TreatAsync(fixture, repository, card, inputs)
        );
        Assert.Null(
            card.Snapshot().Document.Root!.Element("info")
        );
        Assert.Equal(
            MediaProcessingState.Unprocessed,
            card.ProcessingState
        );
        Assert.False(
            File.Exists(
                Path.Combine(card.DataDirectory, "preview.jpg")
            )
        );
    }

    [Fact]
    public async Task TreatAsync_TousLesEpisodes_ForceLeRenduDuCache()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(
            fixture,
            repository,
            series: true
        );
        TreatmentInputs inputs = new() { SeriesNotFound = true };
        Assert.True(
            await TreatAsync(fixture, repository, card, inputs)
        );
        string cache = Path.Combine(
            fixture.Root,
            "interface-yaDIS3",
            new MediaContext(card).Id
        );
        string icon = Path.Combine(
            cache,
            "01",
            "01_001_000",
            "icon.aai"
        );
        string episodePreview = Path.Combine(
            cache,
            "01",
            "01_001_000",
            "preview.jpg"
        );
        Assert.Equal(
            File.ReadAllBytes(episodePreview),
            File.ReadAllBytes(
                Path.Combine(card.DataDirectory, "preview.jpg")
            )
        );
        byte[] marker = [1, 2, 3];
        File.WriteAllBytes(icon, marker);
        Assert.True(
            await TreatAsync(fixture, repository, card, inputs)
        );
        Assert.Equal(marker, File.ReadAllBytes(icon));
        Assert.True(
            await TreatAsync(
                fixture,
                repository,
                card,
                inputs,
                TreatmentMode.AllEpisodes
            )
        );
        Assert.False(
            marker.SequenceEqual(File.ReadAllBytes(icon))
        );
    }

    [Fact]
    public async Task TreatAsync_SourceAbsente_NeValidePasLeFilm()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(
            fixture,
            repository,
            series: false
        );
        File.Delete(
            Path.Combine(fixture.Root, "videos", card.FolderName)
        );
        TreatmentInputs inputs = new();
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () =>
                TreatAsync(fixture, repository, card, inputs)
        );
        Assert.Equal(0, inputs.MovieRequests);
        Assert.Equal(
            MediaProcessingState.Unprocessed,
            card.ProcessingState
        );
    }

    private static Task<bool> TreatAsync(
        ExportFixture fixture,
        WorkspaceRepository repository,
        MediaCard card,
        TreatmentInputs inputs,
        TreatmentMode mode = TreatmentMode.Normal
    )
    {
        TreatmentPreparation preparation = new(
            new ExportFileSystem(),
            inputs,
            inputs
        );
        MediaTreatment treatment = new(
            repository,
            preparation,
            new FixedTimeProvider()
        );
        return treatment.TreatAsync(
            card,
            ExportFixture.Template(),
            new RenderOptions(),
            fixture.Root,
            mode,
            new DuneConfiguration(),
            null,
            CancellationToken.None
        );
    }

    private static MediaCard PrepareCard(
        ExportFixture fixture,
        WorkspaceRepository repository,
        bool series
    )
    {
        string name = series
            ? ExportFixture.SeriesName
            : ExportFixture.MovieName;
        string file = Path.Combine(
            fixture.Root,
            "data",
            name,
            "data.xml"
        );
        XDocument xml = XDocument.Load(file);
        xml.Root!.Element("info")!.Remove();
        XElement item = xml.Root.Element("item")!;
        string source = Path.Combine(fixture.Root, "videos");
        Directory.CreateDirectory(source);
        item.SetElementValue("path", source);
        item.SetElementValue("status", "3");
        item.SetElementValue("folder", series ? "yes" : "no");
        string video = Path.Combine(source, name);
        if (series)
        {
            Directory.CreateDirectory(video);
            video = Path.Combine(video, "S01E01.mkv");
        }
        File.WriteAllBytes(video, []);
        xml.Save(file);
        return repository
            .Open(fixture.Root)
            .Cards.Single(card => card.IsSeries == series);
    }
}
