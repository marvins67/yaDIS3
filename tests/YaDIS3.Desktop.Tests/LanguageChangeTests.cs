using System.Globalization;
using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;

namespace YaDIS3.Desktop.Tests;

public sealed class LanguageChangeTests
{
    [Theory]
    [InlineData("fr-FR", "fr")]
    [InlineData("en-US", "en")]
    [InlineData("de-DE", "de")]
    public async Task OpenAsync_FirstLaunch_PersistsTheDetectedLanguage(
        string culture,
        string expectedCode
    )
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        string directory = Path.Combine(
            Path.GetTempPath(),
            "yadis3-initial-language-" + Guid.NewGuid()
        );
        string userConfig = Path.Combine(
            directory,
            "user.config"
        );
        try
        {
            CultureInfo.CurrentUICulture = new(culture);
            using MainWindowViewModel model = new(userConfig);
            Assert.Equal(expectedCode, model.Texts.Current.Code);
            await model.OpenAsync(
                Path.Combine(directory, "workspace"),
                create: true
            );
            Assert.Equal(
                expectedCode,
                new DuneConfigurationStore(
                    userConfigurationPath: userConfig
                )
                    .LoadSettings()
                    .Language
            );
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfirmLanguageChange_Choice_PreservesLegacySaveAndRestart(
        bool confirmed
    )
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "yadis3-language-" + Guid.NewGuid()
        );
        Directory.CreateDirectory(directory);
        try
        {
            DuneConfigurationStore store = new(
                userConfigurationPath: Path.Combine(
                    directory,
                    "user.config"
                )
            );
            DuneConfiguration saved = new()
            {
                Language = "fr",
                Destination = "Ancien export",
            };
            store.SaveSettings(directory, saved);
            RestartRecorder restart = new();
            using ConfigurationViewModel draft = new(
                new(
                    saved,
                    [],
                    new XDocument(new XElement("groups")),
                    []
                ),
                directory,
                [],
                [],
                (_, _, _) =>
                    throw new InvalidOperationException(
                        "Ne doit pas appliquer le brouillon."
                    ),
                configurationStore: store,
                texts: LegacyLanguageCatalog.Load("fr"),
                restart: restart
            );
            draft.Destination = "Nouveau dossier non validé";
            draft.SelectedLanguage = draft.Languages.Single(
                item => item.Code == "en"
            );
            Assert.True(draft.HasLanguageChange);
            Assert.Equal("fr", store.LoadSettings().Language);
            draft.ConfirmLanguageChange(confirmed);
            Assert.Equal(
                confirmed ? "en" : "fr",
                store.LoadSettings().Language
            );
            Assert.Equal(
                "Ancien export",
                store.LoadSettings().Destination
            );
            Assert.Equal(confirmed ? 1 : 0, restart.Count);
            Assert.Equal("Configurer", draft.Texts["10"]);
            if (!confirmed)
            {
                Assert.Equal("fr", draft.SelectedLanguage!.Code);
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void SelectedLanguage_InitialChoice_DoesNotRestart()
    {
        RestartRecorder restart = new();
        using ConfigurationViewModel draft = new(
            new(
                new(),
                [],
                new XDocument(new XElement("groups")),
                []
            ),
            "/workspace",
            [],
            [],
            (_, _, _) => Task.CompletedTask,
            texts: LegacyLanguageCatalog.Load("fr"),
            restart: restart
        );
        draft.SelectedLanguage = draft.Languages.Single(item =>
            item.Code == "fr"
        );
        draft.ConfirmLanguageChange(true);
        Assert.Equal(0, restart.Count);
    }
}
