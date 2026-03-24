using Lyra.Psd.Core.Common;

namespace Lyra.Psd.Core.Decode.ColorCalibration.Rgb;

public sealed record RgbCalibrationRequest
(
    ColorMode SourceColorMode,
    byte[]? EmbeddedIccProfile,
    bool PreferColorManagement,
    int GridSize
);