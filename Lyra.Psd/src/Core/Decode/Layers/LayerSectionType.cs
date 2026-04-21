namespace Lyra.Psd.Core.Decode.Layers;

/// <summary>
/// Section divider type from the 'lsct' additional layer information.
/// PSD stores layer hierarchy as a flat list of records with group dividers.
/// </summary>
public enum LayerSectionType
{
    /// <summary>Regular layer; not a group boundary.</summary>
    Normal = 0,

    /// <summary>Expanded group header.</summary>
    OpenGroup = 1,

    /// <summary>Collapsed group header.</summary>
    ClosedGroup = 2,

    /// <summary>
    /// Bounding section divider. Marks the bottom of a group's contents in file order.
    /// Hidden in the Photoshop UI but required to reconstruct the hierarchy.
    /// </summary>
    BoundingDivider = 3
}