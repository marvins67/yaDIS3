using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace YaDIS3.Desktop.Views;

public sealed partial class ConfigurationWindow
{
    private bool _folderPickerOpen;

    private async void WorkspaceFocused(
        object? sender,
        FocusChangedEventArgs args
    ) => await BrowseGeneralFolder(true);

    private async void DestinationFocused(
        object? sender,
        FocusChangedEventArgs args
    ) => await BrowseGeneralFolder(false);

    private async void ChooseWorkspace(
        object? sender,
        RoutedEventArgs args
    ) => await BrowseGeneralFolder(true);

    private async void ChooseDestination(
        object? sender,
        RoutedEventArgs args
    ) => await BrowseGeneralFolder(false);

    private async Task BrowseGeneralFolder(bool workspace)
    {
        if (_folderPickerOpen)
        {
            return;
        }
        _folderPickerOpen = true;
        try
        {
            await ChooseGeneralFolder(workspace);
        }
        finally
        {
            this.FindControl<Button>(
                        workspace
                            ? "WorkspaceBrowse"
                            : "DestinationBrowse"
                    )!
                .Focus();
            _folderPickerOpen = false;
        }
    }

    private async Task ChooseGeneralFolder(bool workspace)
    {
        string title = Model.Texts[workspace ? "80" : "82"];
        if (await PickFolder(title) is not { } path)
        {
            return;
        }
        bool created = workspace
            ? Model.ChooseWorkspace(path)
            : Model.ChooseDestination(path);
        if (created)
        {
            await ShowCreatedFolder();
        }
    }

    private async Task ShowCreatedFolder()
    {
        Window prompt = new()
        {
            Title = Model.Texts["139"] + " !",
            Width = 470,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner,
        };
        Button accept = LanguageButton(prompt, "159", true);
        accept.IsCancel = true;
        prompt.Content = CreatedFolderContent(accept);
        await prompt.ShowDialog(this);
    }

    private StackPanel CreatedFolderContent(Button accept) =>
        new()
        {
            Margin = new Thickness(16),
            Spacing = 20,
            Children =
            {
                new TextBlock
                {
                    Text =
                        Model.Texts["140"]
                        + ". "
                        + Model.Texts["141"]
                        + ".",
                    TextWrapping = Avalonia
                        .Media
                        .TextWrapping
                        .Wrap,
                },
                accept,
            },
        };
}
