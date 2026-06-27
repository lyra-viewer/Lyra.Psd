namespace Lyra.Psd.Core.Common;

/// <summary>
/// Canonical names for the Photoshop image-resource block IDs (Adobe's documented resource set).
/// </summary>
public static class PsdImageResourceNames
{
    /// <summary>Returns a human-readable name for an image-resource block ID.</summary>
    public static string GetName(ushort id)
    {
        if (id is >= 2000 and <= 2997)
            return $"Path #{id}";

        return id switch
        {
            1005 => "Resolution Info",
            1006 => "Alpha Channel Names",
            1008 => "Caption",
            1010 => "Background Color",
            1011 => "Print Flags",
            1013 => "Color Halftoning",
            1016 => "Color Transfer Func",
            1024 => "Layer State",
            1026 => "Layer Groups",
            1032 => "Grid & Guides",
            1033 => "Thumbnail (PS4)",
            1036 => "Thumbnail",
            1037 => "Global Angle",
            1039 => "ICC Profile",
            1041 => "ICC Untagged",
            1044 => "Document IDs Seed",
            1045 => "Alpha Unicode Names",
            1049 => "Global Altitude",
            1050 => "Slices",
            1053 => "Alpha Identifiers",
            1054 => "URL List",
            1057 => "Version Info",
            1058 => "EXIF Data 1",
            1059 => "EXIF Data 3",
            1060 => "XMP Metadata",
            1061 => "Caption Digest",
            1062 => "Print Scale",
            1064 => "Pixel Aspect Ratio",
            1065 => "Layer Comps",
            1069 => "Layer Selection IDs",
            1077 => "Display Info",
            1080 => "Print Info",
            1082 => "Print Style",
            2999 => "Clipping Path Name",
            7000 => "Image Ready Vars",
            8000 => "Lightroom Workflow",
            10000 => "Print Flags Info",
            _ => $"Resource #{id}",
        };
    }
}