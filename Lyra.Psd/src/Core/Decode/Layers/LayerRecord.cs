namespace Lyra.Psd.Core.Decode.Layers;

public readonly record struct LayerRecord(
    int Top,
    int Left,
    int Bottom,
    int Right,
    string Name,
    LayerSectionType SectionType,
    bool Visible,
    byte Opacity,
    uint BlendModeKey
)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}