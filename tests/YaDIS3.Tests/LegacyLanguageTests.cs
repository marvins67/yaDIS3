using System.Xml;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class LegacyLanguageTests
{
    [Theory]
    [InlineData("", "fr-FR", "fr", "Configurer")]
    [InlineData("", "en-GB", "en", "Setup")]
    [InlineData("", "de-DE", "en", "Setup")]
    [InlineData("en", "fr-FR", "en", "Setup")]
    public void Load_InitialPreference_UsesYaDis2Selection(
        string savedCode,
        string culture,
        string expectedCode,
        string expectedText
    )
    {
        using LanguageFixture fixture = new();
        LegacyLanguageCatalog catalog = new LegacyLanguageLoader(
            new ExportFileSystem()
        ).Load(fixture.Directory, savedCode, new(culture));
        Assert.Equal(expectedCode, catalog.Current.Code);
        Assert.Equal(expectedText, catalog["10"]);
        Assert.Contains(
            catalog.Languages,
            item => item.Name == "Français"
        );
    }

    [Fact]
    public void Load_ThreeLetterCulture_UsesTheFirstTwoNameCharacters()
    {
        using LanguageFixture fixture = new();
        fixture.Write("fi.xml", "fi", "Suomi", "Asetukset");
        fixture.Write(
            "fil.xml",
            "fil",
            "Filipino",
            "Mga setting"
        );
        var catalog = new LegacyLanguageLoader(
            new ExportFileSystem()
        ).Load(fixture.Directory, "", new("fil-PH"));
        Assert.Equal("fi", catalog.Current.Code);
    }

    [Fact]
    public void Load_UnknownSavedPreference_DoesNotSilentlySelectEnglish()
    {
        using LanguageFixture fixture = new();
        Assert.Throws<InvalidDataException>(() =>
            new LegacyLanguageLoader(
                new ExportFileSystem()
            ).Load(fixture.Directory, "xx", new("fr-FR"))
        );
    }

    [Fact]
    public void GetString_AbsentIdentifier_UsesExactLegacyFallback()
    {
        using LanguageFixture fixture = new();
        var catalog = new LegacyLanguageLoader(
            new ExportFileSystem()
        ).Load(fixture.Directory, "fr", new("fr-FR"));
        Assert.Equal("<label not found>", catalog["99999"]);
    }

    [Fact]
    public void Load_XmlEdited_ReloadsOnlyOnNextLoad()
    {
        using LanguageFixture fixture = new();
        LegacyLanguageLoader loader = new(
            new ExportFileSystem()
        );
        LegacyLanguageCatalog previous = loader.Load(
            fixture.Directory,
            "fr",
            new("en-US")
        );
        fixture.Write(
            "fr.xml",
            "fr",
            "Français",
            "Libellé personnalisé"
        );
        Assert.Equal("Configurer", previous["10"]);
        Assert.Equal(
            "Libellé personnalisé",
            loader.Load(fixture.Directory, "fr", new("en-US"))[
                "10"
            ]
        );
    }

    [Fact]
    public void Load_MalformedUnselectedXml_FailsLikeLoadLanguages()
    {
        using LanguageFixture fixture = new();
        File.WriteAllText(
            Path.Combine(fixture.Directory, "broken.xml"),
            "<strings>"
        );
        Assert.Throws<XmlException>(() =>
            new LegacyLanguageLoader(
                new ExportFileSystem()
            ).Load(fixture.Directory, "fr", new("fr-FR"))
        );
    }

    [Fact]
    public void Load_OriginalCatalogs_AreDistributedNextToTheExecutable()
    {
        LegacyLanguageCatalog french =
            LegacyLanguageCatalog.Load("fr");
        LegacyLanguageCatalog english =
            LegacyLanguageCatalog.Load("en");
        Assert.Equal(18, french.Languages.Count);
        Assert.Equal(
            "Veuillez saisir le chemin au format Dune",
            french["31"]
        );
        Assert.NotEqual(french["31"], english["31"]);
        Assert.Contains(
            french.Languages,
            item => item.Code == "th"
        );
    }

    [Fact]
    public void GetString_DuplicateIdentifiers_ReturnsFirstXmlNode()
    {
        using LanguageFixture fixture = new();
        string file = Path.Combine(fixture.Directory, "fr.xml");
        string original = File.ReadAllText(file)
            .Replace(
                "</strings>",
                "<string id='10'>Second libellé</string></strings>",
                StringComparison.Ordinal
            );
        File.WriteAllText(file, original);
        var catalog = new LegacyLanguageLoader(
            new ExportFileSystem()
        ).Load(fixture.Directory, "fr", new("fr-FR"));
        Assert.Equal("Configurer", catalog["10"]);
    }
}
