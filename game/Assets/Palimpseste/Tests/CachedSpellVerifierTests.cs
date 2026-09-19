using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Palimpseste.Game.Library;
using UnityEngine;

namespace Palimpseste.Game.Tests
{
    public sealed class CachedSpellVerifierTests
    {
        [Test]
        public void CompleteOfflinePacketOpensButPartialOrAlteredPacketDoesNot()
        {
            var examples = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "examples"));
            var temporaryRoot = Path.GetFullPath(Application.temporaryCachePath);
            var directory = Path.Combine(temporaryRoot, "palimpseste-cache-" + Guid.NewGuid().ToString("N"));
            var artifacts = Path.Combine(directory, "artifacts");
            Directory.CreateDirectory(artifacts);
            try
            {
                var packet = File.ReadAllBytes(Path.Combine(examples, "03_paquet_illustratif.json"));
                var spellPath = Path.Combine(directory, "spell.json");
                var marker = spellPath + ".sha256";
                File.WriteAllBytes(spellPath, packet);
                File.WriteAllText(marker, ParchmentStore.Hash(packet), Encoding.ASCII);
                Copy(examples, artifacts, "ring.path.0.json", "fixture.ring.path.0");
                Copy(examples, artifacts, "outer.footprint.0.json", "fixture.outer.footprint.0");
                Copy(examples, artifacts, "full.silhouette.0.json", "fixture.full.silhouette.0");
                Copy(examples, artifacts, "full_mask.png", "fixture.mask.full_mask");
                Copy(examples, artifacts, "outer_mask.png", "fixture.mask.outer_mask");
                var record = new ParchmentRecord
                {
                    parchment_id = "fixture-parchment-001",
                    spell_id = "fixture-spell-braise",
                    state = "ready"
                };
                Assert.IsTrue(CachedSpellVerifier.TryLoad(record, directory, out var verified));
                Assert.IsNotEmpty(verified);

                var maskPath = Path.Combine(artifacts, "fixture.mask.outer_mask");
                var mask = File.ReadAllBytes(maskPath);
                var alteredMask = (byte[])mask.Clone();
                alteredMask[alteredMask.Length / 2] ^= 1;
                File.WriteAllBytes(maskPath, alteredMask);
                Assert.IsFalse(CachedSpellVerifier.TryLoad(record, directory, out var partial));
                Assert.IsNull(partial, "No packet may be exposed after one artifact changed");
                File.WriteAllBytes(maskPath, mask);

                File.Delete(maskPath);
                Assert.IsFalse(CachedSpellVerifier.TryLoad(record, directory, out partial));
                Assert.IsNull(partial, "Missing artifact must block the whole spell");
                File.WriteAllBytes(maskPath, mask);

                File.Delete(marker);
                Assert.IsFalse(CachedSpellVerifier.TryLoad(record, directory, out partial));
                Assert.IsNull(partial, "Uncommitted download without marker must stay unavailable");
                File.WriteAllText(marker, ParchmentStore.Hash(packet), Encoding.ASCII);

                var unsupported = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(packet)
                    .Replace("sp.compiler/1.0", "sp.compiler/9.0"));
                File.WriteAllBytes(spellPath, unsupported);
                File.WriteAllText(marker, ParchmentStore.Hash(unsupported), Encoding.ASCII);
                Assert.IsFalse(CachedSpellVerifier.TryLoad(record, directory, out partial));
                Assert.IsNull(partial, "Unsupported compiler version must be rejected even with a matching local marker");

                File.WriteAllBytes(spellPath, packet);
                File.WriteAllText(marker, ParchmentStore.Hash(packet), Encoding.ASCII);
                Assert.IsTrue(CachedSpellVerifier.TryLoad(record, directory, out verified));
            }
            finally
            {
                var full = Path.GetFullPath(directory);
                Assert.IsTrue(full.StartsWith(temporaryRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                if (Directory.Exists(full)) Directory.Delete(full, true);
            }
        }

        private static void Copy(string examples, string artifacts, string name, string id)
        {
            File.Copy(Path.Combine(examples, "geometry", name), Path.Combine(artifacts, id));
        }
    }
}
