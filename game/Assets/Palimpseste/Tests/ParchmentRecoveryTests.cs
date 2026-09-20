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
        public void DurableJournalAheadOfCheckpointRestoresOpenDrawingAfterRestart()
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
                Assert.IsFalse(canvas.Closed);
                Assert.AreEqual("writing", loaded.state);
                Assert.IsFalse(loaded.needs_capture);
                Assert.AreEqual(3, loaded.sequence);
                var journal = File.ReadAllLines(Path.Combine(store.DirectoryFor(record), "journal.jsonl"));
                Assert.AreEqual(3, journal.Length);
                StringAssert.Contains("\"op\":\"up\"", journal[2]);

                var secondRestart = new ParchmentStore();
                var second = secondRestart.LoadAll().Find(item => item.local_id == record.local_id);
                Assert.IsFalse(secondRestart.Replay(second).Closed);
                Assert.AreEqual(3, second.sequence, "Recovery must not append a second up event");
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
                Assert.IsFalse(canvas.Closed);
                Assert.AreEqual("writing", record.state);
                Assert.AreEqual(2, File.ReadAllLines(journalPath).Length);
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

        [Test]
        public void HistoricalRecordWithoutRasterVersionKeepsLegacyRegionReplay()
        {
            var store = new ParchmentStore();
            var record = store.Create(Guid.NewGuid().ToString("N"));
            try
            {
                record.raster_version = null;
                record.layout_version = null;
                store.Save(record);
                store.Append(record, "down", new Vector2(512, 512), BrushStyle.Solid, Ink, 14, 1);
                store.Append(record, "up", Vector2.zero, BrushStyle.Solid, Ink, 14, 1);
                var recovered = store.Replay(record);
                Assert.IsTrue(recovered.IsLocked(InkRegion.Core));
                Assert.IsTrue(recovered.Closed);
                Assert.AreEqual("capture_pending", record.state);
                Assert.AreEqual("crash_recovered", record.closed_reason);
            }
            finally { DeleteOwnRecord(store, record); }
        }

        [Test]
        public void ExplicitFinishSurvivesCrashBeforeStateCheckpoint()
        {
            var store = new ParchmentStore();
            var record = store.Create(Guid.NewGuid().ToString("N"));
            try
            {
                var point = new Vector2(512, 512);
                store.Append(record, "down", point, BrushStyle.Solid, Ink, 14, 1);
                store.Append(record, "up", Vector2.zero, BrushStyle.Solid, Ink, 14, 1);
                var statePath = Path.Combine(store.DirectoryFor(record), "state.json");
                var beforeClose = File.ReadAllBytes(statePath);
                store.Append(record, "close", Vector2.zero, BrushStyle.Solid, Ink, 14, 1);
                File.WriteAllBytes(statePath, beforeClose);

                var loaded = new ParchmentStore().LoadAll().Find(item => item.local_id == record.local_id);
                var canvas = store.Replay(loaded);
                Assert.IsTrue(canvas.Closed);
                Assert.AreEqual("capture_pending", loaded.state);
                Assert.AreEqual("user_finished", loaded.closed_reason);
                Assert.IsTrue(loaded.needs_capture);
                Assert.AreEqual(3, loaded.sequence, "the close event must not be duplicated");
            }
            finally { DeleteOwnRecord(store, record); }
        }

        [Test]
        public void HistoricalPendingCaptureKeepsItsClosedJournalAndMetadata()
        {
            var store = new ParchmentStore();
            var record = store.Create(Guid.NewGuid().ToString("N"));
            try
            {
                record.raster_version = null;
                record.layout_version = null;
                store.Save(record);
                store.Append(record, "down", new Vector2(512, 512), BrushStyle.Solid, Ink, 14, 1);
                store.Append(record, "up", Vector2.zero, BrushStyle.Solid, Ink, 14, 1);
                store.Append(record, "close", Vector2.zero, BrushStyle.Solid, Ink, 14, 1);
                record.state = "capture_pending";
                record.closed_reason = "window_closed";
                record.needs_capture = true;
                store.Save(record);

                var loaded = new ParchmentStore().LoadAll().Find(item => item.local_id == record.local_id);
                var canvas = store.Replay(loaded);
                Assert.IsTrue(canvas.Closed);
                Assert.IsTrue(canvas.IsLocked(InkRegion.Core));
                Assert.AreEqual("capture_pending", loaded.state);
                Assert.AreEqual("window_closed", loaded.closed_reason);
                Assert.IsTrue(loaded.needs_capture);
                Assert.AreEqual(3, loaded.sequence);
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
