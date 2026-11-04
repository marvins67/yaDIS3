using System.Text.Json;

namespace YaDIS3.Core;

public sealed partial class TvdbClient
{
    private static string[] ReadNames(JsonElement node, string collection)
    {
        JsonElement list = ReadChild(node, collection);
        return list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(item => ReadText(item, "name"))
                .Where(name => name.Length > 0).ToArray()
            : [];
    }

    private static string[] ReadPeople(JsonElement node, params string[] roles)
    {
        JsonElement list = ReadChild(node, "characters");
        return list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray()
                .Where(item => roles.Any(role => ReadText(item, "peopleType")
                    .Contains(role, StringComparison.OrdinalIgnoreCase)))
                .Select(item => ReadText(item, "personName"))
                .Where(name => name.Length > 0).Distinct(StringComparer.Ordinal).ToArray()
            : [];
    }

    private async Task<string> EpisodeDirectorsAsync(
        JsonElement episode, CancellationToken cancellationToken
    )
    {
        long id = ReadNumber(episode, "id");
        if (id <= 0)
        {
            return string.Join("|", ReadPeople(episode, "director"));
        }
        using JsonDocument detail = await GetAsync(
            $"episodes/{id}/extended?meta=translations", cancellationToken
        );
        return string.Join("|", ReadPeople(ReadChild(detail.RootElement, "data"), "director"));
    }
}
