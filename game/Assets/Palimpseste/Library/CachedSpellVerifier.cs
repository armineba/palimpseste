using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Palimpseste.Game.Library
{
    public static class CachedSpellVerifier
    {
        private static readonly Regex ArtifactId = new Regex("^[a-z][a-z0-9_.-]{0,63}$", RegexOptions.CultureInvariant);
        private static readonly Regex Sha256 = new Regex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);

        public static bool TryLoad(ParchmentRecord record, string parchmentDirectory, out string json)
        {
            json = null;
            if (record == null || string.IsNullOrEmpty(record.parchment_id) || string.IsNullOrEmpty(record.spell_id))
                return false;
            try
            {
                var spellPath = Path.Combine(parchmentDirectory, "spell.json");
                var markerPath = spellPath + ".sha256";
                if (!File.Exists(spellPath) || !File.Exists(markerPath)) return false;
                var bytes = File.ReadAllBytes(spellPath);
                var marker = File.ReadAllText(markerPath, Encoding.ASCII).Trim();
                if (!Sha256.IsMatch(marker) || ParchmentStore.Hash(bytes) != marker) return false;

                var packet = JObject.Parse(Encoding.UTF8.GetString(bytes));
                if (Value(packet, "schema_version") != "sp.compiled/1.0" ||
                    Value(packet, "spell_id") != record.spell_id ||
                    Value(packet, "parchment_id") != record.parchment_id ||
                    Value(packet, "versions", "catalog") != "sp.capabilities/1.0" ||
                    Value(packet, "versions", "compiler") != "sp.compiler/1.0" ||
                    Value(packet, "versions", "geometry") != "sp.geometry/1.0" ||
                    Value(packet, "versions", "rules_profile") != "lab_v1" ||
                    !SupportedMinimumClient(Value(packet, "versions", "min_client")) ||
                    Value(packet, "plan", "schema_version") != "sp.plan/1.0")
                    return false;
                if (!(packet["geometry_manifest"] is JArray geometries) ||
                    !(packet["binary_assets"] is JArray binaries))
                    return false;

                var seen = new HashSet<string>(StringComparer.Ordinal);
                var artifacts = Path.GetFullPath(Path.Combine(parchmentDirectory, "artifacts"));
                foreach (var item in geometries)
                    if (!VerifyArtifact(item, artifacts, seen)) return false;
                foreach (var item in binaries)
                    if (!VerifyArtifact(item, artifacts, seen)) return false;
                json = Encoding.UTF8.GetString(bytes);
                return true;
            }
            catch { return false; }
        }

        private static string Value(JToken root, params string[] path)
        {
            foreach (var part in path)
            {
                root = root?[part];
                if (root == null) return null;
            }
            return root.Type == JTokenType.String ? root.Value<string>() : null;
        }

        private static bool SupportedMinimumClient(string version) => version == "1.0.0" || version == "1.1.0";

        private static bool VerifyArtifact(JToken item, string root, HashSet<string> seen)
        {
            var id = Value(item, "artifact_id");
            var hash = Value(item, "sha256");
            if (id == null || hash == null || !ArtifactId.IsMatch(id) || !Sha256.IsMatch(hash) || !seen.Add(id))
                return false;
            var path = Path.GetFullPath(Path.Combine(root, id));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(path))
                return false;
            var bytes = File.ReadAllBytes(path);
            var size = item["size_bytes"];
            return size?.Type == JTokenType.Integer && size.Value<long>() == bytes.LongLength &&
                ParchmentStore.Hash(bytes) == hash;
        }
    }
}
