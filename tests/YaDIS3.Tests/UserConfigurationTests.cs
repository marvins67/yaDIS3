using System.Configuration;
using System.Text;
using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class UserConfigurationTests
{
    [Fact]
    public void Save_EmplacementExterieur_UtiliseLesClesHistoriques()
    {
        using ExportFixture fixture = new();
        DuneConfigurationStore store =
            fixture.ConfigurationStore();
        store.SaveSettings(
            fixture.Root,
            new()
            {
                Workspace = fixture.Root,
                Destination = "Z:\\Dune\\yaDIS",
                Aai = true,
                Cases = true,
                JpegQuality = 85,
            }
        );
        XDocument document = XDocument.Load(
            fixture.UserConfigPath
        );
        XElement section = document
            .Root!.Element("userSettings")!
            .Element(UserConfigurationStore.SectionName)!;
        Assert.Equal(fixture.Root, Read(section, "Workspace"));
        Assert.Equal(
            "Z:\\Dune\\yaDIS",
            Read(section, "ExportPath")
        );
        Assert.Equal("True", Read(section, "UseAAI"));
        Assert.Equal("True", Read(section, "UseCase"));
        Assert.Equal("85", Read(section, "JPGQuality"));
        Assert.False(
            File.Exists(
                Path.Combine(fixture.Root, "user.config")
            )
        );
        Assert.Equal(
            fixture.Root,
            store.LoadSettings().Workspace
        );
    }

    [Fact]
    public void Load_DeuxWorkspaces_ConserveLesPreferencesGlobalesEtLesDossiersLocaux()
    {
        using ExportFixture fixture = new();
        DuneConfigurationStore store =
            fixture.ConfigurationStore();
        string other = Path.Combine(fixture.Root, "other");
        store.SaveSettings(
            fixture.Root,
            new()
            {
                Workspace = fixture.Root,
                Destination = "Dune",
            }
        );
        new XDocument(
            new XElement(
                "folders",
                new XElement(
                    "folder",
                    new XElement("path", "/Films"),
                    new XElement("type", 0),
                    new XElement("dunepath", "smb://nas/Films")
                )
            )
        ).Save(Path.Combine(fixture.Root, "folders.xml"));
        Assert.Single(store.Load(fixture.Root).Folders);
        Assert.Empty(store.Load(other).Folders);
        Assert.Equal(
            "Dune",
            store.Load(other).Settings.Destination
        );
        File.WriteAllText(
            Path.Combine(fixture.Root, "last-workspace.txt"),
            "IGNORE",
            Encoding.UTF8
        );
        File.WriteAllText(
            Path.Combine(
                fixture.Root,
                "yadis3-modern-export.json"
            ),
            "{\"Destination\":\"IGNORE\"}",
            Encoding.UTF8
        );
        Assert.Equal(
            "Dune",
            store.Load(fixture.Root).Settings.Destination
        );
        Assert.Equal(
            fixture.Root,
            store.LoadSettings().Workspace
        );
    }

    [Fact]
    public void Save_SchemaNet_LisibleParLeFournisseurDeConfiguration()
    {
        using ExportFixture fixture = new();
        fixture
            .ConfigurationStore()
            .SaveSettings(
                fixture.Root,
                new() { Workspace = fixture.Root }
            );
        string application = Path.Combine(
            fixture.Root,
            "app.config"
        );
        File.WriteAllText(
            application,
            "<configuration />",
            Encoding.UTF8
        );
        ExeConfigurationFileMap map = new()
        {
            ExeConfigFilename = application,
            RoamingUserConfigFilename = Path.Combine(
                fixture.Root,
                "absent.config"
            ),
            LocalUserConfigFilename = fixture.UserConfigPath,
        };
        Configuration configuration =
            ConfigurationManager.OpenMappedExeConfiguration(
                map,
                ConfigurationUserLevel.PerUserRoamingAndLocal
            );
        ClientSettingsSection section =
            Assert.IsType<ClientSettingsSection>(
                configuration.GetSection(
                    "userSettings/"
                        + UserConfigurationStore.SectionName
                )
            );
        Assert.Equal(
            fixture.Root,
            section
                .Settings.Get("Workspace")
                .Value.ValueXml.InnerText
        );
    }

    private static string Read(XElement section, string key) =>
        section
            .Elements("setting")
            .Single(node =>
                (string?)node.Attribute("name") == key
            )
            .Element("value")!
            .Value;
}
