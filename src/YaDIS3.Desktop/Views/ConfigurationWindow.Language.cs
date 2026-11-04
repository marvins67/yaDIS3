using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace YaDIS3.Desktop.Views;

public sealed partial class ConfigurationWindow
{
    private bool _languagePromptOpen;

    private async void LanguageSelected(
        object? sender,
        SelectionChangedEventArgs args
    )
    {
        if (_languagePromptOpen || !Model.HasLanguageChange)
        {
            return;
        }
        _languagePromptOpen = true;
        try
        {
            Model.ConfirmLanguageChange(
                await ConfirmLanguageRestart()
            );
        }
        finally
        {
            _languagePromptOpen = false;
        }
    }

    private async Task<bool> ConfirmLanguageRestart()
    {
        Window prompt = new()
        {
            Title = Model.Texts["71"],
            Width = 470,
            Height = 160,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner,
        };
        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttons.Children.Add(LanguageButton(prompt, "89", true));
        buttons.Children.Add(
            LanguageButton(prompt, "90", false)
        );
        prompt.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 20,
            Children =
            {
                new TextBlock
                {
                    Text = Model.Texts["121"] + ".",
                    TextWrapping = Avalonia
                        .Media
                        .TextWrapping
                        .Wrap,
                },
                buttons,
            },
        };
        return await prompt.ShowDialog<bool>(this);
    }

    private Button LanguageButton(
        Window prompt,
        string identifier,
        bool result
    )
    {
        Button button = new()
        {
            Content = Model.Texts[identifier],
            MinWidth = 75,
            IsDefault = result,
            IsCancel = !result,
        };
        button.Click += (_, _) => prompt.Close(result);
        return button;
    }
}
