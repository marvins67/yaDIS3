using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private async Task DeleteSelectedMediaAsync()
    {
        Workspace workspace = _workspace
            ?? throw new InvalidOperationException(
                Texts.GetString("340")
            );
        MediaCard[] selected = TreatmentSelection.Count > 0
            ? TreatmentSelection.Distinct().ToArray()
            : SelectedCard is { } current ? [current] : [];
        if (selected.Length == 0)
        {
            return;
        }

        LegacyMediaDeletionResult result = await Task.Run(
            () => new LegacyMediaDeletion(_configurationStore).Delete(
                workspace.Directory, selected, OperationToken
            ),
            OperationToken
        );

        foreach (MediaCard card in selected.Where(card =>
            result.DeletedDataPaths.Contains(card.FilePath)
        ))
        {
            card.PropertyChanged -= CardChanged;
            Cards.Remove(card);
            card.Dispose();
        }
        TreatmentSelection.Clear();
        ReloadFilterGroups();
        RefreshFilter();
        NotifyAll();
        Status = result.Errors.Count == 0
            ? Texts.GetString("143")
            : string.Join(" · ", result.Errors);
    }
}
