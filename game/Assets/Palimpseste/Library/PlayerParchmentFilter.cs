using System;

namespace Palimpseste.Game.Library
{
    public static class PlayerParchmentFilter
    {
        public static bool IsUserParchment(ParchmentRecord record, string principalId) =>
            !string.IsNullOrEmpty(principalId) && record != null &&
            string.Equals(record.owner_id, principalId, StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParseExact(record.parchment_id, "N", out _) &&
            !string.IsNullOrEmpty(record.capture_key) &&
            (record.server_issued || record.reference_artifact_id == "server");
    }
}
