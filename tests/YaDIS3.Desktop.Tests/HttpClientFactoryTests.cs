using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using YaDIS3.Core;
using YaDIS3.Desktop.ViewModels;

namespace YaDIS3.Desktop.Tests;

public sealed class HttpClientFactoryTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task SearchAsync_ClientInjecte_ConserveRequeteEtGestionErreur(
        HttpStatusCode status
    )
    {
        string credentials = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                credentials,
                "{\"apikey\":\"test-only-api-key\"}",
                Encoding.UTF8
            );
            using RecordingTvdbHandler handler = new(status);
            RecordingHttpClientFactory factory = new(handler);
            using WorkspaceRepository repository = new();
            using MainWindowViewModel model = CreateModel(
                repository,
                factory
            );
            model.ConfigureTvdb(credentials);
            model.Query = "Dragon Ball & été";
            await model.RunAsync(model.SearchAsync);
            Assert.Equal("TVDB", factory.RequestedName);
            Assert.Equal(2, handler.Requests.Count);
            Assert.Contains(
                "query=Dragon%20Ball%20%26%20%C3%A9t%C3%A9&language=fra&limit=50",
                handler.Requests[1],
                StringComparison.Ordinal
            );
            Assert.False(model.IsBusy);
            if (status == HttpStatusCode.OK)
            {
                Assert.Equal(
                    "Titre français",
                    Assert.Single(model.SearchResults).Name
                );
            }
            else
            {
                Assert.Empty(model.SearchResults);
                Assert.Contains(
                    "403",
                    model.Status,
                    StringComparison.Ordinal
                );
            }
        }
        finally
        {
            File.Delete(credentials);
        }
    }

    private static MainWindowViewModel CreateModel(
        IWorkspaceRepository repository,
        IHttpClientFactory factory
    ) =>
        new(
            repository,
            new ImageImporter(new SkiaJpegEncoder()),
            factory,
            new DuneConfigurationStore(),
            new DuneConfigurationService(),
            new SourceFolderScanner(),
            NullLogger<MainWindowViewModel>.Instance,
            NullLogger<ConfigurationViewModel>.Instance
        );
}
