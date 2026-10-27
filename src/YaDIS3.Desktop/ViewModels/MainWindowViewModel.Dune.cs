using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using YaDIS3.Rendering;
namespace YaDIS3.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private DuneTemplate? selectedTemplate;
    private string templateLanguage = "fr", pathMappings = "", exportParentPath = "";
    private bool aaiBackgrounds, useCases, showEpisodePreview;
    private Bitmap? renderedPreview;
    private CancellationTokenSource? activeOperation;
    public ObservableCollection<DuneTemplate> Templates { get; } = [];
    public IReadOnlyList<string> TemplateLanguages { get; } = ["fr", "en"];
    public ICommand ProcessCommand { get; private set; } = null!;
    public ICommand ExportCommand { get; private set; } = null!;
    public ICommand CancelCommand { get; private set; } = null!;
    public DuneTemplate? SelectedTemplate { get => selectedTemplate; set { selectedTemplate = value; Notify(); } }
    public string TemplateLanguage { get => templateLanguage; set { templateLanguage = value; Notify(); } }
    public string PathMappings { get => pathMappings; set { pathMappings = value; Notify(); } }
    public string ExportParentPath { get => exportParentPath; set { exportParentPath = value; Notify(); } }
    public bool AaiBackgrounds { get => aaiBackgrounds; set { aaiBackgrounds = value; Notify(); } }
    public bool UseCases { get => useCases; set { useCases = value; Notify(); } }
    public bool ShowEpisodePreview { get => showEpisodePreview; set { showEpisodePreview = value; RefreshRenderedPreview(); Notify(); } }
    public Bitmap? RenderedPreview { get => renderedPreview; private set => Replace(ref renderedPreview, value); }
    private CancellationToken OperationToken => activeOperation?.Token ?? CancellationToken.None;
    private sealed record DunePreferences(string Template, string TemplatePath, string Language, bool Aai, bool Cases, string Mappings, string Destination);
    private void InitializeDune()
    {
        ProcessCommand = new UiCommand(() => RunAsync(ProcessSelectedAsync));
        ExportCommand = new UiCommand(() => RunAsync(ExportDuneAsync));
        CancelCommand = new UiCommand(() => { activeOperation?.Cancel(); return Task.CompletedTask; });
        try { foreach (var template in DuneTemplate.Bundled()) Templates.Add(template); SelectedTemplate = Templates.FirstOrDefault(); }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Xml.XmlException) { Status = "Templates : " + e.Message; }
    }
    public void LoadTemplate(string directory)
    {
        var template = DuneTemplate.Load(directory, Path.Combine(AppContext.BaseDirectory, "Assets", "Images"));
        var existing = Templates.FirstOrDefault(t => t.Directory == template.Directory);
        if (existing is null) Templates.Add(template); SelectedTemplate = existing ?? template; Status = "Template chargé : " + template.Name;
    }
    private void LoadDunePreferences()
    {
        PathMappings = ""; ExportParentPath = ""; TemplateLanguage = "fr"; AaiBackgrounds = false; UseCases = false;
        SelectedTemplate = Templates.FirstOrDefault(t => t.Name == "Origins") ?? Templates.FirstOrDefault();
        if (workspace is null) return;
        var file = Path.Combine(workspace.Directory, "yadis3-modern-export.json"); if (!File.Exists(file)) return;
        try
        {
            var config = JsonSerializer.Deserialize<DunePreferences>(File.ReadAllText(file)); if (config is null) return;
            if (File.Exists(Path.Combine(config.TemplatePath, "template.xml"))) LoadTemplate(config.TemplatePath);
            else SelectedTemplate = Templates.FirstOrDefault(t => t.Name == config.Template) ?? SelectedTemplate;
            TemplateLanguage = config.Language is "fr" or "en" ? config.Language : "fr"; AaiBackgrounds = config.Aai; UseCases = config.Cases;
            PathMappings = config.Mappings; ExportParentPath = config.Destination;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or System.Xml.XmlException or InvalidDataException) { Status = "Réglages Dune non chargés : " + e.Message; }
    }
    private void SaveDunePreferences()
    {
        if (workspace is null || SelectedTemplate is null) return;
        var file = Path.Combine(workspace.Directory, "yadis3-modern-export.json"); var temporary = file + ".tmp";
        try
        {
            var config = new DunePreferences(SelectedTemplate.Name, SelectedTemplate.Directory, TemplateLanguage, AaiBackgrounds, UseCases, PathMappings, ExportParentPath);
            File.WriteAllText(temporary, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })); File.Move(temporary, file, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public async Task ProcessSelectedAsync()
    {
        var card = SelectedCard ?? throw new InvalidOperationException("Sélectionnez une fiche.");
        var template = SelectedTemplate ?? throw new InvalidOperationException("Chargez un template yaDIS3.");
        var current = workspace ?? throw new InvalidOperationException("Ouvrez une bibliothèque.");
        if (card.IsDirty) await repository.SaveAsync(card, OperationToken);
        SaveDunePreferences(); var media = new MediaContext(card); var options = new RenderOptions(TemplateLanguage, AaiBackgrounds, UseCases);
        var progress = new Progress<string>(message => Status = message);
        await Task.Run(() => DuneExporter.Process(media, template, options, current.Directory, cancellationToken: OperationToken, progress: progress), OperationToken);
        RefreshImages(); Status = "Rendu terminé : aperçu, icônes et saisons actualisés. Les données vidéo et métadonnées existantes sont conservées.";
    }
    public async Task ExportDuneAsync()
    {
        var template = SelectedTemplate ?? throw new InvalidOperationException("Chargez un template yaDIS3.");
        if (string.IsNullOrWhiteSpace(ExportParentPath)) throw new InvalidOperationException("Choisissez un dossier de destination pour l'export Dune.");
        var mappings = DunePaths.Parse(PathMappings);
        await SaveAllAsync(); SaveDunePreferences();
        var media = Cards.Select(c => new MediaContext(c)).ToArray(); var options = new RenderOptions(TemplateLanguage, AaiBackgrounds, UseCases); var destination = ExportParentPath;
        var progress = new Progress<string>(message => Status = message);
        var result = await Task.Run(() => new DuneExporter(template, options, mappings).Export(media, destination, OperationToken, progress), OperationToken);
        Status = $"Export terminé : {result.MediaCount} fiche(s), {result.EpisodeCount} épisode(s), dans {result.Directory}." + (result.Warnings.Count > 0 ? " " + string.Join(" · ", result.Warnings) : "");
    }
    private void RefreshRenderedPreview()
    {
        var card = SelectedCard;
        if (card is null) { RenderedPreview = null; return; }
        var path = Path.Combine(card.DataDirectory, "preview.jpg");
        if (ShowEpisodePreview && SelectedEpisode is { } episode && workspace is not null)
        {
            var media = new MediaContext(card);
            var node = media.Episodes.FirstOrDefault(e => (string?)e.Attribute("key") == episode.Key);
            if (node is not null)
            {
                var candidate = Path.Combine(workspace.Directory, "interface-yaDIS3", media.Id, MediaContext.ViewingSeason(node), MediaContext.EpisodeFolder(node), "preview.jpg");
                if (File.Exists(candidate)) path = candidate;
            }
        }
        RenderedPreview = LoadImage(path);
    }
}
