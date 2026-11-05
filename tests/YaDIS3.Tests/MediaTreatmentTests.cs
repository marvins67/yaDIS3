using SkiaSharp;
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
        using (SKBitmap marked = AaiCodec.Load(icon))
        {
            marked.Erase(SKColors.Red);
            AaiCodec.Save(marked, icon);
        }
        byte[] marker = File.ReadAllBytes(icon);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TreatAsync_EpisodeSupprime_RetireXmlMiniatureEtRendus(bool keepSecond)
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(fixture, repository, series: true);
        if (keepSecond)
        {
            File.WriteAllBytes(Path.Combine(fixture.Root, "videos", card.FolderName, "S01E02.mkv"), []);
        }
        TreatmentInputs inputs = new() { SeriesNotFound = true };
        Assert.True(await TreatAsync(fixture, repository, card, inputs));
        string thumbnail = Path.Combine(card.DataDirectory, "1x1.jpg");
        File.WriteAllBytes(thumbnail, [1, 2, 3]);
        string legacy = Path.Combine(fixture.Root, "interface", "01", card.FolderName, "01", "01_001_000");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "marker.txt"), "ancien rendu");
        string cache = Path.Combine(fixture.Root, "interface-yaDIS3", new MediaContext(card).Id, "01", "01_001_000");
        Assert.True(Directory.Exists(cache));
        File.Delete(Path.Combine(fixture.Root, "videos", card.FolderName, "S01E01.mkv"));
        Assert.Equal(keepSecond, await TreatAsync(fixture, repository, card, inputs));
        Assert.Equal(keepSecond ? 1 : 0, card.Episodes.Count);
        Assert.DoesNotContain(XDocument.Load(card.FilePath).Root!.Element("info")!.Elements("episode"),
            item => (string?)item.Attribute("episode") == "1");
        Assert.False(File.Exists(thumbnail));
        Assert.False(Directory.Exists(legacy));
        Assert.False(Directory.Exists(cache));
    }

    [Fact]
    public async Task TreatAsync_SourceSerieIndisponible_ConserveEpisodesEtMiniatures()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(fixture, repository, series: true);
        TreatmentInputs inputs = new() { SeriesNotFound = true };
        Assert.True(await TreatAsync(fixture, repository, card, inputs));
        XElement before = new(card.Snapshot().Document.Root!.Element("info")!.Element("episode")!);
        string source = Path.Combine(fixture.Root, "videos", card.FolderName);
        Directory.Move(source, source + ".offline");
        string thumbnail = Path.Combine(card.DataDirectory, "1x1.jpg");
        File.WriteAllBytes(thumbnail, [1, 2, 3]);
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => TreatAsync(fixture, repository, card, inputs));
        Assert.True(XNode.DeepEquals(before, card.Snapshot().Document.Root!.Element("info")!.Element("episode")));
        Assert.True(File.Exists(thumbnail));
    }

    [Theory]
    [InlineData("aired")]
    [InlineData("dvd")]
    [InlineData("absolute")]
    public async Task PrepareAsync_EpisodeDeplace_ConserveRetouchesCleEtOrdre(string order)
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(fixture, repository, series: true);
        TreatmentInputs inputs = new() { SeriesNotFound = true };
        TreatmentPreparation preparation = new(new ExportFileSystem(), inputs, inputs);
        Assert.True(await preparation.PrepareAsync(card, TreatmentMode.Normal, CancellationToken.None));
        card.ApplyTvdb(new SeriesDetails(42, "Série", "", "", "", []), order);
        card.Episodes[0].Title = "Titre corrigé";
        XElement before = new(card.Snapshot().Document.Root!.Element("info")!.Element("episode")!);
        string original = before.Element("file")!.Value;
        string directory = Path.Combine(Path.GetDirectoryName(original)!, "Season 1");
        Directory.CreateDirectory(directory);
        string renamed = Path.Combine(directory, "Nouveau.S01E01.m4v");
        File.Move(original, renamed);
        Assert.True(await preparation.PrepareAsync(card, TreatmentMode.Normal, CancellationToken.None));
        XElement after = card.Snapshot().Document.Root!.Element("info")!.Element("episode")!;
        before.SetElementValue("file", renamed);
        Assert.Equal(1, inputs.TechnicalReads);
        Assert.True(XNode.DeepEquals(before, after));
        Assert.Equal(order, card.Order);
    }

    [Fact]
    public async Task PrepareAsync_SourceDeconnecteePendantRecherche_NeSupprimeAucunEpisode()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(fixture, repository, series: true);
        TreatmentInputs initial = new();
        TreatmentPreparation preparation = new(new ExportFileSystem(), initial, initial);
        Assert.True(await preparation.PrepareAsync(card, TreatmentMode.Normal, CancellationToken.None));
        XElement before = new(card.Snapshot().Document.Root!.Element("info")!);
        string source = Path.Combine(fixture.Root, "videos", card.FolderName);
        TreatmentInputs disconnected = new()
        {
            DuringSeriesSearch = () => Directory.Move(source, source + ".offline"),
        };
        preparation = new(new ExportFileSystem(), disconnected, disconnected);
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            preparation.PrepareAsync(card, TreatmentMode.Normal, CancellationToken.None));
        Assert.True(XNode.DeepEquals(before, card.Snapshot().Document.Root!.Element("info")));
    }

    [Fact]
    public async Task TreatAsync_FondModifie_RecomposeApercuAvecIconeExistante()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(fixture, repository, series: true);
        TreatmentInputs inputs = new() { SeriesNotFound = true };
        Assert.True(await TreatAsync(fixture, repository, card, inputs));
        string preview = Path.Combine(card.DataDirectory, "preview.jpg");
        byte[] oldPreview = File.ReadAllBytes(preview);
        WriteRedImage(card.FanartPath);

        Assert.True(await TreatAsync(fixture, repository, card, inputs));
        Assert.False(oldPreview.SequenceEqual(File.ReadAllBytes(preview)));
    }

    [Fact]
    public async Task TreatAsync_EchecRendu_ConserveApercuEtPermetReprise()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(fixture, repository, series: true);
        TreatmentInputs inputs = new() { SeriesNotFound = true };
        Assert.True(await TreatAsync(fixture, repository, card, inputs));
        string preview = Path.Combine(card.DataDirectory, "preview.jpg");
        byte[] oldPreview = File.ReadAllBytes(preview);
        File.WriteAllBytes(card.FanartPath, [1, 2, 3]);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            TreatAsync(fixture, repository, card, inputs));
        Assert.Equal(oldPreview, File.ReadAllBytes(preview));
        Assert.Equal("3", XDocument.Load(card.FilePath).Root!
            .Element("item")!.Element("status")!.Value);

        File.Delete(card.FanartPath);
        Assert.True(await TreatAsync(fixture, repository, card, inputs));
        Assert.Equal("1", XDocument.Load(card.FilePath).Root!
            .Element("item")!.Element("status")!.Value);
    }

    [Fact]
    public async Task TreatAsync_PremierEpisodeXml_ApercuSuitCetEpisode()
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(fixture, repository, series: true);
        string source = Path.Combine(fixture.Root, "videos", card.FolderName);
        string original = Path.Combine(source, "S01E01.mkv");
        File.Move(original, Path.Combine(source, "S01E02.mkv"));
        TreatmentInputs inputs = new() { SeriesNotFound = true };
        Assert.True(await TreatAsync(fixture, repository, card, inputs));

        File.WriteAllBytes(original, []);
        Assert.True(await TreatAsync(fixture, repository, card, inputs));
        string cache = Path.Combine(fixture.Root, "interface-yaDIS3", new MediaContext(card).Id, "01");
        string preview = Path.Combine(card.DataDirectory, "preview.jpg");
        string second = Path.Combine(cache, "01_002_000", "preview.jpg");
        Assert.Equal(File.ReadAllBytes(second), File.ReadAllBytes(preview));

        string moved = Path.Combine(source, "Nouveau.S01E02.m4v");
        File.Move(Path.Combine(source, "S01E02.mkv"), moved);
        Assert.True(await TreatAsync(fixture, repository, card, inputs));
        Assert.Equal(File.ReadAllBytes(second), File.ReadAllBytes(preview));

        File.Delete(moved);
        Assert.True(await TreatAsync(fixture, repository, card, inputs));
        Assert.Equal(File.ReadAllBytes(Path.Combine(cache, "01_001_000", "preview.jpg")),
            File.ReadAllBytes(preview));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TreatAsync_RetoucheEnregistree_RegenereIconeEpisode(bool artwork)
    {
        using ExportFixture fixture = new();
        using WorkspaceRepository repository = new();
        using MediaCard card = PrepareCard(fixture, repository, series: true);
        TreatmentInputs inputs = new() { SeriesNotFound = true };
        Assert.True(await TreatAsync(fixture, repository, card, inputs));
        string icon = Path.Combine(fixture.Root, "interface-yaDIS3",
            new MediaContext(card).Id, "01", "01_001_000", "icon.aai");
        byte[] original = File.ReadAllBytes(icon);
        Episode episode = Assert.Single(card.Episodes);
        if (artwork)
        {
            string source = Path.Combine(fixture.Root, "manual.jpg");
            WriteRedImage(source);
            new ImageImporter(new CopyArtworkEncoder()).Import(
                card, ArtworkKind.Episode, source, episode);
        }
        else
        {
            episode.Title = "Titre retouché";
        }
        await repository.SaveAsync(card);
        Assert.True(await TreatAsync(fixture, repository, card, inputs));
        Assert.False(original.SequenceEqual(File.ReadAllBytes(icon)));
        Assert.False(card.NeedsEpisodeRerender);
    }

    private static void WriteRedImage(string path)
    {
        using SKBitmap bitmap = new(128, 72);
        bitmap.Erase(SKColors.Red);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        File.WriteAllBytes(path, jpeg.ToArray());
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
