using Lyra.Psd.Core.Common;
using Lyra.Psd.Core.Readers;
using Lyra.Psd.Core.SectionData;

namespace Lyra.Psd.Core.SectionReaders;

internal static class ImageDataReader
{
    public static ImageData Read(PsdBigEndianReader reader)
    {
        var compression = (CompressionType)reader.ReadUInt16();

        var payloadOffset = reader.Position;
        var payloadLength = reader.Length - payloadOffset;

        return new ImageData(payloadOffset, payloadLength, compression);
    }
}