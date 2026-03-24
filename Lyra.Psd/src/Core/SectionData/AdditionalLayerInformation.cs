using Lyra.Psd.Core.Common;

namespace Lyra.Psd.Core.SectionData;

public readonly record struct AdditionalLayerInformation(
    uint Signature,
    uint Key,
    long PayloadOffset,
    long PayloadLength
)
{
    public string KeyFourCC => FourCC.ToString(Key);
    public string SignatureFourCC => FourCC.ToString(Signature);
}