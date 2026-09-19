using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Palimpseste.Game.Drawing;
using Palimpseste.Game.Library;
using UnityEngine;
using UnityEngine.TestTools;

namespace Palimpseste.Game.Tests
{
    public sealed class ParchmentRecoveryTests
    {
        private static readonly Color32 Ink = new Color32(125, 39, 31, 220);

        [Test]
        public void DurableJournalAheadOfCheckpointClosesOnceAfterRestart()
        {
            var store = new ParchmentStore();
            var record = store.Create(Guid.NewGuid().ToString("N"));
            try
            {
                store.Append(record, "down", new Vector2(512, 512), BrushStyle.Solid, Ink, 14, 1);
                var statePath = Path.Combine(store.DirectoryFor(record), "state.json");
                var olderCheckpoint = File.ReadAllBytes(statePath);
                store.Append(record, "move", new Vector2(620, 512), BrushStyle.Solid, Ink, 14, 1);
                File.WriteAllBytes(statePath, olderCheckpoint); // Simulate interruption before checkpoint save.

                var restarted = new ParchmentStore();
                var loaded = restarted.LoadAll().Find(item => item.local_id == record.local_id);
                Assert.NotNull(loaded);
                var canvas = restarted.Replay(loaded);
                Assert.IsTrue(canvas.Engaged);
                Assert.IsTrue(canvas.Closed);
                Assert.AreEqual("capture_pending", loaded.state);
                Assert.AreEqual("crash_recovered", loaded.closed_reason);
                Assert.IsTrue(loaded.needs_capture);
                Assert.AreEqual(3, loaded.sequence);
                var journal = File.ReadAllLines(Path.Combine(store.DirectoryFor(record), "journal.jsonl"));
                Assert.AreEqual(3, journal.Length);
                StringAssert.Contains("\"op\":\"close\"", journal[2]);

                var secondRestart = new ParchmentStore();
                var second = secondRestart.LoadAll().Find(item => item.local_id == record.local_id);
                Assert.IsTrue(secondRestart.Replay(second).Closed);
                Assert.AreEqual(3, second.sequence, "Recovery must not append a second close event");
            }
            finally { DeleteOwnRecord(store, record); }
        }

        [Test]
        public void TornFinalLineIsDiscardedOnlyForAnOpenDrawing()
        {
            var store = new ParchmentStore();
            var record = store.Create(Guid.NewGuid().ToString("N"));
            try
            {
                store.Append(record, "down", new Vector2(512, 512), BrushStyle.Solid, Ink, 14, 1);
                store.Append(record, "up", Vector2.zero, BrushStyle.Solid, Ink, 14, 1);
                var journalPath = Path.Combine(store.DirectoryFor(record), "journal.jsonl");
                File.AppendAllText(journalPath, "{\"sequence\":3", Encoding.UTF8);
                var canvas = new ParchmentStore().Replay(record);
                Assert.IsTrue(canvas.Closed);
                Assert.AreEqual("capture_pending", record.state);
                Assert.AreEqual(3, File.ReadAllLines(journalPath).Length);
                Assert.IsTrue(File.ReadAllText(journalPath).EndsWith("\n", StringComparison.Ordinal));
            }
            finally { DeleteOwnRecord(store, record); }
        }

        [Test]
        public void CompleteTamperedEventBlocksFurtherWriting()
        {
            var store = new ParchmentStore();
            var record = store.Create(Guid.NewGuid().ToString("N"));
            try
            {
                store.Append(record, "down", new Vector2(512, 512), BrushStyle.Solid, Ink, 14, 1);
                var journalPath = Path.Combine(store.DirectoryFor(record), "journal.jsonl");
                var journal = File.ReadAllText(journalPath);
                var altered = journal.Replace("\"op\":\"down\"", "\"op\":\"move\"");
                Assert.AreNotEqual(journal, altered);
                File.WriteAllText(journalPath, altered, new UTF8Encoding(false));
                LogAssert.Expect(LogType.Error, new Regex("Journal de parchemin corrompu"));
                new ParchmentStore().Replay(record);
                Assert.AreEqual("capture_corrupted", record.state);
                Assert.Throws<InvalidOperationException>(() =>
                    store.Append(record, "down", new Vector2(500, 500), BrushStyle.Solid, Ink, 14, 1));
            }
            finally { DeleteOwnRecord(store, record); }
        }

        private static void DeleteOwnRecord(ParchmentStore store, ParchmentRecord record)
        {
            var root = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "Palimpseste", "parchments"));
            var target = Path.GetFullPath(store.DirectoryFor(record));
            Assert.IsTrue(target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            if (Directory.Exists(target)) Directory.Delete(target, true);
        }
    }
}
