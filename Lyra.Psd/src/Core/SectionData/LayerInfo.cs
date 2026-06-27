namespace Lyra.Psd.Core.SectionData;

public readonly record struct LayerInfo(
    long PayloadOffset,
    long PayloadLength,
    short LayerCount
)
{
    public bool HasMergedAlpha => LayerCount < 0;

    /// <summary>
    /// Actual number of layers. A negative stored <see cref="LayerCount"/> is Photoshop's flag for
    /// "first alpha channel holds merged transparency"; the real count is its magnitude.
    /// </summary>
    public int EffectiveLayerCount => Math.Abs((int)LayerCount);
}