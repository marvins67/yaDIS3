using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xunit;
using YaDIS3.Desktop.ViewModels;
using YaDIS3.Desktop.Views;

namespace YaDIS3.Desktop.Tests;

[Collection("Avalonia UI")]
public sealed class SagaEditorWindowTests
{
    [Fact]
    public void SagaEditorWindow_MembersAndArtwork_AreInVisualTree()
    {
        AppBuilder
            .Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .SetupWithoutStarting();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "yadis-saga-editor-" + Guid.NewGuid().ToString("N")
        );
        string movie = "Film.mkv";
        string data = Path.Combine(workspace, "data", movie);
        Directory.CreateDirectory(data);
        // Image PNG valide (son extension historique reste .jpg).
        byte[] image = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwC" +
            "AAAAC0lEQVR42mP8/x8AAwMCAO+/j1sAAAAASUVORK5CYII="
        );
        File.WriteAllBytes(Path.Combine(data, "poster.jpg"), image);
        File.WriteAllBytes(Path.Combine(data, "fanart.jpg"), image);
        SagaContextDraft draft = new(
            "Collection",
            "Collection",
            movie,
            movie,
            [movie]
        );
        SagaEditorWindow editor = new(draft, workspace);
        try
        {
            editor.Show();
            Grid root = Assert.IsType<Grid>(editor.Content);
            Grid gallery = Assert.Single(root.Children.OfType<Grid>());
            ListBox members = Assert.Single(
                gallery.Children.OfType<ListBox>()
            );
            Assert.Equal(movie, Assert.Single(
                members.ItemsSource!.OfType<string>()
            ));
            Button[] artworkButtons = gallery.Children
                .OfType<Button>().ToArray();
            Assert.Equal(2, artworkButtons.Length);
            Assert.All(artworkButtons, button =>
            {
                Image picture = Assert.IsType<Image>(button.Content);
                Assert.NotNull(picture.Source);
                Assert.Equal(Stretch.Fill, picture.Stretch);
            });
        }
        finally
        {
            editor.Close();
            Directory.Delete(workspace, true);
        }
    }
}
