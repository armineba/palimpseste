using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Palimpseste.Game.Bootstrap;
using Palimpseste.Game.Drawing;
using Palimpseste.Game.Library;
using UnityEngine;
using UnityEngine.TestTools;

namespace Palimpseste.Game.PlayModeTests
{
    public sealed class FocusInterruptionTests
    {
        [UnityTest]
        public IEnumerator FinishingAnEmptyDrawingWarnsUntilTheFirstStroke()
        {
            var app = UnityEngine.Object.FindFirstObjectByType<PalimpsesteApp>();
            Assert.NotNull(app);
            var type = typeof(PalimpsesteApp);
            var store = (ParchmentStore)Field(type, "store").GetValue(app);
            var selectedField = Field(type, "selected");
            var canvasField = Field(type, "canvas");
            var pageField = Field(type, "page");
            var noticeField = Field(type, "notice");
            var oldSelected = selectedField.GetValue(app);
            var oldCanvas = canvasField.GetValue(app);
            var oldPage = pageField.GetValue(app);
            var oldNotice = noticeField.GetValue(app);
            var drawingPage = Enum.Parse(pageField.FieldType, "Drawing");
            var finish = type.GetMethod("FinalizeDrawing", BindingFlags.NonPublic | BindingFlags.Instance);
            var append = type.GetMethod("Append", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(finish);
            Assert.NotNull(append);
            var record = store.Create(Guid.NewGuid().ToString("N"));
            try
            {
                selectedField.SetValue(app, record);
                canvasField.SetValue(app, new DrawingCanvas());
                pageField.SetValue(app, drawingPage);
                finish.Invoke(app, new object[] { "user_finished" });
                Assert.AreEqual(drawingPage, pageField.GetValue(app));
                Assert.AreEqual("Ajoutez au moins un trait avant de terminer.", noticeField.GetValue(app));
                Assert.AreEqual("blank", record.state);
                Assert.IsFalse(record.needs_capture);
                Assert.AreEqual(0, record.sequence);

                // The first durable down event is the point where the UI must clear the warning.
                var canvas = (DrawingCanvas)canvasField.GetValue(app);
                var point = new Vector2(512, 512);
                Assert.IsTrue(canvas.Begin(point, BrushStyle.Solid, new Color32(125, 39, 31, 220), 14, 1));
                append.Invoke(app, new object[] { "down", point, 1f });
                Assert.AreEqual("Dessin en cours. Ajoutez des traits, puis cliquez sur Dessin terminé.",
                    noticeField.GetValue(app));
                Assert.IsTrue(canvas.Engaged);
                Assert.AreEqual(1, record.sequence);
            }
            finally
            {
                selectedField.SetValue(app, oldSelected);
                canvasField.SetValue(app, oldCanvas);
                pageField.SetValue(app, oldPage);
                noticeField.SetValue(app, oldNotice);
                DeleteOwnRecord(store, record);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LosingFocusEndsAndPersistsTheCurrentStrokeWithoutClosingTheDrawing()
        {
            var app = UnityEngine.Object.FindFirstObjectByType<PalimpsesteApp>();
            Assert.NotNull(app);
            var type = typeof(PalimpsesteApp);
            var store = (ParchmentStore)Field(type, "store").GetValue(app);
            var selectedField = Field(type, "selected");
            var canvasField = Field(type, "canvas");
            var pageField = Field(type, "page");
            var oldSelected = selectedField.GetValue(app);
            var oldCanvas = canvasField.GetValue(app);
            var oldPage = pageField.GetValue(app);
            var drawingPage = Enum.Parse(pageField.FieldType, "Drawing");
            var focusMethod = type.GetMethod("OnApplicationFocus", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(focusMethod);
            var record = store.Create(Guid.NewGuid().ToString("N"));
            try
            {
                var canvas = new DrawingCanvas();
                var point = new Vector2(512, 512);
                var ink = new Color32(125, 39, 31, 220);
                Assert.IsTrue(canvas.Begin(point, BrushStyle.Solid, ink, 14, 1));
                store.Append(record, "down", point, BrushStyle.Solid, ink, 14, 1);
                selectedField.SetValue(app, record);
                canvasField.SetValue(app, canvas);
                pageField.SetValue(app, drawingPage);

                focusMethod.Invoke(app, new object[] { false });
                Assert.IsFalse(canvas.IsDrawing);
                Assert.IsFalse(canvas.IsLocked(InkRegion.Core));
                Assert.IsFalse(canvas.Closed, "Focus loss must leave the whole drawing available");
                Assert.IsTrue(canvas.Begin(point, BrushStyle.Solid, ink, 14, 1),
                    "a second stroke may start at the same location");
                canvas.End();
                Assert.AreEqual(2, record.sequence);
                Assert.AreEqual("writing", record.state);
                var journal = File.ReadAllText(Path.Combine(store.DirectoryFor(record), "journal.jsonl"));
                StringAssert.Contains("\"op\":\"up\"", journal);
                focusMethod.Invoke(app, new object[] { true });
                Assert.AreEqual(2, record.sequence, "Focus regain must not append another event");
            }
            finally
            {
                selectedField.SetValue(app, oldSelected);
                canvasField.SetValue(app, oldCanvas);
                pageField.SetValue(app, oldPage);
                DeleteOwnRecord(store, record);
            }
            yield return null;
        }

        private static void DeleteOwnRecord(ParchmentStore store, ParchmentRecord record)
        {
            var root = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "Palimpseste", "parchments"));
            var target = Path.GetFullPath(store.DirectoryFor(record));
            Assert.IsTrue(target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            if (Directory.Exists(target)) Directory.Delete(target, true);
        }

        private static FieldInfo Field(Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, name);
            return field;
        }
    }
}
