using System.Collections.Concurrent;
using Lyra.Psd.Core.Common;
using Wacton.Unicolour;
using Wacton.Unicolour.Icc;

namespace Lyra.Psd.Core.Decode.ColorCalibration.Rgb;

public sealed class RgbCalibrationProvider
{
    // Lazy so concurrent callers (e.g. preview + tiles) build a given LUT exactly once.
    // SourceColorMode is part of the key: RGB and Grayscale builders produce different curves
    // for the same profile, and one provider may serve either depending on the document.
    private readonly ConcurrentDictionary<(ColorMode mode, string key, int grid), Lazy<RgbLuts>> _cache = new();

    public RgbLuts GetCalibration(RgbCalibrationRequest req, Func<Configuration, RgbLuts> buildLuts, out string? iccProfileUsed)
    {
        ArgumentNullException.ThrowIfNull(req);
        ArgumentNullException.ThrowIfNull(buildLuts);

        iccProfileUsed = null;

        if (!req.PreferColorManagement)
            return RgbLuts.Identity;

        if (req.EmbeddedIccProfile is { Length: > 0 })
        {
            var profileKey = Hash(req.EmbeddedIccProfile);
            var icc = req.EmbeddedIccProfile;

            iccProfileUsed = "Embedded ICC Profile";

            var lazy = _cache.GetOrAdd(
                (req.SourceColorMode, profileKey, req.GridSize),
                _ => new Lazy<RgbLuts>(
                    () => buildLuts(new Configuration(iccConfig: new IccConfiguration(icc, Intent.RelativeColorimetric))),
                    LazyThreadSafetyMode.ExecutionAndPublication));

            try
            {
                return lazy.Value;
            }
            catch
            {
                // If the factory threw, remove the faulted Lazy so the next call retries.
                _cache.TryRemove((req.SourceColorMode, profileKey, req.GridSize), out _);
                throw;
            }
        }

        return RgbLuts.Identity;
    }

    private static string Hash(byte[] data) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data));
}