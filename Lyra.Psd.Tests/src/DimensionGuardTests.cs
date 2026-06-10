using Lyra.Psd.Core.Common;
using Lyra.Psd.Core.Decode.Decompressors;
using Lyra.Psd.Core.Decode.PlaneRowConsumer;
using Lyra.Psd.Core.Readers;
using Lyra.Psd.Core.SectionData;

namespace Lyra.Psd.Tests;

/// <summary>
/// A crafted header claiming far more pixel data than the stream holds must be rejected
/// before any large buffer is allocated.
/// </summary>
public class DimensionGuardTests
{
    [Fact]
    public void RawRejectsOversizedDimensions()
    {
        var header = new FileHeader(1, NumberOfChannels: 3, Width: 100_000, Height: 100_000, Depth: 8, ColorMode.Rgb);
        var tinyStream = new byte[16];

        Assert.Throws<InvalidDataException>(() => DecodeFull(new PsdRawDecompressor(), header, tinyStream));
    }

    [Fact]
    public void RleRejectsOversizedRowTable()
    {
        var header = new FileHeader(1, NumberOfChannels: 3, Width: 8, Height: 100_000, Depth: 8, ColorMode.Rgb);
        var tinyStream = new byte[16];

        Assert.Throws<InvalidDataException>(() => DecodeFull(new PsdRleDecompressor(), header, tinyStream));
    }

    private static void DecodeFull(IPsdDecompressor decompressor, FileHeader header, byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        var reader = new PsdBigEndianReader(stream);

        // A throwaway sink; the guard should fire before any row is produced.
        var sink = new ThrowingSink();
        decompressor.DecompressPlanesRowRegion(reader, header, 0, header.Height, sink, CancellationToken.None);
    }

    private sealed class ThrowingSink : IPlaneRowConsumer
    {
        public void ConsumeRow(int planeIndex, int y, ReadOnlySpan<byte> row)
            => throw new Xunit.Sdk.XunitException("Guard should have rejected the header before producing rows.");
    }
}