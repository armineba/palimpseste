using NUnit.Framework;
using Palimpseste.Game.Drawing;
using UnityEngine;

namespace Palimpseste.Game.Tests
{
    public sealed class DrawingCanvasTests
    {
        private static readonly Color32 Ink = new Color32(125, 39, 31, 220);

        [Test]
        public void AStrokeCrossingAllRegionsLocksEachActuallyInkedRegion()
        {
            var canvas = new DrawingCanvas();
            Assert.IsTrue(canvas.Begin(new Vector2(512, 512), BrushStyle.Solid, Ink, 14, 1));
            canvas.Move(new Vector2(900, 512), BrushStyle.Solid, Ink, 14, 1);
            canvas.End();
            Assert.IsTrue(canvas.AllLocked);
            Assert.IsTrue(canvas.Closed);
            Assert.IsTrue(canvas.Engaged);
        }

        [Test]
        public void OutsideClickDoesNotSpendInkOrEngageSupport()
        {
            var canvas = new DrawingCanvas();
            Assert.IsFalse(canvas.Begin(new Vector2(0, 0), BrushStyle.Double, Ink, 20, 1));
            Assert.AreEqual(0, canvas.UsedInkMicro);
            Assert.IsFalse(canvas.Engaged);
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
