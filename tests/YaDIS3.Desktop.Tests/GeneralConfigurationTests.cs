using System.Xml.Linq;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;

namespace YaDIS3.Desktop.Tests;

public sealed class GeneralConfigurationTests
{
    [Theory]
    [InlineData("Films", false, true)]
    [InlineData("Films", true, true)]
    [InlineData("MesyaDIS", false, false)]
    [InlineData("WORKSPACE", true, false)]
    public void ChooseFolder_NonemptyFolder_CreatesHistoricalSubfolder(
        string name,
        bool workspace,
        bool expectedCreation
    )
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "general-" + Guid.NewGuid().ToString("N")
        );
        string chosen = Path.Combine(root, name);
        Directory.CreateDirectory(chosen);
        try
        {
            File.WriteAllText(
                Path.Combine(chosen, "film.iso"),
                "film"
            );
            using ConfigurationViewModel draft = CreateDraft();
            bool created = workspace
                ? draft.ChooseWorkspace(chosen)
                : draft.ChooseDestination(chosen);
            string target = expectedCreation
                ? Path.Combine(
                    chosen,
                    workspace ? "workspace" : "yaDIS"
                )
                : chosen;
            Assert.Equal(expectedCreation, created);
            Assert.Equal(
                target,
                workspace
                    ? draft.WorkspacePath
                    : draft.Destination
            );
            Assert.True(Directory.Exists(target));
            Assert.Equal(
                "film",
                File.ReadAllText(
                    Path.Combine(chosen, "film.iso")
                )
            );
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ChooseFolder_EmptyFolder_UsesItWithoutCreatingSubfolder(
        bool workspace
    )
    {
        string chosen = Path.Combine(
            Path.GetTempPath(),
            "general-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(chosen);
        try
        {
            using ConfigurationViewModel draft = CreateDraft();
            bool created = workspace
                ? draft.ChooseWorkspace(chosen)
                : draft.ChooseDestination(chosen);
            Assert.False(created);
            Assert.Equal(
                chosen,
                workspace
                    ? draft.WorkspacePath
                    : draft.Destination
            );
            Assert.Empty(
                Directory.EnumerateFileSystemEntries(chosen)
            );
        }
        finally
        {
            Directory.Delete(chosen, true);
        }
    }

    [Fact]
    public async Task ApplyAsync_GeneralOptions_PreservesHiddenValues()
    {
        ConfigurationSnapshot? saved = null;
        using ConfigurationViewModel draft = CreateDraft(value =>
            saved = value
        );
        draft.Destination = "smb://nas/yaDIS";
        draft.AutoSelect = true;
        draft.AutoSelectName = "Famille";
        draft.IgnoreArticles = true;
        draft.Articles = "le, la, les";
        draft.BasicMode = true;
        Assert.True(await draft.ApplyAsync());
        Assert.Equal(
            "smb://nas/yaDIS",
            saved!.Settings.Destination
        );
        Assert.True(saved.Settings.AutoSelect);
        Assert.Equal("Famille", saved.Settings.AutoSelectName);
        Assert.True(saved.Settings.IgnoreArticles);
        Assert.Equal("le,la,les", saved.Settings.Articles);
    }

    [Fact]
    public void SelectionNames_DisabledSelections_RemainAvailableAndSorted()
    {
        using ConfigurationViewModel draft = CreateDraft();
        IReadOnlyList<string> names = draft.SelectionNames;
        Assert.Equal(["Famille", "Science-fiction"], names);
        draft.IgnoreArticles = true;
        Assert.Same(names, draft.SelectionNames);
    }

    private static ConfigurationViewModel CreateDraft(
        Action<ConfigurationSnapshot>? save = null
    ) =>
        new(
            new(
                new(),
                [],
                XDocument.Parse(
                    "<groups><selections><selection enabled='no'><name>Science-fiction</name></selection><selection enabled='yes'><name>Famille</name></selection></selections></groups>"
                ),
                []
            ),
            "/workspace",
            [],
            [],
            (snapshot, _, _) =>
            {
                save?.Invoke(snapshot);
                return Task.CompletedTask;
            }
        );
}
