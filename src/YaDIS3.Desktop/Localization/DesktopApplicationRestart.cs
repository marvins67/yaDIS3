using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using YaDIS3.Desktop.Views;

namespace YaDIS3.Desktop.Localization;

/// <summary>Équivalent multiplateforme d'Application.Restart.</summary>
internal sealed class DesktopApplicationRestart
    : IApplicationRestart
{
    public void Restart()
    {
        string executable =
            Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "Application executable unavailable."
            );
        ProcessStartInfo start = CreateStartInfo(
            executable,
            Environment.GetCommandLineArgs(),
            Environment.CurrentDirectory
        );
        using Process? restarted = Process.Start(start);
        if (
            Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop
        )
        {
            if (desktop.MainWindow is MainWindow main)
            {
                main.PrepareForRestart();
            }
            desktop.Shutdown();
        }
    }

    internal static ProcessStartInfo CreateStartInfo(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory
    )
    {
        ProcessStartInfo start = new(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        int first = Path.GetFileNameWithoutExtension(executable)
            .Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            ? 0
            : 1;
        foreach (string argument in arguments.Skip(first))
        {
            start.ArgumentList.Add(argument);
        }
        return start;
    }
}
