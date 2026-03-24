namespace Lyra.Psd.Core.SectionData;

public readonly record struct ImageResources(
    uint Length,
    ImageResourceBlockHeader[] Blocks
);