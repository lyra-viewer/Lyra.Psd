namespace Lyra.Psd.Core.Decode.Layers;

public readonly record struct LayerRecord(
    int Top,
    int Left,
    int Bottom,
    int Right,
    string Name
)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}