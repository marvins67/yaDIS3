using System.Text;
using Xunit;
using YaDIS3.Core;

namespace YaDIS3.Tests;

public sealed class LegacyExportCharacterizationTests
{
    // ProcessForm.ProcessMovie/ProcessTVShow utilisent item/filename,
    // sans CleanTitle, suppression d'extension ni normalisation Unicode.
    [Theory]
    [InlineData(
        "(001) Blanche-Neige Et Les Sept Nains (1938).mkv",
        false,
        "00/99/(001) Blanche-Neige Et Les Sept Nains (1938).mkv"
    )]
    [InlineData(
        "L'été d'Adèle [1080p] + bonus.mkv",
        false,
        "00/99/L'été d'Adèle [1080p] + bonus.mkv"
    )]
    [InlineData(
        "Wall·E (2008).iso",
        false,
        "00/99/Wall·E (2008).iso"
    )]
    [InlineData("SPY×FAMILY", true, "01/SPY×FAMILY")]
    [InlineData(
        "Percy Jackson and the Olympians",
        true,
        "01/Percy Jackson and the Olympians"
    )]
    [InlineData(
        "Apocalypse, la 1ère Guerre mondiale",
        true,
        "01/Apocalypse, la 1ère Guerre mondiale"
    )]
    public void MediaPath_RealisticFilename_PreservesLegacyName(
        string filename,
        bool series,
        string expected
    )
    {
        Assert.Equal(
            expected,
            LegacyExportPaths.Media(filename, series)
        );
    }

    // ProcessTVShow prend Substring(0, 2), pas le numéro de saison TVDB.
    [Theory]
    [InlineData("02_001_000", "02")]
    [InlineData("01_000_001", "01")]
    [InlineData("00_001_000", "00")]
    [InlineData("100_001_000", "10")]
    public void SeasonPath_LegacyEpisodeKey_UsesFirstTwoCharacters(
        string key,
        string expected
    )
    {
        Assert.Equal(expected, LegacyExportPaths.Season(key));
    }

    [Theory]
    [InlineData("9 (2009)", "0-9")]
    [InlineData("Été", "É")]
    [InlineData("évasion", "é")]
    [InlineData("[REC]", "[")]
    [InlineData("Le Seigneur des anneaux", "L")]
    public void Alphabet_LegacySortTitle_DoesNotStripAccents(
        string title,
        string expected
    )
    {
        Assert.Equal(
            expected,
            LegacyExportPaths.Alphabet(title)
        );
    }

    // MainModule.CreateDF découpe sur | et écrit une ligne finale même vide.
    [Theory]
    [InlineData(
        false,
        "background_path=background.jpg",
        "background_path=background.aai"
    )]
    [InlineData(
        true,
        "background_path=background.aai",
        "background_path=background.aai"
    )]
    public void Encode_LegacyCreateDf_PreservesBomCrLfAndBackgroundOption(
        bool aai,
        string expected,
        string content
    )
    {
        byte[] actual = LegacyDuneEncoding.Encode(
            content + "|",
            aai
        );
        Assert.Equal(
            new byte[] { 0xef, 0xbb, 0xbf },
            actual[..3]
        );
        Assert.Equal(
            LegacyDuneEncoding.Header
                + "\r\n"
                + expected
                + "\r\n\r\n",
            Encoding.UTF8.GetString(actual[3..])
        );
    }
}
