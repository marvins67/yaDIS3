using Xunit;

namespace YaDIS3.Tests;

/// <summary>Vérifie l’ordre historique des propriétés des menus de personnes.</summary>
public sealed class LegacyPersonMenuOrderTests
{
    [Theory]
    [InlineData("05")]
    [InlineData("06")]
    public void ExportLegacy_MenuPersonnes_ConserveOrdreDesProprietes(string category)
    {
        using ExportFixture fixture = new();
        Directory.CreateDirectory(fixture.Destination);

        ExportFixture.Exporter().ExportLegacy(fixture.Cards(), fixture.Destination);

        string[] lines = File.ReadAllLines(Path.Combine(
            fixture.Destination, "00", category, "A", "dune_folder.txt"));
        int icon = Array.IndexOf(lines, "icon_path=icon.aai");
        Assert.True(icon >= 0);
        Assert.Equal("icon_sel_path=icon_sel.aai", lines[icon + 1]);
        Assert.Equal("icon_valign=center", lines[icon + 2]);
    }

}
