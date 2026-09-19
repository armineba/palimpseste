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
                Assert.IsTrue(canvas.IsLocked(InkRegion.Core));
                Assert.IsFalse(canvas.Closed, "Focus loss must leave untouched regions available");
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
                var root = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "Palimpseste", "parchments"));
                var target = Path.GetFullPath(store.DirectoryFor(record));
                Assert.IsTrue(target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                if (Directory.Exists(target)) Directory.Delete(target, true);
            }
            yield return null;
        }

        private static FieldInfo Field(Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, name);
            return field;
        }
    }
}
