namespace YaDIS3.Desktop.ViewModels;

/// <summary>Champs modifiables d'une saga historique.</summary>
public sealed record SagaContextDraft(
    string Name,
    string SortName,
    string Poster,
    string Fanart,
    IReadOnlyList<string> Members
);
