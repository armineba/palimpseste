using Palimpseste.Contracts;

namespace Palimpseste.Api;

// Compatibility declaration only, not an authentication or authorization mechanism.
// Historical reads and replay of an already admitted capture do not consult this guard.
public static class GenerationAdmission
{
    public const string ClientVersionHeader = "X-Palimpseste-Client-Version";
    public const string MinimumGenerationClientVersion = SpellBlueprintV2Limits.MinimumClient;

    public static bool SupportsCurrentGeneration(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(ClientVersionHeader, out var values) || values.Count != 1)
            return false;
        var value = values[0];
        // Reject ambiguous lists, prerelease labels, whitespace and unbounded header values.
        if (string.IsNullOrEmpty(value) || value.Length > 32 || value.Any(c => !char.IsAsciiDigit(c) && c != '.') ||
            !Version.TryParse(value, out var version) || version.Build < 0)
            return false;
        return version >= Version.Parse(MinimumGenerationClientVersion);
    }
}
