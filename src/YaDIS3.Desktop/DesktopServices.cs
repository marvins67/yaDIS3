using Microsoft.Extensions.DependencyInjection;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;

namespace YaDIS3.Desktop;

/// <summary>
/// Centralise la création et la durée de vie des services de
/// l'application.
/// </summary>
internal static class DesktopServices
{
    internal const string TvdbClientName = "TVDB";

    internal static ServiceProvider CreateProvider(
        string? userConfigurationPath = null
    )
    {
        ServiceCollection services = new();
        services.AddHttpClient(
            TvdbClientName,
            client => client.Timeout = TimeSpan.FromSeconds(45)
        );
        services.AddSingleton<
            IWorkspaceRepository,
            WorkspaceRepository
        >();
        services.AddSingleton<IJpegEncoder, SkiaJpegEncoder>();
        services.AddSingleton<ImageImporter>();
        services.AddSingleton<
            IExportFileSystem,
            ExportFileSystem
        >();
        services.AddSingleton(
            provider => new DuneConfigurationStore(
                provider.GetRequiredService<IExportFileSystem>(),
                userConfigurationPath
            )
        );
        services.AddSingleton<DuneConfigurationService>();
        services.AddSingleton<SourceFolderScanner>();
        services.AddSingleton<MainWindowViewModel>();
        return services.BuildServiceProvider();
    }
}
