using System.Buffers.Binary;
using Lyra.Psd.Core.Common;
using Lyra.Psd.Core.Decode.Color;
using Lyra.Psd.Core.Decode.Color.Processors;
using Lyra.Psd.Core.Decode.Composite;
using Lyra.Psd.Core.Decode.Pixel;

namespace Lyra.Psd.Tests;

public class LabProcessorTests
{
    private static readonly SurfaceFormat Rgba8Straight = new(PixelFormat.Rgba8888, AlphaType.Straight);

    [Fact]
    public void NeutralsAndKnownColorsDecode()
    {
        // Pixels: white Lab(100,0,0), black Lab(0,0,0), mid-gray Lab(~50,0,0), sRGB-red Lab(53.24, 80.09, 67.20).
        byte[] l = [255, 0, 128, 136];
        byte[] a = [128, 128, 128, 208];
        byte[] b = [128, 128, 128, 195];

        var px = DecodeRow(MakeLab8(4, 1, l, a, b));

        // white
        Assert.True(px[0].r >= 250 && px[0].g >= 250 && px[0].b >= 250, $"white={px[0]}");
        // black
        Assert.Equal((0, 0, 0), px[1]);
        // neutral gray: channels equal, mid-range
        Assert.True(Math.Abs(px[2].r - px[2].g) <= 1 && Math.Abs(px[2].g - px[2].b) <= 1, $"gray not neutral: {px[2]}");
        Assert.InRange(px[2].r, 100, 140);
        // red-ish
        Assert.True(px[3].r >= 230 && px[3].g <= 40 && px[3].b <= 40, $"red={px[3]}");
    }

    [Fact]
    public void SixteenBitMatchesEightBit()
    {
        byte[] l = [255, 0, 128, 136];
        byte[] a = [128, 128, 128, 208];
        byte[] b = [128, 128, 128, 195];

        var expected = DecodeRow(MakeLab8(4, 1, l, a, b));
        var actual = DecodeRow(MakeLab16(4, 1, l, a, b));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void StraightAlphaIsPreserved()
    {
        byte[] l = [128];
        byte[] a = [128];
        byte[] b = [128];
        byte[] alpha = [64];

        var px = DecodeRow(MakeLab8(1, 1, l, a, b, alpha), Rgba8Straight, out var alphaOut);
        Assert.Equal(64, alphaOut[0]);
        Assert.True(px[0].r > 0); // color not zeroed under straight alpha
    }

    [Fact]
    public void ThirtyTwoBitLabIsRejected()
    {
        var planes = new List<Plane>
        {
            new(PlaneRole.L, new byte[4], 4),
            new(PlaneRole.LabA, new byte[4], 4),
            new(PlaneRole.LabB, new byte[4], 4),
        };
        
        var img = new PlaneImage(1, 1, 32, planes);

        Assert.Throws<NotSupportedException>(() => DecodeRow(img));
    }

    private static (byte r, byte g, byte b)[] DecodeRow(PlaneImage img) => DecodeRow(img, Rgba8Straight, out _);

    private static (byte r, byte g, byte b)[] DecodeRow(PlaneImage img, SurfaceFormat format, out byte[] alphaOut)
    {
        var ctx = new ColorModeContext(ColorMode.Lab, format, IndexedPaletteRgb: null, IccProfile: null, PreferColorManagement: true);
        using var surface = new LabProcessor().Process(img, ctx, colorModeData: null, CancellationToken.None);

        var row = surface.GetRowSpan(0);
        var result = new (byte r, byte g, byte b)[img.Width];
        alphaOut = new byte[img.Width];

        for (var x = 0; x < img.Width; x++)
        {
            result[x] = (row[x * 4 + 0], row[x * 4 + 1], row[x * 4 + 2]);
            alphaOut[x] = row[x * 4 + 3];
        }

        return result;
    }

    private static PlaneImage MakeLab8(int w, int h, byte[] l, byte[] a, byte[] b, byte[]? alpha = null)
    {
        var planes = new List<Plane>
        {
            new(PlaneRole.L, l, w),
            new(PlaneRole.LabA, a, w),
            new(PlaneRole.LabB, b, w),
        };

        if (alpha != null)
            planes.Add(new Plane(PlaneRole.A, alpha, w));

        return new PlaneImage(w, h, 8, planes);
    }

    private static PlaneImage MakeLab16(int w, int h, byte[] l8, byte[] a8, byte[] b8)
    {
        // value*257 round-trips exactly through the 16->8 conversion, so the 16-bit path
        // should reproduce the 8-bit result.
        var planes = new List<Plane>
        {
            new(PlaneRole.L, Widen(l8), w * 2),
            new(PlaneRole.LabA, Widen(a8), w * 2),
            new(PlaneRole.LabB, Widen(b8), w * 2),
        };

        return new PlaneImage(w, h, 16, planes);

        static byte[] Widen(byte[] src)
        {
            var dst = new byte[src.Length * 2];
            for (var i = 0; i < src.Length; i++)
                BinaryPrimitives.WriteUInt16BigEndian(dst.AsSpan(i * 2, 2), (ushort)(src[i] * 257));

            return dst;
        }
    }
}