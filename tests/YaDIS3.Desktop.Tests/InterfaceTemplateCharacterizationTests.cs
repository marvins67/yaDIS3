using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;
using YaDIS3.Rendering;

namespace YaDIS3.Desktop.Tests;

/// <summary>
/// Caractérise la sélection des thèmes dans SetupForm de yaDIS2.
/// Un aperçu absent efface l'auteur et la version, sans message.
/// La validation seule transmet le thème à la configuration active.
/// </summary>
public sealed class InterfaceTemplateCharacterizationTests
{
    [Fact]
    public void TemplateDescription_ApercuAbsent_EffaceAuteurEtVersion()
    {
        string folder = CreateTemplate("Sans aperçu", "Auteur", "1.2");
        try
        {
            DuneTemplate template = DuneTemplate.Load(folder);
            using ConfigurationViewModel draft = CreateDraft([template]);

            Assert.Same(template, draft.SelectedTemplate);
            Assert.Null(draft.TemplatePreview);
            Assert.Equal("", draft.TemplateDescription);
            Assert.Equal("", draft.Message);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void TemplateDescription_ApercuCorrompu_EffaceInformationsSansMessage()
    {
        string folder = CreateTemplate("Illisible", "Auteur", "2.1");
        File.WriteAllBytes(
            Path.Combine(folder, "preview.jpg"),
            [1, 2, 3, 4]
        );
        try
        {
            DuneTemplate template = DuneTemplate.Load(folder);
            using ConfigurationViewModel draft = CreateDraft([template]);

            Assert.Null(draft.TemplatePreview);
            Assert.Equal("", draft.TemplateDescription);
            Assert.Equal("", draft.Message);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void SelectedTemplate_ChangementSansValidation_NeModifiePasLaSource()
    {
        string first = CreateTemplate("Premier", "Auteur A", "1.0");
        string second = CreateTemplate("Second", "Auteur B", "2.0");
        try
        {
            DuneTemplate initial = DuneTemplate.Load(first);
            DuneTemplate next = DuneTemplate.Load(second);
            ConfigurationSnapshot snapshot = InitialSnapshot(initial);
            using ConfigurationViewModel draft = CreateDraft(
                [initial, next],
                snapshot
            );

            draft.SelectedTemplate = next;

            Assert.Equal(next.Name, draft.Snapshot().Settings.Template);
            Assert.Equal(
                next.Directory,
                draft.Snapshot().Settings.TemplatePath
            );
            Assert.Equal(initial.Name, snapshot.Settings.Template);
            Assert.Equal(initial.Directory, snapshot.Settings.TemplatePath);
        }
        finally
        {
            Directory.Delete(first, recursive: true);
            Directory.Delete(second, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyAsync_ThemeSelectionne_EnregistreNomEtChemin()
    {
        string first = CreateTemplate("Premier", "Auteur A", "1.0");
        string second = CreateTemplate("Second", "Auteur B", "2.0");
        try
        {
            DuneTemplate initial = DuneTemplate.Load(first);
            DuneTemplate next = DuneTemplate.Load(second);
            ConfigurationSnapshot? saved = null;
            using ConfigurationViewModel draft = CreateDraft(
                [initial, next],
                InitialSnapshot(initial),
                value => saved = value
            );

            draft.SelectedTemplate = next;

            Assert.True(await draft.ApplyAsync());
            Assert.NotNull(saved);
            Assert.Equal(next.Name, saved.Settings.Template);
            Assert.Equal(next.Directory, saved.Settings.TemplatePath);
        }
        finally
        {
            Directory.Delete(first, recursive: true);
            Directory.Delete(second, recursive: true);
        }
    }

    [Fact]
    public void SelectedTemplate_PolicePresente_NeChangePasLesFichiers()
    {
        string folder = CreateTemplate("Avec police", "Auteur", "1.0");
        string font = Path.Combine(folder, "Fonts", "original.ttf");
        Directory.CreateDirectory(Path.GetDirectoryName(font)!);
        File.WriteAllBytes(font, [1, 2, 3]);
        try
        {
            DuneTemplate template = DuneTemplate.Load(folder);
            byte[] before = File.ReadAllBytes(font);
            using ConfigurationViewModel draft = CreateDraft([template]);

            Assert.Contains(font, template.Fonts);
            Assert.Equal(before, File.ReadAllBytes(font));
            Assert.Equal(template.Name, draft.Snapshot().Settings.Template);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static ConfigurationSnapshot InitialSnapshot(
        DuneTemplate template
    ) =>
        new(
            new()
            {
                Template = template.Name,
                TemplatePath = template.Directory,
            },
            [],
            new XDocument(new XElement("groups")),
            []
        );

    private static ConfigurationViewModel CreateDraft(
        IReadOnlyList<DuneTemplate> templates,
        ConfigurationSnapshot? initial = null,
        Action<ConfigurationSnapshot>? save = null
    ) =>
        new(
            initial
                ?? new(
                    new(),
                    [],
                    new XDocument(new XElement("groups")),
                    []
                ),
            "/workspace",
            templates,
            [],
            (value, _, _) =>
            {
                save?.Invoke(value);
                return Task.CompletedTask;
            }
        );

    private static string CreateTemplate(
        string name,
        string author,
        string version
    )
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "yadis3-theme-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(directory);
        new XDocument(
            new XElement(
                "template",
                new XAttribute("name", name),
                new XAttribute("author", author),
                new XAttribute("version", version),
                new XElement("movie"),
                new XElement("tv")
            )
        ).Save(Path.Combine(directory, "template.xml"));
        return directory;
    }
}
