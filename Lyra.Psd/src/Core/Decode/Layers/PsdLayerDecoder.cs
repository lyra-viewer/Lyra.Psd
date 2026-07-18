using Lyra.Psd.Core.Readers;
using Lyra.Psd.Core.SectionData;
using Lyra.Psd.Core.SectionReaders;

namespace Lyra.Psd.Core.Decode.Layers;

public static class PsdLayerDecoder
{
    /// <summary>
    /// Decode layer records from the LayerInfo payload.
    /// Resolves each layer's name via 'luni' (Unicode) with Pascal string fallback,
    /// and extracts section-divider type ('lsct'), visibility, opacity, and blend mode.
    /// </summary>
    public static LayerRecord[] DecodeLayerRecords(PsdDocument psdDocument, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(psdDocument);
        ArgumentNullException.ThrowIfNull(stream);

        var header = psdDocument.FileHeader;
        var layerInfo = psdDocument.LayerAndMaskInformation.LayerInfo;

        if (layerInfo.PayloadLength == 0 || layerInfo.LayerCount == 0)
            return [];

        if (!stream.CanSeek)
            throw new NotSupportedException("Layer decoding requires a seekable stream.");

        var isPsb = header.Version == 2;
        var reader = new PsdBigEndianReader(stream)
        {
            Position = layerInfo.PayloadOffset
        };

        var payloadEnd = checked(layerInfo.PayloadOffset + layerInfo.PayloadLength);

        var count = Math.Abs(reader.ReadInt16());
        if (count == 0)
            return [];

        // Smallest possible layer record: bounds(16) + channel count(2) + blend block(12) +
        // extra-data length(4) = 34 bytes. A count the payload cannot hold means corruption.
        const int minRecordBytes = 34;
        if ((long)count * minRecordBytes > layerInfo.PayloadLength)
            throw new InvalidDataException($"Layer count {count} exceeds what the {layerInfo.PayloadLength}-byte LayerInfo payload can hold.");

        var channelEntrySize = 2 + (isPsb ? 8 : 4);
        var records = new LayerRecord[count];

        for (var i = 0; i < count; i++)
        {
            var top = reader.ReadInt32();
            var left = reader.ReadInt32();
            var bottom = reader.ReadInt32();
            var right = reader.ReadInt32();

            var channelCount = reader.ReadUInt16();
            reader.Skip(channelCount * channelEntrySize);

            // 4 (blend sig '8BIM') + 4 (blend key) + 1 (opacity) + 1 (clipping) + 1 (flags) + 1 (filler) = 12 bytes
            reader.Skip(4); // blend mode signature
            var blendModeKey = reader.ReadUInt32();
            var opacity = reader.ReadByte();
            reader.Skip(1); // clipping
            var flags = reader.ReadByte();
            reader.Skip(1); // filler

            // Flags bit 1: 1 = hidden, 0 = visible (inverted per spec).
            var visible = (flags & 0x02) == 0;

            var extraDataLength = reader.ReadUInt32();
            var extraDataEnd = reader.Position + extraDataLength;

            if (extraDataEnd > payloadEnd)
                throw new InvalidDataException($"Layer {i}: extra data ({extraDataLength} bytes) overruns the LayerInfo payload.");

            SkipLayerMaskData(reader);
            SkipLayerBlendingRanges(reader);

            var pascalName = reader.ReadPascalString(padTo: 4);
            var (unicodeName, sectionType) = ReadAdditionalFields(reader, extraDataEnd, isPsb);

            if (reader.Position != extraDataEnd)
                reader.Position = extraDataEnd;

            records[i] = new LayerRecord(
                top, left, bottom, right,
                unicodeName ?? pascalName,
                sectionType,
                visible, opacity, blendModeKey);
        }

        return records;
    }

    /// <summary>
    /// Single pass over the per-layer additional information, extracting
    /// the Unicode name ('luni') and the section divider type ('lsct').
    /// Missing blocks yield null / Normal respectively.
    /// </summary>
    private static (string? UnicodeName, LayerSectionType SectionType) ReadAdditionalFields(
        PsdBigEndianReader reader, long extraDataEnd, bool isPsb)
    {
        if (reader.Position >= extraDataEnd)
            return (null, LayerSectionType.Normal);

        var blocks = AdditionalLayerInformationReader.ReadAll(reader, extraDataEnd, isPsb);

        string? unicodeName = null;
        var sectionType = LayerSectionType.Normal;

        foreach (var block in blocks)
        {
            if (block.Key == AdditionalLayerInformationKeys.UnicodeLayerName)
            {
                reader.Position = block.PayloadOffset;
                unicodeName = reader.ReadUnicodeString();
            }
            else if (block.Key == AdditionalLayerInformationKeys.SectionDivider)
            {
                reader.Position = block.PayloadOffset;
                sectionType = ReadSectionDividerType(reader, block.PayloadLength);
            }
        }

        return (unicodeName, sectionType);
    }

    /// <summary>
    /// 'lsct' payload layout:
    ///   4 bytes: type (0..3)
    ///   [optional, if length >= 12]  4 bytes signature + 4 bytes blend mode key (pass-through etc.)
    ///   [optional, if length >= 16]  4 bytes subtype (0 = normal, 1 = scene group)
    /// </summary>
    private static LayerSectionType ReadSectionDividerType(PsdBigEndianReader reader, long payloadLength)
    {
        if (payloadLength < 4)
            return LayerSectionType.Normal;

        var type = reader.ReadUInt32();
        return type <= 3 ? (LayerSectionType)type : LayerSectionType.Normal;
    }

    private static void SkipLayerMaskData(PsdBigEndianReader reader)
    {
        var length = reader.ReadUInt32();
        if (length > 0)
            reader.Skip(length);
    }

    private static void SkipLayerBlendingRanges(PsdBigEndianReader reader)
    {
        var length = reader.ReadUInt32();
        if (length > 0)
            reader.Skip(length);
    }
}