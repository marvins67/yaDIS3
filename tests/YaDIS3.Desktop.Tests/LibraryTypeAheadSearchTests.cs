using Xunit;
using YaDIS3.Desktop.ViewModels;

namespace YaDIS3.Desktop.Tests;

public sealed class LibraryTypeAheadSearchTests
{
    private static readonly string[] s_titles =
    [
        "Astérix.mkv",
        "Barry Lyndon (1975).mkv",
        "Batman Begins.mkv",
        "Détective Conan",
    ];

    [Fact]
    public void Find_TouchesConsecutives_SelectionneLePrefixe()
    {
        LibraryTypeAheadSearch search = new();
        DateTimeOffset now = DateTimeOffset.UnixEpoch;

        Assert.Equal(1, search.Find(s_titles, 'b', now));
        Assert.Equal(2, search.Find(s_titles, 'a', now.AddMilliseconds(150)));
        Assert.Equal(2, search.Find(s_titles, 't', now.AddMilliseconds(300)));
    }

    [Fact]
    public void Find_ApresPause_RecommenceLaRecherche()
    {
        LibraryTypeAheadSearch search = new();
        DateTimeOffset now = DateTimeOffset.UnixEpoch;

        Assert.Equal(2, search.Find(s_titles, 'b', now.AddSeconds(1)));
        Assert.Equal(3, search.Find(s_titles, 'd', now.AddSeconds(3)));
    }

    [Fact]
    public void Find_CaractereSansCorrespondance_RechercheLaNouvelleLettre()
    {
        LibraryTypeAheadSearch search = new();
        DateTimeOffset now = DateTimeOffset.UnixEpoch;

        Assert.Equal(1, search.Find(s_titles, 'b', now));
        Assert.Equal(3, search.Find(s_titles, 'd', now.AddMilliseconds(250)));
    }

    [Fact]
    public void Find_NomAvecAccent_IgnoreLaCasse()
    {
        LibraryTypeAheadSearch search = new();
        Assert.Equal(
            3,
            search.Find(s_titles, 'D', DateTimeOffset.UnixEpoch)
        );
    }

    [Fact]
    public void Find_AucunTitreCorrespondant_NeModifiePasLaSelection()
    {
        LibraryTypeAheadSearch search = new();
        Assert.Equal(
            -1,
            search.Find(s_titles, 'z', DateTimeOffset.UnixEpoch)
        );
    }
}
