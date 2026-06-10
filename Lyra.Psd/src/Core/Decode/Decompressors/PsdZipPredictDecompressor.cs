using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Lyra.Psd.Core.Common;
using Lyra.Psd.Core.Decode.Pixel;
using Lyra.Psd.Core.Readers;
using Lyra.Psd.Core.SectionData;

namespace Lyra.Psd.Core.Decode.Decompressors;

internal sealed class PsdZipPredictDecompressor : PsdDecompressorBase
{
    protected override PlaneImage Decompress8(PsdBigEndianReader reader, FileHeader header, PlaneRole[] roles, CancellationToken ct)
        => DecompressZipPredict(reader, header, roles, PsdDepth.Bit8, ct);

    protected override PlaneImage Decompress16(PsdBigEndianReader reader, FileHeader header, PlaneRole[] roles, CancellationToken ct)
        => DecompressZipPredict(reader, header, roles, PsdDepth.Bit16, ct);

    protected override PlaneImage Decompress32(PsdBigEndianReader reader, FileHeader header, PlaneRole[] roles, CancellationToken ct)
        => DecompressZipPredict(reader, header, roles, PsdDepth.Bit32, ct);

    protected override PlaneImage Decompress8Preview(PsdBigEndianReader reader, FileHeader header, PlaneRole[] roles, int outWidth, int outHeight, CancellationToken ct)
        => DecompressZipPredictPreview(reader, header, roles, outWidth, outHeight, PsdDepth.Bit8, ct);

    protected override PlaneImage Decompress16Preview(PsdBigEndianReader reader, FileHeader header, PlaneRole[] roles, int outWidth, int outHeight, CancellationToken ct)
        => DecompressZipPredictPreview(reader, header, roles, outWidth, outHeight, PsdDepth.Bit16, ct);

    protected override PlaneImage Decompress32Preview(PsdBigEndianReader reader, FileHeader header, PlaneRole[] roles, int outWidth, int outHeight, CancellationToken ct)
        => DecompressZipPredictPreview(reader, header, roles, outWidth, outHeight, PsdDepth.Bit32, ct);

    private static PlaneImage DecompressZipPredict(PsdBigEndianReader reader, FileHeader header, PlaneRole[] roles, PsdDepth depth, CancellationToken ct)
    {
        var width = header.Width;
        var height = header.Height;

        var rowBytes = depth.RowBytes(width);

        var planes = AllocatePlanes(width, height, (int)depth, roles);

        using var z = new ZLibStream(reader.BaseStream, CompressionMode.Decompress, leaveOpen: true);
        var zReader = new PsdBigEndianReader(z);

        var rentedRow = ArrayPool<byte>.Shared.Rent(rowBytes);
        var rentedScratch = depth == PsdDepth.Bit32 ? ArrayPool<byte>.Shared.Rent(rowBytes) : null;
        try
        {
            var row = rentedRow.AsSpan(0, rowBytes);
            var scratch = rentedScratch is null ? Span<byte>.Empty : rentedScratch.AsSpan(0, rowBytes);

            for (var p = 0; p < roles.Length; p++)
            {
                var plane = planes[p];
                for (var y = 0; y < height; y++)
                {
                    ct.ThrowIfCancellationRequested();

                    zReader.ReadExactly(row);
                    UndoPredictor(row, width, depth, scratch);

                    row.CopyTo(plane.Data.AsSpan(y * plane.BytesPerRow, rowBytes));
                }
            }

            return new PlaneImage(width, height, (int)depth, planes);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedRow);
            if (rentedScratch is not null)
                ArrayPool<byte>.Shared.Return(rentedScratch);
        }
    }

    private static PlaneImage DecompressZipPredictPreview(PsdBigEndianReader reader, FileHeader header, PlaneRole[] roles, int outWidth, int outHeight, PsdDepth depth, CancellationToken ct)
    {
        using var z = new ZLibStream(reader.BaseStream, CompressionMode.Decompress, leaveOpen: true);
        var zReader = new PsdBigEndianReader(z);

        var scratch = depth == PsdDepth.Bit32 ? new byte[depth.RowBytes(header.Width)] : [];

        return DecodeScaledPlanesByRows(header.Width, header.Height, outWidth, outHeight, depth, roles, ct, CreatePlaneRowReader);

        ReadNextSourceRow CreatePlaneRowReader() => rowBuffer =>
        {
            zReader.ReadExactly(rowBuffer);
            UndoPredictor(rowBuffer, header.Width, depth, scratch);
        };
    }

    private static void UndoPredictor(Span<byte> rowBytes, int widthPixels, PsdDepth depth, Span<byte> scratch)
    {
        switch (depth)
        {
            case PsdDepth.Bit8:
                UndoPredictor8(rowBytes);
                return;
            case PsdDepth.Bit16:
                UndoPredictor16(rowBytes, widthPixels);
                return;
            case PsdDepth.Bit32:
                UndoPredictor32(rowBytes, widthPixels, scratch);
                return;
            default:
                throw new NotSupportedException($"Predictor undo not supported for depth {depth}.");
        }
    }

    private static void UndoPredictor8(Span<byte> row)
    {
        for (var x = 1; x < row.Length; x++)
            row[x] = unchecked((byte)(row[x] + row[x - 1]));
    }

    private static void UndoPredictor16(Span<byte> rowBytes, int widthPixels)
    {
        if (rowBytes.Length < widthPixels * 2)
            throw new InvalidOperationException($"Predictor16 row length mismatch: got {rowBytes.Length}, expected {widthPixels * 2}.");

        ref var rowRef = ref MemoryMarshal.GetReference(rowBytes);
        var prev = BinaryPrimitives.ReadUInt16BigEndian(MemoryMarshal.CreateReadOnlySpan(ref rowRef, 2));

        for (var x = 1; x < widthPixels; x++)
        {
            var off = x * 2;
            ref var currentRef = ref Unsafe.Add(ref rowRef, off);

            var delta = BinaryPrimitives.ReadUInt16BigEndian(MemoryMarshal.CreateReadOnlySpan(ref currentRef, 2));

            unchecked
            {
                prev = (ushort)(delta + prev);
                BinaryPrimitives.WriteUInt16BigEndian(MemoryMarshal.CreateSpan(ref currentRef, 2), prev);
            }
        }
    }

    // ------------------------------------------------------------------------
    //  32-bit float prediction (Adobe PSD == TIFF floating-point predictor 3).
    //
    //  The encoder, per row:
    //  1. Splits each big-endian float into 4 byte-planes and stores them
    //     planar: [all byte0][all byte1][all byte2][all byte3].
    //  2. Applies byte-wise horizontal differencing across the whole row.
    //
    //  Decoding therefore reverses that order:
    //  1. Undo the byte-wise differencing across the entire row (stride 1).
    //  2. De-shuffle the planar bytes back into interleaved big-endian floats.
    //
    //  NOTE: this is fundamentally different from the 16-bit case, which is a
    //  plain per-sample (uint16) horizontal delta with no byte reshuffle.
    // ------------------------------------------------------------------------
    private static void UndoPredictor32(Span<byte> rowBytes, int widthPixels, Span<byte> scratch)
    {
        var needed = widthPixels * 4;
        if (rowBytes.Length < needed)
            throw new InvalidOperationException($"Predictor32 row length mismatch: got {rowBytes.Length}, expected {needed}.");

        if (scratch.Length < needed)
            throw new InvalidOperationException($"Predictor32 scratch too small: got {scratch.Length}, expected {needed}.");

        var row = rowBytes[..needed];

        for (var i = 1; i < needed; i++)
            row[i] = unchecked((byte)(row[i] + row[i - 1]));

        var tmp = scratch[..needed];
        row.CopyTo(tmp);

        for (var b = 0; b < 4; b++)
        {
            var planeBase = b * widthPixels;
            for (var x = 0; x < widthPixels; x++)
                row[x * 4 + b] = tmp[planeBase + x];
        }
    }
}