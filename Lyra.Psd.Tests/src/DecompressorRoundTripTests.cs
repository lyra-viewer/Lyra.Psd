using Lyra.Psd.Core.Common;
using Lyra.Psd.Core.Decode.Decompressors;
using Lyra.Psd.Core.Decode.Pixel;
using Lyra.Psd.Core.Decode.PlaneRowConsumer;
using Lyra.Psd.Core.Readers;
using Lyra.Psd.Core.SectionData;

namespace Lyra.Psd.Tests;

/// <summary>
/// Byte-exact round-trip coverage for every composite decompressor at 8/16/32-bit depth.
/// These are the only tests that exercise ZIP / ZIP-with-prediction, which cannot be
/// produced by Photoshop for the composite section.
/// </summary>
public class DecompressorRoundTripTests
{
    private const int Width = 5; // intentionally odd
    private const int Height = 3;
    private const int Channels = 3; // RGB -> R,G,B planes

    public static IEnumerable<object[]> Cases()
    {
        foreach (var depthBits in new[] { 8, 16, 32 })
        foreach (var compression in new[] { CompressionType.Raw, CompressionType.Rle, CompressionType.Zip, CompressionType.ZipPredict })
        {
            // PSB only changes the RLE row-table width and a few length fields; cover both for RLE.
            yield return [compression, depthBits, false];
            if (compression == CompressionType.Rle)
                yield return [compression, depthBits, true];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void RoundTrips(CompressionType compression, int depthBits, bool psb)
    {
        var bpc = depthBits / 8;
        var planes = BuildPlanes(Channels, Width, Height, bpc, seed: 1234 + depthBits + (int)compression);

        var payload = compression switch
        {
            CompressionType.Raw => CompressionTestCodecs.EncodeRaw(planes),
            CompressionType.Rle => CompressionTestCodecs.EncodeRle(planes, Width, Height, bpc, psb),
            CompressionType.Zip => CompressionTestCodecs.EncodeZip(planes),
            CompressionType.ZipPredict => CompressionTestCodecs.EncodeZipPredict(planes, Width, Height, bpc),
            _ => throw new ArgumentOutOfRangeException(nameof(compression))
        };

        var header = new FileHeader(
            Version: (ushort)(psb ? 2 : 1),
            NumberOfChannels: Channels,
            Width: Width,
            Height: Height,
            Depth: (ushort)depthBits,
            ColorMode: ColorMode.Rgb);

        var decoded = Decode(compression, header, payload);

        Assert.Equal(Channels, decoded.Planes.Count);
        for (var p = 0; p < Channels; p++)
            Assert.Equal(planes[p], decoded.Planes[p].Data);
    }

    private static PlaneImage Decode(CompressionType compression, FileHeader header, byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        var reader = new PsdBigEndianReader(stream);

        var image = PsdDecompressorBase.AllocatePlaneImage(header);
        var sink = new PlaneImageRowSink(image);

        IPsdDecompressor decompressor = compression switch
        {
            CompressionType.Raw => new PsdRawDecompressor(),
            CompressionType.Rle => new PsdRleDecompressor(),
            CompressionType.Zip => new PsdZipDecompressor(),
            CompressionType.ZipPredict => new PsdZipPredictDecompressor(),
            _ => throw new ArgumentOutOfRangeException(nameof(compression))
        };

        decompressor.DecompressPlanesRowRegion(reader, header, 0, header.Height, sink, CancellationToken.None);
        return sink.Image;
    }

    private static byte[][] BuildPlanes(int channels, int width, int height, int bpc, int seed)
    {
        var rng = new Random(seed);
        var planeBytes = width * height * bpc;
        var planes = new byte[channels][];
        for (var p = 0; p < channels; p++)
        {
            var buf = new byte[planeBytes];
            rng.NextBytes(buf);
            planes[p] = buf;
        }

        return planes;
    }
}