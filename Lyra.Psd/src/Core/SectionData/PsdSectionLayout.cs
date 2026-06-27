namespace Lyra.Psd.Core.SectionData;

/// <summary>
/// On-disk span of a single top-level section: its absolute byte <paramref name="Offset"/> from the
/// start of the file and its total <paramref name="Length"/> in bytes (including any length-prefix
/// framing that belongs to the section).
/// </summary>
public readonly record struct PsdSectionSpan(long Offset, long Length);

/// <summary>
/// Authoritative byte layout of the five top-level PSD/PSB sections, captured from actual stream
/// positions during <see cref="PsdDocument.ReadDocument"/>. Consumers (e.g. a structure inspector)
/// should read offsets/lengths from here rather than re-deriving them from framing constants.
/// </summary>
public readonly record struct PsdSectionLayout(
    PsdSectionSpan FileHeader,
    PsdSectionSpan ColorModeData,
    PsdSectionSpan ImageResources,
    PsdSectionSpan LayerAndMaskInformation,
    PsdSectionSpan ImageData
);