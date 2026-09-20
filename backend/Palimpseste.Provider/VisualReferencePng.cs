using System.Buffers.Binary;
using System.IO.Compression;

namespace Palimpseste.Provider;

/// <summary>Server-created, hash-bound input. A client/model never supplies this filesystem path.</summary>
public sealed record SpellVisualReference(string PngPath, string Sha256, string? AnimationSheetJson = null);

public sealed record GeneratedImageArtifact(string SavedPath, string Sha256, int Width, int Height, string ToolCallId);

public sealed record ProviderVisualReference(CodexResult Transport, byte[]? PngBytes, string? Sha256,
    string? SavedPath, int? Width, int? Height, string? AnimationSheetJson = null);

public interface IVisualReferenceGenerator
{
    Task<ProviderVisualReference> GenerateVisualReferenceAsync(string jobId, string attemptId,
        byte[] frozenDescriptionUtf8, CancellationToken ct);
}

/// <summary>Complete bounded validation before a generated PNG becomes a trusted spell artifact.</summary>
public static class VisualReferencePng
{
    public const int MaxBytes = 8 * 1024 * 1024;
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static (int Width, int Height) Validate(ReadOnlySpan<byte> bytes) => ValidateCore(bytes, animationStrip: false);

    /// <summary>Only for fixed Unity D16 output, never for model-generated image artifacts.</summary>
    public static (int Width, int Height) ValidateAnimationStrip(ReadOnlySpan<byte> bytes) => ValidateCore(bytes, animationStrip: true);

    private static (int Width, int Height) ValidateCore(ReadOnlySpan<byte> bytes, bool animationStrip)
    {
        if (bytes.Length is < 50 or > MaxBytes || !bytes[..8].SequenceEqual(Signature))
            throw new InvalidDataException("visual_reference_png_signature_or_size");
        var offset = 8;
        var header = false; var ended = false; var dataEnded = false;
        var width = 0; var height = 0; var channels = 0;
        using var compressed = new MemoryStream();
        while (offset + 12 <= bytes.Length)
        {
            var size = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
            if (size > bytes.Length - offset - 12) throw new InvalidDataException("visual_reference_png_chunk_length");
            var length = checked((int)size);
            var type = bytes.Slice(offset + 4, 4);
            foreach (var value in type)
                if (value is not (>= (byte)'A' and <= (byte)'Z') and not (>= (byte)'a' and <= (byte)'z'))
                    throw new InvalidDataException("visual_reference_png_chunk_type");
            if ((type[2] & 0x20) != 0) throw new InvalidDataException("visual_reference_png_reserved_bit");
            var data = bytes.Slice(offset + 8, length);
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset + 8 + length, 4));
            if (Crc32(bytes.Slice(offset + 4, 4 + length)) != expectedCrc)
                throw new InvalidDataException("visual_reference_png_crc");
            if (type.SequenceEqual("IHDR"u8))
            {
                if (header || offset != 8 || length != 13) throw new InvalidDataException("visual_reference_png_header");
                var w = BinaryPrimitives.ReadUInt32BigEndian(data[..4]);
                var h = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4));
                var invalidDimensions = animationStrip ? w != 2048 || h != 320 : w is < 512 or > 2048 || h is < 512 or > 2048;
                if (invalidDimensions || data[8] != 8 ||
                    data[9] is not (2 or 6) || data[10] != 0 || data[11] != 0 || data[12] != 0)
                    throw new InvalidDataException(animationStrip ? "visual_capture_strip_requires_rgb_rgba8_2048x320" :
                        "visual_reference_png_requires_rgb_rgba8_noninterlaced_512_to_2048");
                width = (int)w; height = (int)h; channels = data[9] == 2 ? 3 : 4; header = true;
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                if (!header || dataEnded || ended) throw new InvalidDataException("visual_reference_png_chunk_order");
                compressed.Write(data);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                if (!header || length != 0 || compressed.Length == 0) throw new InvalidDataException("visual_reference_png_end");
                ended = true; offset += 12; break;
            }
            else
            {
                if (!header || (type[0] & 0x20) == 0 || type.SequenceEqual("acTL"u8) ||
                    type.SequenceEqual("fcTL"u8) || type.SequenceEqual("fdAT"u8))
                    throw new InvalidDataException("visual_reference_png_unsupported_chunk");
                if (compressed.Length != 0) dataEnded = true;
            }
            offset += length + 12;
        }
        if (!ended || offset != bytes.Length) throw new InvalidDataException("visual_reference_png_truncated_or_trailing");
        compressed.Position = 0;
        using var inflater = new ZLibStream(compressed, CompressionMode.Decompress);
        var row = new byte[checked(width * channels + 1)];
        for (var y = 0; y < height; y++)
        {
            inflater.ReadExactly(row);
            if (row[0] > 4) throw new InvalidDataException("visual_reference_png_filter");
        }
        if (inflater.ReadByte() != -1) throw new InvalidDataException("visual_reference_png_decompressed_overflow");
        return (width, height);
    }

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (var value in bytes) crc = CrcTable[(crc ^ value) & 255] ^ (crc >> 8);
        return ~crc;
    }
    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++) value = (value >> 1) ^ ((value & 1) != 0 ? 0xedb88320u : 0u);
            table[i] = value;
        }
        return table;
    }
}
