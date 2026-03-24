using Lyra.Psd.Core.Common;

namespace Lyra.Psd.Core.SectionData;

public readonly record struct FileHeader(
    ushort Version,
    ushort NumberOfChannels,
    int Width,
    int Height,
    ushort Depth,
    ColorMode ColorMode
);