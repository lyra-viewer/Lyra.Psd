using System.Buffers;
using Lyra.Psd.Core.Common;
using Lyra.Psd.Core.Decode.ColorCalibration.Lab;
using Lyra.Psd.Core.Decode.ColorCalibration.Rgb;
using Lyra.Psd.Core.Decode.Composite;
using Lyra.Psd.Core.Decode.Pixel;
using Lyra.Psd.Core.SectionData;

namespace Lyra.Psd.Core.Decode.Color.Processors;

/// <summary>
/// Lab color mode (L*a*b*) -> RGBA surface.
///
/// MVP: supports 8- and 16-bit Lab (the depths Photoshop writes). 16-bit samples are reduced to
/// 8-bit first (consistent with every other processor, since the output is RGBA8 regardless), then
/// converted to sRGB via <see cref="LabToRgbConverter"/>. Channel packing, alpha handling, and
/// premultiplication reuse <see cref="PixelRowWriter.WriteRgbRow"/>.
/// </summary>
public sealed class LabProcessor : IColorModeProcessor
{
    public string? IccProfileUsed { get; private set; }

    // Instance-scoped (one processor per decode operation) so the conversion cache stays warm
    // across all tiles of a tiled decode instead of being rebuilt (512 KB) per tile.
    private readonly LabToRgbConverter _converter = new();

    public RgbaSurface Process(PlaneImage src, ColorModeContext ctx, ColorModeData? colorModeData, CancellationToken ct)
    {
        var depth = PsdDepthUtil.FromBitsPerChannel(src.BitsPerChannel);
        var bpcBytes = depth.BytesPerChannel();
        var rowBytes = depth.RowBytes(src.Width);

        // 32-bit Lab is exceedingly rare and its float encoding is not the normalized [0,1] form
        // the shared sample converter assumes; reject it explicitly rather than produce wrong color.
        if (bpcBytes == 4)
            throw new NotSupportedException("32-bit/channel Lab is not supported yet (only 8- and 16-bit).");

        var l = src.GetPlaneOrThrow(PlaneRole.L);
        var aLab = src.GetPlaneOrThrow(PlaneRole.LabA);
        var bLab = src.GetPlaneOrThrow(PlaneRole.LabB);
        src.TryGetPlane(PlaneRole.A, out var alpha);
        var hasAlpha = alpha.Data is { Length: > 0 };

        if (l.BytesPerRow < rowBytes || aLab.BytesPerRow < rowBytes || bLab.BytesPerRow < rowBytes)
            throw new InvalidOperationException("Lab plane BytesPerRow is smaller than Width*bpc.");

        if (hasAlpha && alpha.BytesPerRow < rowBytes)
            throw new InvalidOperationException("Alpha plane BytesPerRow is smaller than Width*bpc.");

        // Lab is device-independent; the MVP applies the standard D50 Lab -> sRGB transform rather
        // than an embedded ICC profile, so no profile is reported as "used".
        IccProfileUsed = null;

        var stride = checked(src.Width * 4);
        var size = checked(stride * src.Height);

        var owner = MemoryPool<byte>.Shared.Rent(size);
        var surface = new RgbaSurface(src.Width, src.Height, owner, stride, ctx.OutputFormat);

        var rOut = ArrayPool<byte>.Shared.Rent(src.Width);
        var gOut = ArrayPool<byte>.Shared.Rent(src.Width);
        var bOut = ArrayPool<byte>.Shared.Rent(src.Width);

        // 16-bit -> 8-bit scratch (only when needed).
        byte[]? l8Rent = null, a8Rent = null, b8Rent = null, alpha8Rent = null;
        if (bpcBytes != 1)
        {
            l8Rent = ArrayPool<byte>.Shared.Rent(src.Width);
            a8Rent = ArrayPool<byte>.Shared.Rent(src.Width);
            b8Rent = ArrayPool<byte>.Shared.Rent(src.Width);
            if (hasAlpha)
                alpha8Rent = ArrayPool<byte>.Shared.Rent(src.Width);
        }

        try
        {
            for (var y = 0; y < src.Height; y++)
            {
                ct.ThrowIfCancellationRequested();

                ReadOnlySpan<byte> l8, a8, b8;
                ReadOnlySpan<byte> aRow8 = default;

                if (bpcBytes == 1)
                {
                    l8 = l.Data.AsSpan(y * l.BytesPerRow, src.Width);
                    a8 = aLab.Data.AsSpan(y * aLab.BytesPerRow, src.Width);
                    b8 = bLab.Data.AsSpan(y * bLab.BytesPerRow, src.Width);

                    if (hasAlpha)
                        aRow8 = alpha.Data.AsSpan(y * alpha.BytesPerRow, src.Width);
                }
                else // 16-bit
                {
                    var lt = l8Rent!.AsSpan(0, src.Width);
                    var at = a8Rent!.AsSpan(0, src.Width);
                    var bt = b8Rent!.AsSpan(0, src.Width);

                    PsdSampleConvert.Row16BeTo8(l.Data.AsSpan(y * l.BytesPerRow, rowBytes), lt);
                    PsdSampleConvert.Row16BeTo8(aLab.Data.AsSpan(y * aLab.BytesPerRow, rowBytes), at);
                    PsdSampleConvert.Row16BeTo8(bLab.Data.AsSpan(y * bLab.BytesPerRow, rowBytes), bt);

                    l8 = lt;
                    a8 = at;
                    b8 = bt;

                    if (hasAlpha)
                    {
                        var alt = alpha8Rent!.AsSpan(0, src.Width);
                        PsdSampleConvert.Row16BeTo8(alpha.Data.AsSpan(y * alpha.BytesPerRow, rowBytes), alt);
                        aRow8 = alt;
                    }
                }

                var rr = rOut.AsSpan(0, src.Width);
                var gg = gOut.AsSpan(0, src.Width);
                var bb = bOut.AsSpan(0, src.Width);

                for (var x = 0; x < src.Width; x++)
                    _converter.Convert(l8[x], a8[x], b8[x], out rr[x], out gg[x], out bb[x]);

                PixelRowWriter.WriteRgbRow(
                    surface.GetRowSpan(y),
                    ctx.OutputFormat.PixelFormat,
                    ctx.OutputFormat.AlphaType,
                    rr, gg, bb,
                    hasAlpha ? aRow8 : default,
                    hasAlpha,
                    useCalibration: false,
                    RgbLuts.Identity
                );
            }

            return surface;
        }
        catch
        {
            surface.Dispose();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rOut);
            ArrayPool<byte>.Shared.Return(gOut);
            ArrayPool<byte>.Shared.Return(bOut);

            if (l8Rent != null)
                ArrayPool<byte>.Shared.Return(l8Rent);

            if (a8Rent != null)
                ArrayPool<byte>.Shared.Return(a8Rent);

            if (b8Rent != null)
                ArrayPool<byte>.Shared.Return(b8Rent);

            if (alpha8Rent != null)
                ArrayPool<byte>.Shared.Return(alpha8Rent);
        }
    }
}