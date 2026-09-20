using System.Text.Json;

namespace Palimpseste.Provider;

public sealed partial class LunaCodexProvider
{
    public static string AnimationSheetRequest(byte[] description)
    {
        using var doc = JsonDocument.Parse(description);
        var root = doc.RootElement;
        var children = root.GetProperty("relations").EnumerateArray()
            .Select(r => r.GetProperty("target_subject_id").GetString()).ToHashSet(StringComparer.Ordinal);
        var rootCarriers = root.GetProperty("clauses").EnumerateArray()
            .Where(c => !children.Contains(c.GetProperty("subject_id").GetString()))
            .SelectMany(c => c.GetProperty("facts").EnumerateArray())
            .Where(f => f.GetProperty("dimension").GetString() == "carrier")
            .Select(f => f.GetProperty("value").GetString()).ToArray();
        // Terminal collision/trigger is a useful disappearance branch for a projectile
        // or trap. Sustained fields, beams, barriers and pulses show their natural ending.
        var ending = rootCarriers.FirstOrDefault() is "projectile" or "trap" ? "contact" : "expiration";
        return JsonSerializer.Serialize(new { layout_version = "sp.animation-sheet/1.0", rows = 3, columns = 7, ending_basis = ending });
    }

    private static bool AnimationSheetMatches(JsonElement actual, string expected)
    {
        using var doc = JsonDocument.Parse(expected);
        return actual.ValueKind == JsonValueKind.Object && actual.EnumerateObject().Count() == 4 &&
            doc.RootElement.EnumerateObject().All(p => actual.TryGetProperty(p.Name, out var value) &&
                value.ValueKind == p.Value.ValueKind && value.ToString() == p.Value.ToString());
    }
}
