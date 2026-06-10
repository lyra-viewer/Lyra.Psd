using System.Buffers.Binary;
using System.IO.Compression;
using Lyra.Psd.Core.Common;
using Lyra.Psd.Core.Decode.Decompressors;
using Lyra.Psd.Core.Decode.PlaneRowConsumer;
using Lyra.Psd.Core.Readers;
using Lyra.Psd.Core.SectionData;

namespace Lyra.Psd.Tests;

/// <summary>
/// Pins the 32-bit ZIP-with-prediction decoder to the Adobe/TIFF floating-point predictor (predictor 3),
/// using a hand-computed payload so the test does not depend on the test encoder.
///
/// Source row: two floats [1.0f, 2.0f], one plane, width 2.
///   big-endian interleaved: 3F 80 00 00 | 40 00 00 00
///   shuffle to planar bytes: 3F 40 | 80 00 | 00 00 | 00 00
///   byte-difference (encode):
///     3F, (40-3F)=01, (80-40)=40, (00-80)=80, 00, 00, 00, 00 => 3F 01 40 80 00 00 00 00
/// The decoder must accumulate the byte deltas, then de-shuffle back to the two original floats.
/// </summary>
public class Predictor32SpecTests
{
    [Fact]
    public void DecodesFloatingPointPredictorRow()
    {
        byte[] encodedRow = [0x3F, 0x01, 0x40, 0x80, 0x00, 0x00, 0x00, 0x00];
        var payload = Zlib(encodedRow);

        var header = new FileHeader(1, 1, Width: 2, Height: 1, Depth: 32, ColorMode.Grayscale);

        using var stream = new MemoryStream(payload, writable: false);
        var reader = new PsdBigEndianReader(stream);

        var image = PsdDecompressorBase.AllocatePlaneImage(header);
        var sink = new PlaneImageRowSink(image);

        new PsdZipPredictDecompressor()
            .DecompressPlanesRowRegion(reader, header, 0, 1, sink, CancellationToken.None);

        var data = sink.Image.Planes[0].Data;
        Assert.Equal(1.0f, BinaryPrimitives.ReadSingleBigEndian(data.AsSpan(0, 4)));
        Assert.Equal(2.0f, BinaryPrimitives.ReadSingleBigEndian(data.AsSpan(4, 4)));
    }

    private static byte[] Zlib(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(data);
        return ms.ToArray();
    }
}