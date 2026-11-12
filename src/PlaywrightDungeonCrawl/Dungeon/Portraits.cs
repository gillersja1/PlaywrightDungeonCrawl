using System.IO.Compression;

namespace PlaywrightDungeonCrawl.Dungeon;

/// <summary>
/// Paints floor 17's portraits as PNGs on the server: a simple bust on a two-tone background, drawn from a seed, so
/// the same seed always gives the same bytes. A forgery is the same painting with a small patch nudged off color:
/// invisible to the eye, obvious to a byte comparison.
/// </summary>
public static class Portraits
{
    public const int Width = 120;
    public const int Height = 150;

    /// <summary>A small square nudged off color, at (X, Y).</summary>
    public sealed record Flaw(int X, int Y, int Size = 4, int Shift = 9);

    public static byte[] Paint(int seed, Flaw? flaw = null)
    {
        var rng = new Random(seed);
        var (sky, ground, skin, coat) = (Color(rng), Color(rng), Color(rng, 150), Color(rng));
        var horizon = 60 + rng.Next(50);
        var headX = 50 + rng.Next(20);
        var headY = 45 + rng.Next(15);
        var headR = 18 + rng.Next(8);
        var pixels = new byte[Width * Height * 3];

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var c = y < horizon ? sky : ground;
                var dx = x - headX;
                var dy = y - headY;
                if (dx * dx + dy * dy <= headR * headR)
                {
                    c = skin;
                }
                else if (y > headY + headR - 2 && Math.Abs(dx) < headR + (y - headY - headR) * 1.2)
                {
                    c = coat;
                }
                if (flaw is { } f && x >= f.X && x < f.X + f.Size && y >= f.Y && y < f.Y + f.Size)
                {
                    c = (Nudge(c.R, f.Shift), Nudge(c.G, f.Shift), Nudge(c.B, f.Shift));
                }
                var i = (y * Width + x) * 3;
                (pixels[i], pixels[i + 1], pixels[i + 2]) = c;
            }
        }
        return Png(pixels);
    }

    private static (byte R, byte G, byte B) Color(Random rng, int min = 30) =>
        ((byte)rng.Next(min, 226), (byte)rng.Next(min, 226), (byte)rng.Next(min, 226));

    private static byte Nudge(byte value, int shift) => (byte)(value > 127 ? value - shift : value + shift);

    /// <summary>An 8-bit RGB PNG, no interlacing: the signature, IHDR, one IDAT and IEND.</summary>
    private static byte[] Png(byte[] rgb)
    {
        var raw = new byte[Height * (Width * 3 + 1)];
        for (var y = 0; y < Height; y++)
        {
            // Each row starts with filter type 0 (none).
            Buffer.BlockCopy(rgb, y * Width * 3, raw, y * (Width * 3 + 1) + 1, Width * 3);
        }
        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(raw);
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        WriteBigEndian(header, 0, Width);
        WriteBigEndian(header, 4, Height);
        header[8] = 8; // bit depth
        header[9] = 2; // color type: RGB
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", compressed.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream png, string type, byte[] data)
    {
        var length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        png.Write(length);
        var body = new byte[4 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(type, body);
        data.CopyTo(body, 4);
        png.Write(body);
        var crc = new byte[4];
        WriteBigEndian(crc, 0, (int)Crc32(body));
        png.Write(crc);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++)
        {
            c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        }
        return c;
    }).ToArray();

    private static uint Crc32(byte[] bytes)
    {
        var c = 0xFFFFFFFF;
        foreach (var b in bytes)
        {
            c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        }
        return c ^ 0xFFFFFFFF;
    }
}
