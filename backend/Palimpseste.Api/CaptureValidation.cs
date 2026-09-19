using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Palimpseste.Api;

public sealed record CaptureManifest(Guid CaptureId, Guid ParchmentId, string CanonicalJson, string ManifestHash,
    string RequestHash, string DrawingHash, string InkHash, string JournalHash, string PixelHash,
    string ReferenceHash, long UsedInk, string ClosedReason);

public static class CaptureValidation
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly string[] Fields = ["schema_version", "capture_id", "parchment_id", "layout_version", "reference_sha256", "raster_version", "drawing_file_sha256", "drawing_pixel_sha256", "ink_file_sha256", "journal_file_sha256", "width", "height", "used_ink_micro_units", "closed_reason", "locked_regions", "created_at"];

    public static CaptureManifest Validate(JsonElement root, Guid parchmentId, string referenceHash, byte[] referencePng, byte[] drawing, byte[] ink, byte[] journal, long budget, long firstSequence, string firstHash)
    {
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != Fields.Length || Fields.Any(x => !root.TryGetProperty(x, out _))) throw new InvalidDataException("Capture manifest fields");
        if (root.GetProperty("schema_version").GetString() != "sp.capture/1.0" || root.GetProperty("layout_version").GetString() != "three_regions_v1") throw new InvalidDataException("Capture version");
        if (!Guid.TryParseExact(root.GetProperty("capture_id").GetString(), "N", out var captureId) || !Guid.TryParseExact(root.GetProperty("parchment_id").GetString(), "N", out var manifestParchment) || manifestParchment != parchmentId) throw new InvalidDataException("Capture identity");
        var rasterVersion = root.GetProperty("raster_version").GetString();
        if (string.IsNullOrWhiteSpace(rasterVersion) || rasterVersion.Length > 80) throw new InvalidDataException("Raster version");
        var refHash = root.GetProperty("reference_sha256").GetString() ?? "";
        if (refHash != referenceHash || ApiJson.Sha256(referencePng) != referenceHash) throw new InvalidDataException("Reference hash");
        if (root.GetProperty("width").GetInt32() != 1024 || root.GetProperty("height").GetInt32() != 1024) throw new InvalidDataException("Capture dimensions");
        var usedInk = root.GetProperty("used_ink_micro_units").GetInt64();
        if (usedInk is < 1 or > 1_000_000_000 || usedInk > budget) throw new InvalidDataException("Ink budget");
        var reason = root.GetProperty("closed_reason").GetString() ?? "";
        if (reason is not ("all_regions_locked" or "window_closed" or "ink_exhausted" or "crash_recovered")) throw new InvalidDataException("Closed reason");
        var regions = root.GetProperty("locked_regions");
        if (regions.ValueKind != JsonValueKind.Array || regions.GetArrayLength() > 3) throw new InvalidDataException("Locked regions");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var region in regions.EnumerateArray())
        {
            var name = region.GetString() ?? "";
            if (name is not ("core" or "ring" or "outer") || !seen.Add(name)) throw new InvalidDataException("Locked regions");
        }
        if (!DateTimeOffset.TryParse(root.GetProperty("created_at").GetString(), out _)) throw new InvalidDataException("Capture timestamp");
        var drawingHash = ApiJson.Sha256(drawing);
        var inkHash = ApiJson.Sha256(ink);
        var journalHash = ApiJson.Sha256(journal);
        if (drawingHash != root.GetProperty("drawing_file_sha256").GetString() || inkHash != root.GetProperty("ink_file_sha256").GetString() || journalHash != root.GetProperty("journal_file_sha256").GetString()) throw new InvalidDataException("Capture file hash");
        var drawingPixels = PngPixels(drawing, requireOpaque: true);
        var inkPixels = PngPixels(ink, requireOpaque: false);
        var referencePixels = PngPixels(referencePng, requireOpaque: true, allowRgb: true);
        ValidateComposite(drawingPixels, inkPixels, referencePixels);
        var pixelHash = ApiJson.Sha256(drawingPixels);
        if (pixelHash != root.GetProperty("drawing_pixel_sha256").GetString()) throw new InvalidDataException("Capture pixel hash");
        ValidateJournal(journal, firstSequence, firstHash);
        var canonical = ApiJson.Canonicalize(root);
        var manifestHash = ApiJson.Sha256(Encoding.UTF8.GetBytes(canonical));
        var requestHash = ApiJson.Sha256(Encoding.UTF8.GetBytes($"{manifestHash}|{drawingHash}|{inkHash}|{journalHash}"));
        return new CaptureManifest(captureId, parchmentId, canonical, manifestHash, requestHash, drawingHash, inkHash, journalHash, pixelHash, refHash, usedInk, reason);
    }

    public static string PngPixelsHash(ReadOnlySpan<byte> bytes, bool requireOpaque) => ApiJson.Sha256(PngPixels(bytes, requireOpaque));

    private static void ValidateComposite(byte[] drawing, byte[] ink, byte[] reference)
    {
        for (var y = 0; y < 1024; y++)
        for (var x = 0; x < 1024; x++)
        {
            var index = (y * 1024 + x) * 4;
            var alpha = ink[index + 3];
            if (alpha != 0)
            {
                var u = (x + 0.5f) / 1024f;
                var v = (y + 0.5f) / 1024f;
                var dx = 2f * u - 1f;
                var dy = 2f * v - 1f;
                if (dx * dx + dy * dy > 1f) throw new InvalidDataException("Ink outside parchment layout");
            }
            var a = alpha / 255f;
            for (var channel = 0; channel < 3; channel++)
            {
                var expected = (int)MathF.Round(ink[index + channel] * a + reference[index + channel] * (1f - a), MidpointRounding.ToEven);
                if (Math.Abs(drawing[index + channel] - expected) > 1)
                    throw new InvalidDataException("Drawing/ink/reference composite mismatch");
            }
        }
    }

    public static byte[] PngPixels(ReadOnlySpan<byte> bytes, bool requireOpaque, bool allowRgb = false)
    {
        if (bytes.Length > 8 * 1024 * 1024 || bytes.Length < 50 || !bytes[..8].SequenceEqual(Signature)) throw new InvalidDataException("PNG signature/size");
        var offset = 8;
        var sawHeader = false;
        var sawEnd = false;
        var channels = 4;
        using var idat = new MemoryStream();
        while (offset + 12 <= bytes.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
            if (length > bytes.Length - offset - 12) throw new InvalidDataException("PNG chunk length");
            var type = bytes.Slice(offset + 4, 4);
            var chunk = bytes.Slice(offset + 8, checked((int)length));
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset + 8 + (int)length, 4));
            if (Crc32(bytes.Slice(offset + 4, 4 + (int)length)) != expectedCrc) throw new InvalidDataException("PNG CRC");
            if (type.SequenceEqual("IHDR"u8))
            {
                if (sawHeader || offset != 8 || length != 13 || BinaryPrimitives.ReadUInt32BigEndian(chunk[..4]) != 1024 || BinaryPrimitives.ReadUInt32BigEndian(chunk.Slice(4, 4)) != 1024 || chunk[8] != 8 || (chunk[9] != 6 && !(allowRgb && chunk[9] == 2)) || chunk[10] != 0 || chunk[11] != 0 || chunk[12] != 0) throw new InvalidDataException("PNG RGB/RGBA8 1024x1024 required");
                channels = chunk[9] == 2 ? 3 : 4;
                sawHeader = true;
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                if (!sawHeader || sawEnd) throw new InvalidDataException("PNG chunk order");
                idat.Write(chunk);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                if (length != 0 || !sawHeader) throw new InvalidDataException("PNG end");
                sawEnd = true;
                offset += 12;
                break;
            }
            else if ((type[0] & 0x20) == 0) throw new InvalidDataException("Unknown critical PNG chunk");
            offset += checked(12 + (int)length);
        }
        if (!sawEnd || offset != bytes.Length || idat.Length == 0) throw new InvalidDataException("PNG truncated/trailing data");
        idat.Position = 0;
        using var inflater = new ZLibStream(idat, CompressionMode.Decompress);
        var pixels = new byte[1024 * 1024 * 4];
        var stride = 1024 * channels;
        var previous = new byte[stride];
        var current = new byte[stride];
        var row = new byte[stride + 1];
        for (var y = 0; y < 1024; y++)
        {
            inflater.ReadExactly(row);
            var filter = row[0];
            if (filter > 4) throw new InvalidDataException("PNG filter");
            for (var x = 0; x < stride; x++)
            {
                var left = x >= channels ? current[x - channels] : 0;
                var up = previous[x];
                var upperLeft = x >= channels ? previous[x - channels] : 0;
                var predictor = filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    _ => Paeth(left, up, upperLeft)
                };
                current[x] = unchecked((byte)(row[x + 1] + predictor));
            }
            var target = y * 1024 * 4;
            if (channels == 4)
            {
                if (requireOpaque)
                    for (var x = 3; x < stride; x += 4) if (current[x] != 255) throw new InvalidDataException("Drawing PNG must be opaque");
                Buffer.BlockCopy(current, 0, pixels, target, stride);
            }
            else
            {
                for (var x = 0; x < 1024; x++)
                {
                    pixels[target + x * 4] = current[x * 3];
                    pixels[target + x * 4 + 1] = current[x * 3 + 1];
                    pixels[target + x * 4 + 2] = current[x * 3 + 2];
                    pixels[target + x * 4 + 3] = 255;
                }
            }
            (previous, current) = (current, previous);
        }
        if (inflater.ReadByte() != -1) throw new InvalidDataException("PNG excess decompressed data");
        return pixels;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a); var pb = Math.Abs(p - b); var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (var item in bytes)
        {
            crc ^= item;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
        }
        return ~crc;
    }

    private static void ValidateJournal(byte[] bytes, long firstSequence, string firstHash)
    {
        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = gzip.Read(buffer);
            if (read == 0) break;
            if (output.Length + read > 8 * 1024 * 1024) throw new InvalidDataException("Journal too large");
            output.Write(buffer, 0, read);
        }
        var lines = Encoding.UTF8.GetString(output.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0 || lines.Length > 65540) throw new InvalidDataException("Journal event count");
        var previous = new string('0', 64);
        long expectedSequence = 1;
        var points = 0;
        foreach (var line in lines)
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var sequence = root.GetProperty("sequence").GetInt64();
            var op = root.GetProperty("op").GetString() ?? "";
            if (sequence != expectedSequence++ || op is not ("down" or "move" or "up" or "close")) throw new InvalidDataException("Journal sequence/op");
            if (op != "close" && ++points > 65536) throw new InvalidDataException("Journal points");
            var x = root.GetProperty("x").GetInt32(); var y = root.GetProperty("y").GetInt32();
            var brush = root.GetProperty("brush").GetInt32();
            var r = root.GetProperty("r").GetInt32(); var g = root.GetProperty("g").GetInt32(); var b = root.GetProperty("b").GetInt32(); var a = root.GetProperty("a").GetInt32();
            var diameter = root.GetProperty("diameter_milli").GetInt32(); var pressure = root.GetProperty("pressure_milli").GetInt32();
            if (x is < 0 or > 65535 || y is < 0 or > 65535 || brush is < 0 or > 2 || new[] { r, g, b, a }.Any(c => c is < 0 or > 255) || diameter is < 0 or > 1_000_000 || pressure is < 0 or > 1000) throw new InvalidDataException("Journal coordinate/style");
            if (root.GetProperty("previous_sha256").GetString() != previous) throw new InvalidDataException("Journal hash chain");
            var content = $"{sequence}|{op}|{x}|{y}|{brush}|{r}|{g}|{b}|{a}|{diameter}|{pressure}|{previous}";
            var hash = ApiJson.Sha256(Encoding.UTF8.GetBytes(content));
            if (root.GetProperty("sha256").GetString() != hash) throw new InvalidDataException("Journal event hash");
            if (sequence == firstSequence && hash != firstHash) throw new InvalidDataException("First block mismatch");
            previous = hash;
        }
        if (firstSequence < 1 || firstSequence >= expectedSequence || !lines.Any(x => x.Contains("\"op\":\"down\"", StringComparison.Ordinal))) throw new InvalidDataException("Journal missing first ink");
    }
}
