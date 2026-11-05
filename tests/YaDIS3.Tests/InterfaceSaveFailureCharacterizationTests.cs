using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

/// <summary>
/// Caractérise la persistance des réglages lors d'un échec
/// d'écriture ou d'une validation refusée.
/// </summary>
public sealed class InterfaceSaveFailureCharacterizationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Save_PremiereConfigurationEchouee_NeLaissePasDeDocument(
        int failedWrite
    )
    {
        using ExportFixture fixture = new();
        DuneConfigurationStore store = new(
            new ConfigurationFileSystem(failedWrite),
            fixture.UserConfigPath
        );

        Assert.Throws<IOException>(() =>
            store.Save(fixture.Root, Snapshot())
        );

        Assert.False(File.Exists(fixture.UserConfigPath));
        foreach (
            string filename in new[]
            {
                "folders.xml",
                "groups.xml",
                "shortcuts.xml",
            }
        )
        {
            Assert.False(
                File.Exists(
                    Path.Combine(fixture.Root, filename)
                )
            );
        }
        Assert.Empty(
            Directory.EnumerateFiles(
                fixture.Root,
                "*.tmp-*",
                SearchOption.AllDirectories
            )
        );
    }

    [Fact]
    public void Save_CategoriesInvalides_PreserveLesFichiersExistants()
    {
        using ExportFixture fixture = new();
        DuneConfigurationStore store =
            fixture.ConfigurationStore();
        store.Save(fixture.Root, Snapshot());

        string groupsPath = Path.Combine(
            fixture.Root,
            "groups.xml"
        );
        byte[] previous = File.ReadAllBytes(groupsPath);
        ConfigurationSnapshot invalid = Snapshot() with
        {
            Settings = new()
            {
                Categories = "0012",
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            store.Save(fixture.Root, invalid)
        );

        Assert.Equal(previous, File.ReadAllBytes(groupsPath));
        Assert.Equal(
            90,
            store.Load(fixture.Root).Settings.JpegQuality
        );
    }

    private static ConfigurationSnapshot Snapshot() =>
        new(
            new DuneConfiguration(),
            [],
            new XDocument(
                new XElement(
                    "groups",
                    new XElement("selections")
                )
            ),
            []
        );
}
