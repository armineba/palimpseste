using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Palimpseste.Core
{
    // Canonical 8-bit, non-interlaced PNG codec for the controlled 1024² capture format.
    public static class PngCodec
    {
        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        private static readonly uint[] CrcTable = MakeCrcTable();
        public sealed class Image
        {
            public int Width { get; internal set; }
            public int Height { get; internal set; }
            public byte[] Rgba { get; internal set; }
        }

        public static Image DecodeRgba(byte[] png, int expectedWidth = 1024, int expectedHeight = 1024)
        {
            if (png == null || png.Length < 45 || png.Length > 8 * 1024 * 1024 ||
                !png.Take(8).SequenceEqual(Signature)) throw new InvalidDataException("Invalid PNG signature or size");
            int at = 8, width = 0, height = 0, bpp = 0;
            bool ihdr = false, ended = false;
            using (var idat = new MemoryStream())
            {
                while (at + 12 <= png.Length)
                {
                    int length = ReadInt(png, at); at += 4;
                    if (length < 0 || length > 8 * 1024 * 1024 || at + 8L + length > png.Length)
                        throw new InvalidDataException("PNG chunk exceeds input");
                    int typeAt = at; string type = System.Text.Encoding.ASCII.GetString(png, at, 4); at += 4;
                    uint expectedCrc = ReadUInt(png, at + length);
                    if (Crc(png, typeAt, length + 4) != expectedCrc)
                        throw new InvalidDataException("PNG chunk CRC mismatch");
                    if (!ihdr && type != "IHDR") throw new InvalidDataException("IHDR must be first");
                    if (type == "IHDR")
                    {
                        if (ihdr || length != 13) throw new InvalidDataException("Invalid IHDR");
                        width = ReadInt(png, at); height = ReadInt(png, at + 4);
                        if (width != expectedWidth || height != expectedHeight || width <= 0 || height <= 0 ||
                            png[at + 8] != 8 || png[at + 9] != 6 || png[at + 10] != 0 ||
                            png[at + 11] != 0 || png[at + 12] != 0)
                            throw new InvalidDataException("Only 8-bit RGBA non-interlaced PNG with expected dimensions is accepted");
                        bpp = 4; ihdr = true;
                    }
                    else if (type == "IDAT") idat.Write(png, at, length);
                    else if (type == "IEND") { if (length != 0) throw new InvalidDataException("Invalid IEND"); ended = true; at += length + 4; break; }
                    else if (char.IsUpper(type[0])) throw new InvalidDataException("Unsupported critical PNG chunk: " + type);
                    at += length + 4;
                }
                if (!ended || at != png.Length || idat.Length < 6) throw new InvalidDataException("Incomplete PNG");
                var compressed = idat.ToArray();
                if ((compressed[0] & 15) != 8 || ((compressed[0] << 8) | compressed[1]) % 31 != 0 ||
                    (compressed[1] & 32) != 0) throw new InvalidDataException("Unsupported zlib header");
                int rowBytes = checked(width * bpp);
                int rawLength = checked((rowBytes + 1) * height);
                var filtered = new byte[rawLength];
                using (var source = new MemoryStream(compressed, 2, compressed.Length - 6, false))
                using (var deflate = new DeflateStream(source, CompressionMode.Decompress))
                {
                    int read = 0, n;
                    while (read < rawLength && (n = deflate.Read(filtered, read, rawLength - read)) > 0) read += n;
                    if (read != rawLength || deflate.ReadByte() != -1)
                        throw new InvalidDataException("Unexpected decompressed PNG size");
                }
                if (Adler32(filtered) != ReadUInt(compressed, compressed.Length - 4))
                    throw new InvalidDataException("PNG zlib checksum mismatch");
                var rgba = new byte[checked(width * height * 4)];
                for (int y = 0; y < height; y++)
                {
                    int src = y * (rowBytes + 1), dst = y * rowBytes;
                    int filter = filtered[src++];
                    if (filter > 4) throw new InvalidDataException("Unknown PNG row filter");
                    for (int x = 0; x < rowBytes; x++)
                    {
                        int left = x >= bpp ? rgba[dst + x - bpp] : 0;
                        int up = y > 0 ? rgba[dst + x - rowBytes] : 0;
                        int upperLeft = y > 0 && x >= bpp ? rgba[dst + x - rowBytes - bpp] : 0;
                        int predictor = filter == 1 ? left : filter == 2 ? up : filter == 3 ? (left + up) / 2 :
                            filter == 4 ? Paeth(left, up, upperLeft) : 0;
                        rgba[dst + x] = unchecked((byte)(filtered[src + x] + predictor));
                    }
                }
                return new Image { Width = width, Height = height, Rgba = rgba };
            }
        }

        public static byte[] EncodeRgba(byte[] rgba, int width, int height)
        {
            if (width <= 0 || height <= 0 || width > 1024 || height > 1024 ||
                rgba == null || rgba.Length != checked(width * height * 4))
                throw new ArgumentException("Invalid RGBA dimensions");
            int rowBytes = width * 4;
            var filtered = new byte[(rowBytes + 1) * height];
            for (int y = 0; y < height; y++)
                Buffer.BlockCopy(rgba, y * rowBytes, filtered, y * (rowBytes + 1) + 1, rowBytes);
            using (var zlib = new MemoryStream())
            {
                zlib.WriteByte(0x78); zlib.WriteByte(0x01);
                int offset = 0;
                while (offset < filtered.Length)
                {
                    int count = Math.Min(65535, filtered.Length - offset);
                    zlib.WriteByte(offset + count == filtered.Length ? (byte)1 : (byte)0);
                    zlib.WriteByte((byte)count); zlib.WriteByte((byte)(count >> 8));
                    int inverse = ~count;
                    zlib.WriteByte((byte)inverse); zlib.WriteByte((byte)(inverse >> 8));
                    zlib.Write(filtered, offset, count);
                    offset += count;
                }
                WriteUInt(zlib, Adler32(filtered));
                using (var output = new MemoryStream())
                {
                    output.Write(Signature, 0, Signature.Length);
                    var header = new byte[13];
                    SetInt(header, 0, width); SetInt(header, 4, height);
                    header[8] = 8; header[9] = 6;
                    WriteChunk(output, "IHDR", header);
                    WriteChunk(output, "IDAT", zlib.ToArray());
                    WriteChunk(output, "IEND", Array.Empty<byte>());
                    return output.ToArray();
                }
            }
        }

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c, da = Math.Abs(p - a), db = Math.Abs(p - b), dc = Math.Abs(p - c);
            return da <= db && da <= dc ? a : db <= dc ? b : c;
        }
        private static int ReadInt(byte[] data, int at) => checked((int)ReadUInt(data, at));
        private static uint ReadUInt(byte[] data, int at) => (uint)data[at] << 24 | (uint)data[at + 1] << 16 |
            (uint)data[at + 2] << 8 | data[at + 3];
        private static void SetInt(byte[] data, int at, int value)
        {
            data[at] = (byte)(value >> 24); data[at + 1] = (byte)(value >> 16);
            data[at + 2] = (byte)(value >> 8); data[at + 3] = (byte)value;
        }
        private static void WriteUInt(Stream output, uint value)
        {
            output.WriteByte((byte)(value >> 24)); output.WriteByte((byte)(value >> 16));
            output.WriteByte((byte)(value >> 8)); output.WriteByte((byte)value);
        }
        private static void WriteChunk(Stream output, string type, byte[] data)
        {
            WriteUInt(output, (uint)data.Length);
            var name = System.Text.Encoding.ASCII.GetBytes(type);
            output.Write(name, 0, name.Length); output.Write(data, 0, data.Length);
            var bytes = new byte[name.Length + data.Length];
            Buffer.BlockCopy(name, 0, bytes, 0, 4);
            Buffer.BlockCopy(data, 0, bytes, 4, data.Length);
            WriteUInt(output, Crc(bytes, 0, bytes.Length));
        }
        private static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            foreach (var item in data) { a = (a + item) % 65521; b = (b + a) % 65521; }
            return b << 16 | a;
        }
        private static uint Crc(byte[] data, int at, int length)
        {
            uint crc = 0xffffffff;
            for (int i = at; i < at + length; i++) crc = CrcTable[(crc ^ data[i]) & 255] ^ (crc >> 8);
            return crc ^ 0xffffffff;
        }
        private static uint[] MakeCrcTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++) {
                uint value = i;
                for (int k = 0; k < 8; k++) value = (value & 1) != 0 ? 0xedb88320 ^ (value >> 1) : value >> 1;
                table[i] = value;
            }
            return table;
        }
    }
}
