using System.Buffers.Binary;
using System.IO.Compression;

namespace Lyra.Psd.Tests;

/// <summary>
/// Reference *encoders* for the four PSD composite compression schemes.
///
/// Real Photoshop only ever writes the merged/composite image-data section as RAW or RLE;
/// ZIP and ZIP-with-prediction appear only in per-layer channel data (which this decoder
/// does not decode pixels for). These encoders synthesize byte-exact payloads so the
/// decompressors can be round-trip verified.
/// </summary>
internal static class CompressionTestCodecs
{
    public static byte[] EncodeRaw(byte[][] planes) => Concat(planes);

    public static byte[] EncodeRle(byte[][] planes, int width, int height, int bpc, bool psb)
    {
        var rowSize = width * bpc;
        var packed = new List<byte[]>(planes.Length * height);

        foreach (var plane in planes)
            for (var y = 0; y < height; y++)
                packed.Add(PackBitsEncode(plane.AsSpan(y * rowSize, rowSize)));

        using var ms = new MemoryStream();

        // Row-byte-count table: 2 bytes/row (PSD) or 4 bytes/row (PSB).
        Span<byte> lenBuf = stackalloc byte[4];
        foreach (var row in packed)
        {
            if (psb)
            {
                BinaryPrimitives.WriteInt32BigEndian(lenBuf, row.Length);
                ms.Write(lenBuf[..4]);
            }
            else
            {
                BinaryPrimitives.WriteUInt16BigEndian(lenBuf, (ushort)row.Length);
                ms.Write(lenBuf[..2]);
            }
        }

        foreach (var row in packed)
            ms.Write(row);

        return ms.ToArray();
    }

    public static byte[] EncodeZip(byte[][] planes) => Zlib(Concat(planes));

    public static byte[] EncodeZipPredict(byte[][] planes, int width, int height, int bpc)
    {
        var rowSize = width * bpc;
        var predicted = new byte[planes.Length][];

        for (var p = 0; p < planes.Length; p++)
        {
            var copy = (byte[])planes[p].Clone();
            for (var y = 0; y < height; y++)
                ForwardPredict(copy.AsSpan(y * rowSize, rowSize), width, bpc);

            predicted[p] = copy;
        }

        return Zlib(Concat(predicted));
    }

    // --- predictors (forward / encode side) ---------------------------------

    private static void ForwardPredict(Span<byte> row, int width, int bpc)
    {
        switch (bpc)
        {
            case 1:
                for (var i = row.Length - 1; i >= 1; i--)
                    row[i] = unchecked((byte)(row[i] - row[i - 1]));
                break;

            case 2:
                for (var x = width - 1; x >= 1; x--)
                {
                    var cur = BinaryPrimitives.ReadUInt16BigEndian(row.Slice(x * 2, 2));
                    var prev = BinaryPrimitives.ReadUInt16BigEndian(row.Slice((x - 1) * 2, 2));
                    BinaryPrimitives.WriteUInt16BigEndian(row.Slice(x * 2, 2), unchecked((ushort)(cur - prev)));
                }

                break;

            case 4:
                // Inverse of decode: shuffle interleaved BE floats -> planar bytes, then byte-difference.
                var needed = width * 4;
                var tmp = new byte[needed];
                for (var x = 0; x < width; x++)
                for (var b = 0; b < 4; b++)
                    tmp[b * width + x] = row[x * 4 + b];

                for (var i = needed - 1; i >= 1; i--)
                    tmp[i] = unchecked((byte)(tmp[i] - tmp[i - 1]));

                tmp.CopyTo(row);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(bpc), bpc, "Expected 1, 2, or 4.");
        }
    }

    // --- low-level helpers --------------------------------------------------

    private static byte[] PackBitsEncode(ReadOnlySpan<byte> src)
    {
        // Valid (if unoptimized) PackBits: emit literal runs of up to 128 bytes.
        var outp = new List<byte>(src.Length + src.Length / 128 + 1);
        var i = 0;
        while (i < src.Length)
        {
            var run = Math.Min(128, src.Length - i);
            outp.Add((byte)(run - 1));
            for (var j = 0; j < run; j++)
                outp.Add(src[i + j]);
            i += run;
        }

        return outp.ToArray();
    }

    private static byte[] Zlib(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(data);

        return ms.ToArray();
    }

    private static byte[] Concat(byte[][] parts)
    {
        var total = 0;
        foreach (var p in parts)
            total += p.Length;

        var result = new byte[total];
        var offset = 0;
        foreach (var p in parts)
        {
            p.CopyTo(result, offset);
            offset += p.Length;
        }

        return result;
    }
}