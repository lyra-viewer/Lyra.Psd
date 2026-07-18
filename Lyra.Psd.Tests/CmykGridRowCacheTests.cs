using Lyra.Psd.Core.Common;
using Lyra.Psd.Core.Decode.ColorCalibration.Cmyk;
using Lyra.Psd.Core.Decode.Decompressors;
using Lyra.Psd.Core.Decode.Pixel;
using Lyra.Psd.Core.Decode.Composite;

namespace Lyra.Psd.Tests;

public class CmykGridRowCacheTests
{
    /// <summary>
    /// Regression: the per-row cache in WriteCmykRowGridLut used 0xFFFFFFFFFFFFFFFF as its
    /// empty sentinel, whose low 32 bits equal the CMYK key (255,255,255,255) - PSD "no ink",
    /// i.e. paper white. Those pixels false-hit the empty slot and bypassed the LUT, returning
    /// hardcoded 255,255,255 instead of the profile's actual paper white.
    /// </summary>
    [Fact]
    public void NoInkPixels_ConsultTheLut_InsteadOfAliasingTheEmptyCacheSlot()
    {
        // A LUT whose every sample is (250,250,250): any pixel that really goes through the
        // LUT must come out 250; the old sentinel alias produced 255 for no-ink pixels.
        const int gridSize = 2;
        var samples = new byte[gridSize * gridSize * gridSize * gridSize * 3];
        Array.Fill(samples, (byte)250);
        var lut = new CmykGridLut(gridSize, invertInput: true, samples);
        var tables = new PixelRowWriter.CmykGridLookupTables(gridSize);

        const int width = 4;
        Span<byte> noInk = stackalloc byte[width];
        noInk.Fill(255);

        var dst = new byte[width * 4];

        PixelRowWriter.WriteCmykRowGridLut(
            dst,
            PixelFormat.Rgba8888,
            AlphaType.Straight,
            noInk, noInk, noInk, noInk,
            aRow: default,
            hasAlpha: false,
            lut,
            tables);

        for (var x = 0; x < width; x++)
        {
            Assert.Equal(250, dst[x * 4 + 0]);
            Assert.Equal(250, dst[x * 4 + 1]);
            Assert.Equal(250, dst[x * 4 + 2]);
            Assert.Equal(255, dst[x * 4 + 3]);
        }
    }
}

public class CompositePlaneRolesValidationTests
{
    /// <summary>
    /// Regression: a header whose channel count cannot satisfy its color mode used to fail deep
    /// inside the decompressor with a confusing row-table/EOF error; it must fail fast instead.
    /// </summary>
    [Theory]
    [InlineData(ColorMode.Rgb, 2)]
    [InlineData(ColorMode.Rgb, 1)]
    [InlineData(ColorMode.Cmyk, 3)]
    [InlineData(ColorMode.Lab, 2)]
    public void ChannelCountTooSmallForColorMode_ThrowsInvalidData(ColorMode mode, int channels)
    {
        Assert.Throws<InvalidDataException>(() => CompositePlaneRoles.Get(mode, channels));
    }

    [Theory]
    [InlineData(ColorMode.Rgb, 3, 3)]
    [InlineData(ColorMode.Rgb, 4, 4)]
    [InlineData(ColorMode.Rgb, 5, 4)]
    [InlineData(ColorMode.Cmyk, 4, 4)]
    [InlineData(ColorMode.Cmyk, 5, 5)]
    [InlineData(ColorMode.Grayscale, 1, 1)]
    [InlineData(ColorMode.Grayscale, 2, 2)]
    [InlineData(ColorMode.Lab, 3, 3)]
    [InlineData(ColorMode.Multichannel, 3, 3)]
    public void ValidCombinations_YieldExpectedRoleCount(ColorMode mode, int channels, int expectedRoles)
    {
        Assert.Equal(expectedRoles, CompositePlaneRoles.Get(mode, channels).Length);
    }
}
