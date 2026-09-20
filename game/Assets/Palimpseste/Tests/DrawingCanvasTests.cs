using NUnit.Framework;
using Palimpseste.Game.Drawing;
using UnityEngine;

namespace Palimpseste.Game.Tests
{
    public sealed class DrawingCanvasTests
    {
        private static readonly Color32 Ink = new Color32(125, 39, 31, 220);

        [Test]
        public void FreeCanvasAcceptsTheWholeSquareAndSeveralStrokesWithoutClosing()
        {
            var canvas = new DrawingCanvas();
            Assert.IsTrue(canvas.Begin(new Vector2(5, 5), BrushStyle.Solid, Ink, 14, 1));
            canvas.Move(new Vector2(900, 512), BrushStyle.Solid, Ink, 14, 1);
            canvas.End();
            Assert.IsFalse(canvas.AllLocked);
            Assert.IsFalse(canvas.Closed);
            Assert.Greater(canvas.InkPixels[5 * DrawingCanvas.Size + 5].a, 0, "corner is drawable");
            Assert.IsTrue(canvas.Begin(new Vector2(512, 512), BrushStyle.Double, Ink, 14, 1),
                "lifting the pointer must not lock the center");
            canvas.End();
            Assert.IsFalse(canvas.Closed);
            Assert.IsTrue(canvas.Engaged);
            canvas.Close();
            Assert.IsTrue(canvas.Closed);
        }

        [Test]
        public void OutsideSquareClickDoesNotSpendInkOrEngageSupport()
        {
            var canvas = new DrawingCanvas();
            Assert.IsFalse(canvas.Begin(new Vector2(-1, 0), BrushStyle.Double, Ink, 20, 1));
            Assert.AreEqual(0, canvas.UsedInkMicro);
            Assert.IsFalse(canvas.Engaged);
        }

        [Test]
        public void LegacyRasterStillClipsAndLocksRegionsDuringReplay()
        {
            var canvas = new DrawingCanvas(true);
            Assert.IsFalse(canvas.Begin(new Vector2(0, 0), BrushStyle.Solid, Ink, 14, 1));
            Assert.IsTrue(canvas.Begin(new Vector2(512, 512), BrushStyle.Solid, Ink, 14, 1));
            canvas.End();
            Assert.IsTrue(canvas.IsLocked(InkRegion.Core));
            Assert.IsFalse(canvas.Begin(new Vector2(512, 512), BrushStyle.Solid, Ink, 14, 1));
        }

        [Test]
        public void SamePathAtDifferentPointerRatesYieldsSamePixelsAndInkCost()
        {
            var slow = new DrawingCanvas();
            var fast = new DrawingCanvas();
            Assert.IsTrue(slow.Begin(new Vector2(270, 512), BrushStyle.Dotted, Ink, 12, 1));
            Assert.IsTrue(fast.Begin(new Vector2(270, 512), BrushStyle.Dotted, Ink, 12, 1));
            slow.Move(new Vector2(750, 512), BrushStyle.Dotted, Ink, 12, 1);
            for (var i = 1; i <= 120; i++) fast.Move(new Vector2(270 + i * 4, 512), BrushStyle.Dotted, Ink, 12, 1);
            slow.End(); fast.End();
            Assert.AreEqual(slow.UsedInkMicro, fast.UsedInkMicro);
            var a = slow.InkPixels; var b = fast.InkPixels;
            for (var i = 0; i < a.Length; i++) Assert.AreEqual(a[i], b[i], "pixel " + i);
        }
    }
}
