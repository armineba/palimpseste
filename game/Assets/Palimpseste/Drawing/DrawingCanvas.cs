using System;
using UnityEngine;

namespace Palimpseste.Game.Drawing
{
    public enum InkRegion { Outside = -1, Core = 0, Ring = 1, Outer = 2 }
    public enum BrushStyle { Solid, Double, Dotted }

    // All marks, including the exported PNG, are made from this CPU raster. UI scaling never changes it.
    public sealed class DrawingCanvas
    {
        public const int Size = 1024;
        public const long InkLimitMicro = 1000000000L;
        public const string RasterVersion = "cpu-brush/2.0";
        public const string LegacyRasterVersion = "cpu-brush/1.0";
        private const int CostPerOpaquePixelMicro = 5000;
        private readonly Color32[] ink = new Color32[Size * Size];
        private readonly bool[] locked = new bool[3];
        private readonly bool[] touched = new bool[3];
        private readonly bool legacyRegions;
        private Texture2D texture;
        private Vector2 previous;
        private float walked;
        private float carry;
        private bool active;
        private bool anyStamp;

        public long UsedInkMicro { get; private set; }
        public bool Engaged => UsedInkMicro > 0;
        public bool UsesLegacyRegions => legacyRegions;
        public bool Closed { get; private set; }
        public bool IsDrawing => active;
        public bool IsLocked(InkRegion region) => legacyRegions && region != InkRegion.Outside && locked[(int)region];
        public bool AllLocked => legacyRegions && locked[0] && locked[1] && locked[2];
        public bool InkExhausted => UsedInkMicro >= InkLimitMicro;
        public Color32[] InkPixels => ink;

        public Texture2D Texture
        {
            get
            {
                if (texture == null)
                {
                    texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
                    texture.wrapMode = TextureWrapMode.Clamp;
                    texture.filterMode = FilterMode.Bilinear;
                    Refresh();
                }
                return texture;
            }
        }

        public DrawingCanvas(bool legacyRegions = false)
        {
            this.legacyRegions = legacyRegions;
            for (var i = 0; i < ink.Length; i++) ink[i] = new Color32(0, 0, 0, 0);
        }

        private bool CanInk(int x, int y) => x >= 0 && x < Size && y >= 0 && y < Size &&
            (!legacyRegions || (Region(x, y) != InkRegion.Outside && !IsLocked(Region(x, y))));

        public static InkRegion Region(int x, int y)
        {
            var u = (x + 0.5f) / Size;
            var v = (y + 0.5f) / Size;
            var dx = 2f * u - 1f;
            var dy = 2f * v - 1f;
            var r2 = dx * dx + dy * dy;
            if (r2 > 1f) return InkRegion.Outside;
            if (r2 < 0.32f * 0.32f) return InkRegion.Core;
            return r2 < 0.68f * 0.68f ? InkRegion.Ring : InkRegion.Outer;
        }

        public bool Begin(Vector2 point, BrushStyle style, Color32 color, float diameter, float pressure)
        {
            if (Closed || active || InkExhausted || !CanInk(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y))) return false;
            active = true;
            carry = 0f;
            anyStamp = false;
            Array.Clear(touched, 0, touched.Length);
            previous = point;
            walked = 0f;
            Stamp(point, Vector2.right, style, color, diameter, pressure);
            return true;
        }

        public void Move(Vector2 point, BrushStyle style, Color32 color, float diameter, float pressure)
        {
            if (!active || Closed) return;
            var delta = point - previous;
            var length = delta.magnitude;
            if (length < 0.0001f) return;
            var direction = delta / length;
            var step = Mathf.Max(1f, diameter * Mathf.Clamp(pressure, 0.25f, 1.5f) / 4f);
            var distance = step - carry;
            while (distance <= length)
            {
                var p = previous + direction * distance;
                if (style != BrushStyle.Dotted || (walked % Mathf.Max(2f, diameter * 2.4f)) < diameter * 0.55f)
                    Stamp(p, direction, style, color, diameter, pressure);
                walked += step;
                distance += step;
            }
            carry = Mathf.Max(0f, length - (distance - step));
            previous = point;
            if (InkExhausted) End();
        }

        public void End()
        {
            if (!active) return;
            active = false;
            if (legacyRegions)
                for (var i = 0; i < touched.Length; i++) if (touched[i]) locked[i] = true;
            if (AllLocked || (legacyRegions && InkExhausted)) Closed = true;
            if (anyStamp) Refresh();
        }

        public void Close()
        {
            End();
            if (Engaged) Closed = true;
        }

        private void Stamp(Vector2 point, Vector2 direction, BrushStyle style, Color32 color, float diameter, float pressure)
        {
            var d = Mathf.Clamp(diameter * Mathf.Clamp(pressure, 0.25f, 1.5f), 2f, 48f);
            var perp = new Vector2(-direction.y, direction.x);
            if (style == BrushStyle.Double)
            {
                StampCircle(point + perp * d * 0.3f, d * 0.28f, color);
                StampCircle(point - perp * d * 0.3f, d * 0.28f, color);
            }
            else StampCircle(point, style == BrushStyle.Dotted ? d * 0.42f : d * 0.5f, color);
        }

        private void StampCircle(Vector2 point, float radius, Color32 color)
        {
            var xmin = Mathf.Max(0, Mathf.FloorToInt(point.x - radius));
            var xmax = Mathf.Min(Size - 1, Mathf.CeilToInt(point.x + radius));
            var ymin = Mathf.Max(0, Mathf.FloorToInt(point.y - radius));
            var ymax = Mathf.Min(Size - 1, Mathf.CeilToInt(point.y + radius));
            var rr = radius * radius;
            long fullCost = 0;
            for (var y = ymin; y <= ymax; y++)
                for (var x = xmin; x <= xmax; x++)
                    if ((x - point.x) * (x - point.x) + (y - point.y) * (y - point.y) <= rr && CanInk(x, y))
                        fullCost += (long)CostPerOpaquePixelMicro * color.a / 255;
            if (fullCost == 0) return;
            var remaining = InkLimitMicro - UsedInkMicro;
            if (remaining <= 0) return;
            var fraction = Math.Min(1.0, (double)remaining / fullCost);
            long charged = 0;
            for (var y = ymin; y <= ymax; y++)
                for (var x = xmin; x <= xmax; x++)
                {
                    if ((x - point.x) * (x - point.x) + (y - point.y) * (y - point.y) > rr) continue;
                    if (!CanInk(x, y)) continue;
                    var sourceA = (byte)Mathf.Clamp(Mathf.RoundToInt((float)(color.a * fraction)), 0, 255);
                    if (sourceA == 0) continue;
                    var index = y * Size + x;
                    var old = ink[index];
                    var a = sourceA / 255f;
                    var oa = old.a / 255f;
                    var resultA = a + oa * (1 - a);
                    ink[index] = new Color32(
                        (byte)Mathf.RoundToInt((color.r * a + old.r * oa * (1 - a)) / resultA),
                        (byte)Mathf.RoundToInt((color.g * a + old.g * oa * (1 - a)) / resultA),
                        (byte)Mathf.RoundToInt((color.b * a + old.b * oa * (1 - a)) / resultA),
                        (byte)Mathf.RoundToInt(resultA * 255f));
                    if (legacyRegions) touched[(int)Region(x, y)] = true;
                    anyStamp = true;
                    charged += (long)CostPerOpaquePixelMicro * sourceA / 255;
                }
            UsedInkMicro = Math.Min(InkLimitMicro, UsedInkMicro + Math.Min(charged, remaining));
            if (fraction < 1.0 && charged == 0) UsedInkMicro = InkLimitMicro;
            Refresh();
        }

        public byte[] ExportInkPng()
        {
            var t = Texture;
            t.SetPixels32(ink);
            t.Apply(false, false);
            return t.EncodeToPNG();
        }

        public byte[] ExportDrawingPng(Color32[] reference)
        {
            if (reference == null || reference.Length != ink.Length) throw new ArgumentException("Reference PNG mismatch");
            var composite = new Color32[ink.Length];
            for (var i = 0; i < ink.Length; i++)
            {
                var a = ink[i].a / 255f;
                composite[i] = new Color32(
                    (byte)Mathf.RoundToInt(ink[i].r * a + reference[i].r * (1 - a)),
                    (byte)Mathf.RoundToInt(ink[i].g * a + reference[i].g * (1 - a)),
                    (byte)Mathf.RoundToInt(ink[i].b * a + reference[i].b * (1 - a)), 255);
            }
            var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
            t.SetPixels32(composite);
            t.Apply(false, false);
            var bytes = t.EncodeToPNG();
            UnityEngine.Object.Destroy(t);
            return bytes;
        }

        public void Refresh()
        {
            if (texture == null) return;
            texture.SetPixels32(ink);
            texture.Apply(false, false);
        }
    }
}
