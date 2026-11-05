using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

/// <summary>
/// Caractérise les fichiers XML de configuration et les
/// raccourcis de l'export Dune historique.
/// </summary>
public sealed class LegacyInterfaceShortcutsCharacterizationTests
{
    [Fact]
    public void Save_RaccourcisTroisTypes_PreserveOrdreDoublonsEtChemins()
    {
        using ExportFixture fixture = new();
        DuneConfigurationStore store =
            fixture.ConfigurationStore();
        DuneShortcut[] shortcuts =
        [
            new("Favoris", 0),
            new("Animation", 1),
            new("NAS", 2, "smb://nas/Films"),
            new("Favoris", 0),
        ];
        ConfigurationSnapshot snapshot = new(
            new DuneConfiguration(),
            [],
            Groups(enabled: true),
            shortcuts
        );

        store.Save(fixture.Root, snapshot);

        XElement document = XElement.Load(
            Path.Combine(fixture.Root, "shortcuts.xml")
        );
        XElement[] saved = document.Elements("shortcut")
            .ToArray();
        Assert.Equal(4, saved.Length);
        Assert.Equal(
            ["Favoris", "Animation", "NAS", "Favoris"],
            saved.Select(node => node.Value)
        );
        Assert.Equal(
            ["0", "1", "2", "0"],
            saved.Select(node => node.Attribute("type")?.Value)
        );
        Assert.Null(saved[0].Attribute("path"));
        Assert.Null(saved[1].Attribute("path"));
        Assert.Equal(
            "smb://nas/Films",
            saved[2].Attribute("path")?.Value
        );
        Assert.Null(saved[3].Attribute("path"));
        Assert.Equal(shortcuts, store.Load(fixture.Root).Shortcuts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Export_SelectionActiveOuNon_RespecteCibleEtOrdre(
        bool enabled
    )
    {
        using ExportFixture fixture = new();
        Groups(enabled).Save(
            Path.Combine(fixture.Root, "groups.xml")
        );
        Shortcuts().Save(
            Path.Combine(fixture.Root, "shortcuts.xml")
        );

        ExportResult result = ExportFixture
            .Exporter()
            .Export(fixture.Cards(), fixture.Destination);

        string selection = Path.Combine(
            result.Directory,
            "00",
            "01_Favoris"
        );
        Assert.Equal(enabled, Directory.Exists(selection));

        string selectionShortcut = Path.Combine(
            result.Directory,
            "05_00_Favoris",
            "dune_folder.txt"
        );
        Assert.Equal(enabled, File.Exists(selectionShortcut));
        if (enabled)
        {
            Assert.Contains(
                "media_url=../00/01_Favoris",
                File.ReadAllText(selectionShortcut),
                StringComparison.Ordinal
            );
        }

        string genre = File.ReadAllText(
            Path.Combine(
                result.Directory,
                "05_01_Animation",
                "dune_folder.txt"
            )
        );
        Assert.Contains(
            "media_url=../00/04/Animation",
            genre,
            StringComparison.Ordinal
        );

        string folder = File.ReadAllText(
            Path.Combine(
                result.Directory,
                "05_02_NAS",
                "dune_folder.txt"
            )
        );
        Assert.Contains(
            "media_url=smb://nas/Films",
            folder,
            StringComparison.Ordinal
        );
        Assert.Equal(
            enabled,
            File.Exists(
                Path.Combine(
                    result.Directory,
                    "05_03_Favoris",
                    "dune_folder.txt"
                )
            )
        );
    }

    private static XDocument Groups(bool enabled) =>
        new(
            new XElement(
                "groups",
                new XElement(
                    "selections",
                    new XElement(
                        "selection",
                        new XAttribute(
                            "enabled",
                            enabled ? "yes" : "no"
                        ),
                        new XElement("name", "Favoris"),
                        new XElement(
                            "movie",
                            ExportFixture.MovieName
                        )
                    )
                )
            )
        );

    private static XDocument Shortcuts() =>
        new(
            new XElement(
                "shortcuts",
                new XElement(
                    "shortcut",
                    new XAttribute("type", 0),
                    "Favoris"
                ),
                new XElement(
                    "shortcut",
                    new XAttribute("type", 1),
                    "Animation"
                ),
                new XElement(
                    "shortcut",
                    new XAttribute("type", 2),
                    new XAttribute(
                        "path",
                        "smb://nas/Films"
                    ),
                    "NAS"
                ),
                new XElement(
                    "shortcut",
                    new XAttribute("type", 0),
                    "Favoris"
                )
            )
        );
}
