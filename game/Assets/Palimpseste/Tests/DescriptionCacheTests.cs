using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Palimpseste.Game.Library;
using Palimpseste.Game.Service;
using UnityEngine;

namespace Palimpseste.Game.Tests
{
    public sealed class DescriptionCacheTests
    {
        [Test]
        public void ServerDescriptionCanBeReopenedButCorruptOrUnboundTextCannotBeShown()
        {
            var examples = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "examples"));
            var tempRoot = Path.GetFullPath(Application.temporaryCachePath);
            var directory = Path.Combine(tempRoot, "palimpseste-description-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var bytes = File.ReadAllBytes(Path.Combine(examples, "01_description_illustrative.json"));
                var hash = ParchmentStore.Hash(bytes);
                var record = new ParchmentRecord { parchment_id = "test", job_id = "test-job", server_issued = true };
                const string artifactId = "a0123456789abcdef0123456789abcdef";

                Assert.IsFalse(DescriptionCache.TrySave(record, directory, artifactId, null, bytes, out _),
                    "An artifact without the server content hash must not become player text");
                Assert.IsFalse(DescriptionCache.TryLoad(record, directory, out _));
                Assert.IsTrue(DescriptionCache.TrySave(record, directory, artifactId, hash, bytes, out var first));
                StringAssert.Contains("Braise", first.Title);
                Assert.IsTrue(DescriptionCache.TryLoad(record, directory, out var reopened));
                Assert.AreEqual(first.Summary, reopened.Summary);

                var path = Path.Combine(directory, "description.json");
                File.WriteAllText(path, "{\"schema_version\":\"sp.description/1.0\",\"title\":\"Fausse lecture\"}", Encoding.UTF8);
                Assert.IsFalse(DescriptionCache.TryLoad(record, directory, out _),
                    "A partial or altered cache must not be presented as Luna A");
                Assert.IsFalse(DescriptionCache.TrySave(record, directory, artifactId, hash, Encoding.UTF8.GetBytes("{}"), out _));
                Assert.IsFalse(SpellDescriptionView.TryRead(Encoding.UTF8.GetBytes("{}"), out _));
            }
            finally
            {
                var full = Path.GetFullPath(directory);
                Assert.IsTrue(full.StartsWith(tempRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                if (Directory.Exists(full)) Directory.Delete(full, true);
            }
        }
    }
}
