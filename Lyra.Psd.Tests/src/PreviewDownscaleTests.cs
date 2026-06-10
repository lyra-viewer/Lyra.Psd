using System.Buffers.Binary;
using Lyra.Psd.Core.Common;
using Lyra.Psd.Core.Decode.Decompressors;
using Lyra.Psd.Core.Decode.Pixel;
using Lyra.Psd.Core.Readers;
using Lyra.Psd.Core.SectionData;

namespace Lyra.Psd.Tests;

/// <summary>
/// Regression tests for the nearest-neighbour column sampling used by preview/scaled decode.
/// </summary>
public class PreviewDownscaleTests
{
    [Fact]
    public void Downscale16BitSamplesCorrectColumns()
    {
        const int srcWidth = 4;
        const int height = 2;

        // Single grayscale plane; each pixel's value equals its column index.
        var plane = new byte[srcWidth * height * 2];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < srcWidth; x++)
                BinaryPrimitives.WriteUInt16BigEndian(plane.AsSpan((y * srcWidth + x) * 2, 2), (ushort)x);

        var header = new FileHeader(1, 1, srcWidth, height, 16, ColorMode.Grayscale);
        var decoded = DecodePreview(header, plane, outWidth: 2, outHeight: height);

        var data = decoded.Planes[0].Data;
        for (var y = 0; y < height; y++)
        {
            var c0 = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan((y * 2 + 0) * 2, 2));
            var c1 = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan((y * 2 + 1) * 2, 2));
            Assert.Equal(0, c0);
            Assert.Equal(2, c1);
        }
    }

    [Fact]
    public void Downscale32BitSamplesCorrectColumns()
    {
        const int srcWidth = 4;
        const int height = 1;

        var plane = new byte[srcWidth * height * 4];
        for (var x = 0; x < srcWidth; x++)
            BinaryPrimitives.WriteSingleBigEndian(plane.AsSpan(x * 4, 4), x);

        var header = new FileHeader(1, 1, srcWidth, height, 32, ColorMode.Grayscale);
        var decoded = DecodePreview(header, plane, outWidth: 2, outHeight: height);

        var data = decoded.Planes[0].Data;
        Assert.Equal(0f, BinaryPrimitives.ReadSingleBigEndian(data.AsSpan(0, 4)));
        Assert.Equal(2f, BinaryPrimitives.ReadSingleBigEndian(data.AsSpan(4, 4)));
    }

    private static PlaneImage DecodePreview(FileHeader header, byte[] rawPayload, int outWidth, int outHeight)
    {
        using var stream = new MemoryStream(rawPayload, writable: false);
        var reader = new PsdBigEndianReader(stream);
        var decompressor = new PsdRawDecompressor();
        return decompressor.DecompressPreview(reader, header, outWidth, outHeight, CancellationToken.None);
    }
}
