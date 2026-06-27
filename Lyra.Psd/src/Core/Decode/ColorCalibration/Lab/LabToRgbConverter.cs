using Wacton.Unicolour;

namespace Lyra.Psd.Core.Decode.ColorCalibration.Lab;

/// <summary>
/// Converts 8-bit PSD Lab samples to sRGB.
///
/// PSD stores Lab using the D50 illuminant (the ICC PCS white point); the result is converted
/// to standard (D65) sRGB with chromatic adaptation handled by Unicolour.
///
/// 8-bit decode mapping (per Adobe):
///   L* = L / 255 * 100      (0..255   -> 0..100)
///   a* = a - 128            (0..255   -> -128..127)
///   b* = b - 128            (0..255   -> -128..127)
///
/// MVP note: this performs a per-pixel Unicolour conversion backed by a bounded, direct-mapped
/// cache. It is correct but not the fastest possible path; a precomputed 3D grid LUT with
/// tetrahedral interpolation (mirroring the CMYK path) is the planned optimization. The conversion
/// is isolated here so that swap is a localized change.
/// </summary>
internal sealed class LabToRgbConverter
{
    private static readonly Configuration LabToSrgb = new(RgbConfiguration.StandardRgb, XyzConfiguration.D50);

    // Direct-mapped cache: key = 24-bit (L,a,b); value packs the 24-bit RGB plus an "occupied" bit.
    private const int CacheBits = 16;
    private const int CacheSize = 1 << CacheBits;
    private const ulong OccupiedFlag = 1UL << 48;

    private readonly ulong[] _cache = new ulong[CacheSize];

    public void Convert(byte l8, byte a8, byte b8, out byte r, out byte g, out byte b)
    {
        var key = (uint)((l8 << 16) | (a8 << 8) | b8);
        var slot = (key * 2654435761u) >> (32 - CacheBits);

        var entry = _cache[slot];
        if (entry != 0 && (uint)(entry & 0xFFFFFF) == key)
        {
            var cachedRgb = (uint)((entry >> 24) & 0xFFFFFF);
            r = (byte)cachedRgb;
            g = (byte)(cachedRgb >> 8);
            b = (byte)(cachedRgb >> 16);
            return;
        }

        var lStar = l8 * (100.0 / 255.0);
        var aStar = a8 - 128.0;
        var bStar = b8 - 128.0;

        var colour = new Unicolour(LabToSrgb, ColourSpace.Lab, lStar, aStar, bStar);
        var bytes = colour.Rgb.Byte255;

        r = ClampByte(bytes.R);
        g = ClampByte(bytes.G);
        b = ClampByte(bytes.B);

        var rgb = (uint)r | ((uint)g << 8) | ((uint)b << 16);
        _cache[slot] = OccupiedFlag | ((ulong)rgb << 24) | key;
    }

    private static byte ClampByte(int v) => v < 0 ? (byte)0 : v > 255 ? (byte)255 : (byte)v;
}