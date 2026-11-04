using Xunit;
using YaDIS3.Desktop.Localization;

namespace YaDIS3.Desktop.Tests;

public sealed class LanguageRestartTests
{
    [Fact]
    public void CreateStartInfo_DotnetHost_KeepsDllAndArguments()
    {
        string[] arguments =
        [
            "/Applications/yaDIS 3/yaDIS3.Desktop.dll",
            "--workspace",
            "/Volumes/Films & Séries",
        ];
        var start = DesktopApplicationRestart.CreateStartInfo(
            "/usr/local/bin/dotnet",
            arguments,
            "/Users/greg"
        );
        Assert.Equal(3, start.ArgumentList.Count);
        Assert.Equal(arguments[0], start.ArgumentList[0]);
        Assert.Equal(arguments[2], start.ArgumentList[2]);
        Assert.Equal("/Users/greg", start.WorkingDirectory);
        Assert.False(start.UseShellExecute);
    }

    [Fact]
    public void CreateStartInfo_AppHost_DoesNotPassManagedEntryPointAsAnArgument()
    {
        string[] arguments =
        [
            "yaDIS3.Desktop.dll",
            "--workspace",
            "Films & Séries",
        ];
        var start = DesktopApplicationRestart.CreateStartInfo(
            "yaDIS3.Desktop.exe",
            arguments,
            "workspace"
        );
        Assert.Equal(2, start.ArgumentList.Count);
        Assert.Equal("--workspace", start.ArgumentList[0]);
        Assert.Equal("Films & Séries", start.ArgumentList[1]);
    }
}
