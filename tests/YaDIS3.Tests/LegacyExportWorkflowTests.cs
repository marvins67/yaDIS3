using Xunit;
using YaDIS3.Rendering;

namespace YaDIS3.Tests;

/// <summary>
/// Valide le nouveau parcours de publication depuis workspace/interface.
/// </summary>
public sealed class LegacyExportWorkflowTests
{
    [Fact]
    public void ExportLegacy_DestinationAbsente_RefuseLaPublication()
    {
        using ExportFixture fixture = new();

        Assert.Throws<DirectoryNotFoundException>(() =>
            ExportFixture.Exporter().ExportLegacy(
                fixture.Cards(),
                fixture.Destination
            )
        );
        Assert.False(Directory.Exists(fixture.Destination));
    }

    [Fact]
    public void ExportLegacy_DeuxExports_SynchroniseSansSauvegardePrevious()
    {
        using ExportFixture fixture = new();
        fixture.ProcessIndexes();
        Directory.CreateDirectory(fixture.Destination);
        DuneExporter exporter = ExportFixture.Exporter();

        ExportResult first = exporter.ExportLegacy(
            fixture.Cards(),
            fixture.Destination
        );
        string obsolete = Path.Combine(first.Directory, "obsolete.txt");
        File.WriteAllText(obsolete, "ancien");
        exporter.ExportLegacy(fixture.Cards(), fixture.Destination);

        Assert.False(File.Exists(obsolete));
        Assert.False(Directory.Exists(fixture.Destination + ".previous"));
        Assert.True(
            File.Exists(
                Path.Combine(fixture.Root, "interface", "dune_folder.txt")
            )
        );
    }

    [Fact]
    public void ExportLegacy_ImagesTraitees_ReutiliseLesFichiersSource()
    {
        using ExportFixture fixture = new();
        fixture.ProcessIndexes();
        string movie = Path.Combine(
            fixture.Root,
            "interface",
            "00",
            "99",
            ExportFixture.MovieName
        );
        Directory.CreateDirectory(movie);
        byte[] icon = [1, 2, 3, 4];
        File.WriteAllBytes(Path.Combine(movie, "icon.aai"), icon);
        Directory.CreateDirectory(fixture.Destination);

        ExportFixture.Exporter().ExportLegacy(
            fixture.Cards(),
            fixture.Destination
        );

        Assert.Equal(
            icon,
            File.ReadAllBytes(
                Path.Combine(
                    fixture.Destination,
                    "00",
                    "99",
                    ExportFixture.MovieName,
                    "icon.aai"
                )
            )
        );
    }
    [Fact]
    public void ExportLegacy_QuatreSelections_CreeRaccourcisEtIcones()
    {
        using ExportFixture fixture = new();
        fixture.ProcessIndexes();
        Directory.CreateDirectory(fixture.Destination);
        string[] names = ["Papa", "Enfants", "Disney", "Ghibli"];
        SaveSelections(fixture.Root, names);
        SaveShortcuts(fixture.Root, names);

        ExportFixture.Exporter().ExportLegacy(
            fixture.Cards(),
            fixture.Destination
        );

        for (int index = 0; index < names.Length; index++)
        {
            string directory = Path.Combine(
                fixture.Destination,
                FormattableString.Invariant(
                    $"05_{index:00}_{names[index]}"
                )
            );
            string definition = File.ReadAllText(
                Path.Combine(directory, "dune_folder.txt")
            );
            Assert.Equal(
                "media_url=../00/01_" + names[index]
                    + "|media_action=browse|icon_path=icon.aai|icon_valign=center",
                definition.TrimStart('\uFEFF')
            );
            Assert.True(
                new FileInfo(
                    Path.Combine(directory, "icon.aai")
                ).Length > 0
            );
            Assert.True(Directory.Exists(
                Path.Combine(fixture.Destination, "00", "01_" + names[index])
            ));
        }
    }

    [Fact]
    public void ExportLegacy_SelectionRenommeeOuSupprimee_ActualiseRaccourcis()
    {
        using ExportFixture fixture = new();
        fixture.ProcessIndexes();
        Directory.CreateDirectory(fixture.Destination);
        SaveSelections(fixture.Root, ["Papa", "Enfants", "Disney", "Ghibli"]);
        SaveShortcuts(fixture.Root, ["Papa", "Enfants", "Disney", "Ghibli"]);
        var exporter = ExportFixture.Exporter();
        exporter.ExportLegacy(fixture.Cards(), fixture.Destination);

        SaveSelections(fixture.Root, ["Papa", "Jeunesse", "Ghibli"]);
        SaveShortcuts(fixture.Root, ["Papa", "Jeunesse", "Ghibli"]);
        exporter.ExportLegacy(fixture.Cards(), fixture.Destination);

        Assert.False(Directory.Exists(
            Path.Combine(fixture.Destination, "05_01_Enfants")
        ));
        Assert.False(Directory.Exists(
            Path.Combine(fixture.Destination, "05_02_Disney")
        ));
        Assert.True(File.Exists(Path.Combine(
            fixture.Destination,
            "05_01_Jeunesse",
            "icon.aai"
        )));
        Assert.Contains(
            "media_url=../00/01_Jeunesse",
            File.ReadAllText(Path.Combine(
                fixture.Destination, "05_01_Jeunesse", "dune_folder.txt"
            )),
            StringComparison.Ordinal
        );
        Assert.True(File.Exists(Path.Combine(
            fixture.Destination, "05_02_Ghibli", "icon.aai"
        )));
    }

    [Fact]
    public void ExportLegacy_SelectionDesactivee_ConserveIndiceDesSuivantes()
    {
        using ExportFixture fixture = new();
        fixture.ProcessIndexes();
        Directory.CreateDirectory(fixture.Destination);
        SaveSelections(fixture.Root, ["Papa", "Ghibli"]);
        SaveShortcuts(fixture.Root, ["Papa", "Enfants", "Ghibli"]);

        ExportFixture.Exporter().ExportLegacy(
            fixture.Cards(),
            fixture.Destination
        );

        Assert.True(Directory.Exists(Path.Combine(
            fixture.Destination, "05_00_Papa"
        )));
        Assert.False(Directory.Exists(Path.Combine(
            fixture.Destination, "05_01_Enfants"
        )));
        Assert.True(File.Exists(Path.Combine(
            fixture.Destination, "05_02_Ghibli", "icon.aai"
        )));
    }

    private static void SaveSelections(string root, string[] names)
    {
        new System.Xml.Linq.XDocument(
            new System.Xml.Linq.XElement(
                "groups",
                new System.Xml.Linq.XElement(
                    "selections",
                    names.Select(name => new System.Xml.Linq.XElement(
                        "selection",
                        new System.Xml.Linq.XAttribute("enabled", "yes"),
                        new System.Xml.Linq.XElement("name", name),
                        new System.Xml.Linq.XElement(
                            "movie", ExportFixture.MovieName
                        )
                    ))
                )
            )
        ).Save(Path.Combine(root, "groups.xml"));
    }

    private static void SaveShortcuts(string root, string[] names)
    {
        new System.Xml.Linq.XDocument(
            new System.Xml.Linq.XElement(
                "shortcuts",
                names.Select(name => new System.Xml.Linq.XElement(
                    "shortcut",
                    new System.Xml.Linq.XAttribute("type", "0"),
                    name
                ))
            )
        ).Save(Path.Combine(root, "shortcuts.xml"));
    }

}
