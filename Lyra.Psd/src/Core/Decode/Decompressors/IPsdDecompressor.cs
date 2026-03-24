using Lyra.Psd.Core.Decode.Pixel;
using Lyra.Psd.Core.Decode.PlaneRowConsumer;
using Lyra.Psd.Core.Readers;
using Lyra.Psd.Core.SectionData;

namespace Lyra.Psd.Core.Decode.Decompressors;

public interface IPsdDecompressor
{
    PlaneImage DecompressPreview(PsdBigEndianReader reader, FileHeader header, int width, int height, CancellationToken ct);

    void DecompressPlanesRowRegion(PsdBigEndianReader reader, FileHeader header, int yStart, int yEnd, IPlaneRowConsumer consumer, CancellationToken ct);
}