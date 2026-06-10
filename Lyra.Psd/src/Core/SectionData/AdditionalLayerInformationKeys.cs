using Lyra.Psd.Core.Common;

namespace Lyra.Psd.Core.SectionData;

internal static class AdditionalLayerInformationKeys
{
    public static readonly uint UnicodeLayerName = FourCC.FromString("luni");
    public static readonly uint LayerId          = FourCC.FromString("lyid");
    public static readonly uint SectionDivider   = FourCC.FromString("lsct");
    public static readonly uint BlendClipping    = FourCC.FromString("clbl");
    public static readonly uint Protected        = FourCC.FromString("lspf");

    // PSB: these keys carry an 8-byte (uint64) length field instead of the usual 4-byte (uint32).
    private static readonly HashSet<uint> LongLengthPsbKeys =
    [
        FourCC.FromString("LMsk"), // user mask (extra)
        FourCC.FromString("Lr16"), // 16-bit layer info
        FourCC.FromString("Lr32"), // 32-bit layer info
        FourCC.FromString("Layr"), // layer info
        FourCC.FromString("Mt16"), // 16-bit merged transparency
        FourCC.FromString("Mt32"), // 32-bit merged transparency
        FourCC.FromString("Mtrn"), // merged transparency
        FourCC.FromString("Alph"), // alpha
        FourCC.FromString("FMsk"), // filter mask
        FourCC.FromString("lnk2"), // linked layer
        FourCC.FromString("FEid"), // filter effects
        FourCC.FromString("FXid"), // filter effects
        FourCC.FromString("PxSD"), // pixel source data
    ];

    public static bool UsesLongLengthFieldInPsb(uint key) => LongLengthPsbKeys.Contains(key);
}