using Lyra.Psd.Core.Common;
using Lyra.Psd.Core.Decode.Color;
using Lyra.Psd.Core.Decode.Color.Processors;
using Lyra.Psd.Core.Decode.ColorCalibration.Rgb;
using Lyra.Psd.Core.Decode.Composite;
using Lyra.Psd.Core.Decode.Pixel;

namespace Lyra.Psd.Tests;

public class CalibrationIsolationTests
{
    private const string AdobeRgbProfilePath = "/System/Library/ColorSync/Profiles/AdobeRGB1998.icc";

    private static readonly SurfaceFormat Rgba8Straight = new(PixelFormat.Rgba8888, AlphaType.Straight);

    [Fact]
    public void SameImage_DecodesIdentically_RegardlessOfWhatDecodedBefore()
    {
        if (!File.Exists(AdobeRgbProfilePath))
            return;

        var icc = File.ReadAllBytes(AdobeRgbProfilePath);

        var gradient = MakeGradientRgb8(64, 64);
        var white = MakeFlatRgb8(64, 64, 255);

        // Baseline: gradient decoded in a fresh "document".
        var expected = Decode(gradient, icc, new RgbCalibrationProvider());

        // A different document decodes a pure-white image first (this used to poison the
        // profile-keyed static cache with a constant-255 curve)...
        _ = Decode(white, icc, new RgbCalibrationProvider());

        // ...then the gradient decodes in its own document and must be unaffected.
        var actual = Decode(gradient, icc, new RgbCalibrationProvider());

        Assert.Equal(expected, actual);
        Assert.Contains(actual, px => px is not (255, 255, 255)); // and it is certainly not all white
    }

    [Fact]
    public void SameDocument_ReusesOneLut_AcrossSurfaces()
    {
        if (!File.Exists(AdobeRgbProfilePath))
            return;

        var icc = File.ReadAllBytes(AdobeRgbProfilePath);
        var gradient = MakeGradientRgb8(64, 64);

        // Two surfaces of one document (e.g. preview + tile) share the document provider and
        // must produce identical pixels.
        var provider = new RgbCalibrationProvider();
        var first = Decode(gradient, icc, provider);
        var second = Decode(gradient, icc, provider);

        Assert.Equal(first, second);
    }

    private static (byte r, byte g, byte b)[] Decode(PlaneImage img, byte[] icc, RgbCalibrationProvider provider)
    {
        var ctx = new ColorModeContext(
            ColorMode.Rgb,
            Rgba8Straight,
            IndexedPaletteRgb: null,
            IccProfile: icc,
            PreferColorManagement: true,
            Calibration: provider);

        using var surface = new RgbProcessor().Process(img, ctx, colorModeData: null, CancellationToken.None);

        var pixels = new (byte r, byte g, byte b)[surface.Width * surface.Height];
        for (var y = 0; y < surface.Height; y++)
        {
            var row = surface.GetRowSpan(y);
            for (var x = 0; x < surface.Width; x++)
                pixels[y * surface.Width + x] = (row[x * 4], row[x * 4 + 1], row[x * 4 + 2]);
        }

        return pixels;
    }

    private static PlaneImage MakeGradientRgb8(int w, int h)
    {
        var r = new byte[w * h];
        var g = new byte[w * h];
        var b = new byte[w * h];

        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var i = y * w + x;
            r[i] = (byte)(x * 255 / (w - 1));
            g[i] = (byte)(y * 255 / (h - 1));
            b[i] = (byte)((x + y) * 255 / (w + h - 2));
        }

        return new PlaneImage(w, h, 8, [new Plane(PlaneRole.R, r, w), new Plane(PlaneRole.G, g, w), new Plane(PlaneRole.B, b, w)]);
    }

    private static PlaneImage MakeFlatRgb8(int w, int h, byte value)
    {
        var plane = new byte[w * h];
        Array.Fill(plane, value);
        return new PlaneImage(w, h, 8, [new Plane(PlaneRole.R, plane, w), new Plane(PlaneRole.G, plane, w), new Plane(PlaneRole.B, plane, w)]);
    }
}