using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text;

namespace Palimpseste.Provider;

/// <summary>
/// Fixed application compositor for an unlabelled, margin-free 7 by 3 generated PNG atlas.
/// It draws the sheet furniture, never synthesizes, retimes or interprets an animation frame.
/// Input and output stay in memory. The caller must retain the original atlas and its hash
/// separately from this derived sheet; the returned bytes are not a native model output.
/// </summary>
[SupportedOSPlatform("windows")]
public static class AnimationSheetComposer
{
    public const string Version = "sp.animation-sheet-composer/1.0";
    public const int Width = 1536;
    public const int Height = 1152;
    public const int Columns = 7;
    public const int Rows = 3;
    public const int Margin = 32;
    public const int FrameSize = 200;
    public const int ColumnGap = 12;
    public const int FirstRowTop = 190;
    public const int RowStride = 303;
    public const int FrameTopOffset = 58;

    private static readonly Color Background = Color.FromArgb(0x0c, 0x11, 0x16);
    private static readonly Color Foreground = Color.FromArgb(0xe9, 0xf0, 0xf6);
    private static readonly Color Secondary = Color.FromArgb(0x9c, 0xaf, 0xbf);
    private static readonly string[] PhaseNames = ["APPARITION", "STABLE", "DISPARITION"];
    private static readonly Color[] PhaseColors =
        [Color.FromArgb(0x49, 0xc8, 0xff), Color.FromArgb(0x6d, 0xdf, 0xb0), Color.FromArgb(0xb8, 0x99, 0xff)];

    /// <summary>
    /// Validate a bounded RGB/RGBA8 atlas, crop its 21 proportional cells, and compose a
    /// 1536 by 1152 PNG. The entire content of each cell is fitted into a square frame,
    /// preserving aspect ratio. No palette substitution, colour matrix or tonal filter is used.
    /// For the expected 1536 by 1024 atlas, each source column is 219 or 220 pixels wide;
    /// integer proportional boundaries keep every pixel in exactly one source cell.
    /// </summary>
    public static byte[] Compose(byte[] atlas, string title)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("animation_sheet_requires_windows");
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(title);
        if (title.Length > 320) throw new ArgumentException("animation_sheet_title_length", nameof(title));
        var dimensions = VisualReferencePng.Validate(atlas);
        var heading = NormalizeTitle(title);

        using var input = new MemoryStream(atlas, writable: false);
        // PNG structure, exact decompressed size and pixel dimensions were checked above.
        // Disabling embedded colour management preserves the artist's RGB palette.
        // Keep this stream alive until the decoded bitmap and all crops are disposed.
        using var decoded = Image.FromStream(input, useEmbeddedColorManagement: false, validateImageData: true);
        if (decoded is not Bitmap source || source.Width != dimensions.Width || source.Height != dimensions.Height)
            throw new InvalidDataException("animation_sheet_atlas_dimensions");

        // An opaque 24-bit output is sufficient for the fixed dark sheet and keeps even
        // uncompressible output below the 8 MiB artifact budget at these dimensions.
        using var sheet = new Bitmap(Width, Height, PixelFormat.Format24bppRgb);
        sheet.SetResolution(96, 96);
        using (var graphics = Graphics.FromImage(sheet))
        using (var family = ResolveFontFamily())
        {
            graphics.PageUnit = GraphicsUnit.Pixel;
            graphics.Clear(Background);
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            DrawHeader(graphics, family, heading);
            for (var row = 0; row < Rows; row++)
            {
                DrawPhaseHeading(graphics, family, row);
                for (var column = 0; column < Columns; column++)
                {
                    var sourceBounds = Rectangle.FromLTRB(
                        column * source.Width / Columns, row * source.Height / Rows,
                        (column + 1) * source.Width / Columns, (row + 1) * source.Height / Rows);
                    // A separate crop prevents the interpolation filter from sampling the
                    // preceding/following animation cell across an atlas boundary.
                    using var cell = source.Clone(sourceBounds, PixelFormat.Format32bppArgb);
                    DrawCell(graphics, family, cell, row, column);
                }
            }
        }
        using var output = new MemoryStream();
        sheet.Save(output, ImageFormat.Png);
        if (output.Length > VisualReferencePng.MaxBytes)
            throw new InvalidDataException("animation_sheet_output_size");
        var bytes = output.ToArray();
        var actual = VisualReferencePng.Validate(bytes);
        if (actual.Width != Width || actual.Height != Height)
            throw new InvalidDataException("animation_sheet_output_dimensions");
        return bytes;
    }

    /// <summary>Fixed square frame coordinates, excluding the number beneath it.</summary>
    public static Rectangle FrameBounds(int row, int column)
    {
        if (row is < 0 or >= Rows) throw new ArgumentOutOfRangeException(nameof(row));
        if (column is < 0 or >= Columns) throw new ArgumentOutOfRangeException(nameof(column));
        return new(Margin + column * (FrameSize + ColumnGap),
            FirstRowTop + row * RowStride + FrameTopOffset, FrameSize, FrameSize);
    }

    private static void DrawHeader(Graphics graphics, FontFamily family, string title)
    {
        using var white = new SolidBrush(Foreground);
        using var muted = new SolidBrush(Secondary);
        using var titleFormat = SingleLineFormat(StringAlignment.Near);
        using var heading = FitFont(graphics, family, title, 44, 20, Width - 2 * Margin, titleFormat);
        graphics.DrawString(title, heading, white, new RectangleF(Margin, 39, Width - 2 * Margin, 64), titleFormat);
        using var subtitle = CreateFont(family, 17, bold: false);
        graphics.DrawString("VFX ANIMATION SHEET", subtitle, muted,
            new RectangleF(Margin + 1, 111, Width - 2 * Margin, 28), titleFormat);
        using var divider = new Pen(Color.FromArgb(0x32, 0x44, 0x52), 1);
        graphics.DrawLine(divider, Margin, 158, Width - Margin, 158);
    }

    private static void DrawPhaseHeading(Graphics graphics, FontFamily family, int row)
    {
        var top = FirstRowTop + row * RowStride;
        var color = PhaseColors[row];
        using var text = new SolidBrush(color);
        using var font = CreateFont(family, 26, bold: true);
        using var format = SingleLineFormat(StringAlignment.Near);
        graphics.DrawString(PhaseNames[row], font, text, new RectangleF(Margin, top, Width - 2 * Margin, 35), format);
        using var faintLine = new Pen(Color.FromArgb(75, color), 1);
        using var accentLine = new Pen(color, 2);
        graphics.DrawLine(faintLine, Margin, top + 43, Width - Margin, top + 43);
        graphics.DrawLine(accentLine, Margin, top + 43, Margin + 190, top + 43);
    }

    private static void DrawCell(Graphics graphics, FontFamily family, Bitmap cell, int row, int column)
    {
        var frame = FrameBounds(row, column);
        // The background remains neutral. Blue, green and violet are limited to sheet
        // decoration; neither the spell frames nor their glow are recoloured by row.
        using var background = new SolidBrush(Background);
        graphics.FillRectangle(background, frame);
        var inner = Rectangle.Inflate(frame, -4, -4);
        var factor = Math.Min((double)inner.Width / cell.Width, (double)inner.Height / cell.Height);
        var destinationWidth = Math.Clamp((int)Math.Round(cell.Width * factor), 1, inner.Width);
        var destinationHeight = Math.Clamp((int)Math.Round(cell.Height * factor), 1, inner.Height);
        var destination = new Rectangle(inner.X + (inner.Width - destinationWidth) / 2,
            inner.Y + (inner.Height - destinationHeight) / 2, destinationWidth, destinationHeight);
        using (var imageAttributes = new ImageAttributes())
        {
            imageAttributes.SetWrapMode(WrapMode.TileFlipXY);
            graphics.DrawImage(cell, destination, 0, 0, cell.Width, cell.Height, GraphicsUnit.Pixel, imageAttributes);
        }
        using var edge = new Pen(Color.FromArgb(80, PhaseColors[row]), 1);
        using var corner = new Pen(Color.FromArgb(170, PhaseColors[row]), 1);
        graphics.DrawRectangle(edge, frame.X + .5f, frame.Y + .5f, frame.Width - 1, frame.Height - 1);
        graphics.DrawLine(corner, frame.Left + 1, frame.Top + 1, frame.Left + 12, frame.Top + 1);
        graphics.DrawLine(corner, frame.Left + 1, frame.Top + 1, frame.Left + 1, frame.Top + 12);
        using var numberBrush = new SolidBrush(Secondary);
        using var number = CreateFont(family, 18, bold: false);
        using var format = SingleLineFormat(StringAlignment.Center);
        graphics.DrawString((column + 1).ToString(CultureInfo.InvariantCulture), number, numberBrush,
            new RectangleF(frame.X, frame.Bottom + 10, frame.Width, 27), format);
    }

    private static FontFamily ResolveFontFamily()
    {
        // System-installed fonts only. No title, atlas metadata or model output can
        // select a font path or cause a font file/package to be read or downloaded.
        foreach (var name in new[] { "Segoe UI", "Arial" })
            try { return new FontFamily(name); }
            catch (ArgumentException) { }
        return FontFamily.GenericSansSerif;
    }

    private static Font CreateFont(FontFamily family, float size, bool bold)
    {
        var style = bold && family.IsStyleAvailable(FontStyle.Bold) ? FontStyle.Bold : FontStyle.Regular;
        return new(family, size, style, GraphicsUnit.Pixel);
    }

    private static Font FitFont(Graphics graphics, FontFamily family, string text, int initialSize,
        int minimumSize, float width, StringFormat format)
    {
        for (var size = initialSize; size > minimumSize; size--)
        {
            var font = CreateFont(family, size, bold: true);
            if (graphics.MeasureString(text, font, new SizeF(32768, 256), format).Width <= width) return font;
            font.Dispose();
        }
        return CreateFont(family, minimumSize, bold: true);
    }

    private static StringFormat SingleLineFormat(StringAlignment alignment) => new(StringFormat.GenericTypographic)
    {
        Alignment = alignment, LineAlignment = StringAlignment.Near,
        FormatFlags = StringFormatFlags.NoWrap,
        Trimming = StringTrimming.EllipsisCharacter,
        HotkeyPrefix = HotkeyPrefix.None
    };

    private static string NormalizeTitle(string title)
    {
        var result = new StringBuilder(128);
        var count = 0;
        foreach (var rune in title.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                if (result.Length > 0 && result[^1] != ' ') result.Append(' ');
                continue;
            }
            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate)
                continue;
            if (++count > 96) break;
            result.Append(Rune.ToUpperInvariant(rune).ToString());
        }
        return result.Length == 0 ? "SORT" : result.ToString().Trim();
    }
}
