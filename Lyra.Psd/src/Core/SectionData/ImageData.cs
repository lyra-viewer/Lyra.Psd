using Lyra.Psd.Core.Common;

namespace Lyra.Psd.Core.SectionData;

public readonly record struct ImageData(
    long PayloadOffset, // Points to the first byte AFTER the 2-byte compression field.
    long PayloadLength, // Until EOF (TODO or computed)
    CompressionType CompressionType
);