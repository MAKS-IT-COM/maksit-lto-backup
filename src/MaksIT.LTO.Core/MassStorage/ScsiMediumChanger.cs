namespace MaksIT.LTO.Core.MassStorage;


/// <summary>SCSI medium-changer CDBs shared by Windows and Linux transports.</summary>
public static class ScsiMediumChanger {
  public const byte ReadElementStatusOpcode = 0xB8;
  public const byte MoveMediumOpcode = 0xA5;
  public const byte InitializeElementStatusOpcode = 0x07;

  public static byte[] BuildInitializeElementStatusCdb() {
    var cdb = new byte[6];
    cdb[0] = InitializeElementStatusOpcode;
    return cdb;
  }

  public static byte[] BuildMoveMediumCdb(ushort transport, ushort source, ushort destination, bool invert = false) {
    var cdb = new byte[12];
    cdb[0] = MoveMediumOpcode;
    cdb[2] = (byte)(transport >> 8);
    cdb[3] = (byte)(transport & 0xFF);
    cdb[4] = (byte)(source >> 8);
    cdb[5] = (byte)(source & 0xFF);
    cdb[6] = (byte)(destination >> 8);
    cdb[7] = (byte)(destination & 0xFF);
    if (invert)
      cdb[10] = 0x01;

    return cdb;
  }

  public static byte[] BuildReadElementStatusCdb(
    byte elementType,
    ushort startingElement,
    ushort numberOfElements,
    uint allocationLength,
    bool volumeTag = true
  ) {
    var cdb = new byte[12];
    cdb[0] = ReadElementStatusOpcode;
    cdb[1] = (byte)((volumeTag ? 0x10 : 0x00) | (elementType & 0x0F));
    cdb[2] = (byte)(startingElement >> 8);
    cdb[3] = (byte)(startingElement & 0xFF);
    cdb[4] = (byte)(numberOfElements >> 8);
    cdb[5] = (byte)(numberOfElements & 0xFF);
    cdb[7] = (byte)((allocationLength >> 16) & 0xFF);
    cdb[8] = (byte)((allocationLength >> 8) & 0xFF);
    cdb[9] = (byte)(allocationLength & 0xFF);
    return cdb;
  }

  public static IReadOnlyList<LibraryElementStatus> ParseElementStatusData(byte[] data, string defaultType) {
    if (data.Length < 8)
      return [];

    var numElements = (ushort)((data[2] << 8) | data[3]);
    var byteCount = (data[5] << 16) | (data[6] << 8) | data[7];
    if (byteCount <= 0 || 8 + byteCount > data.Length)
      byteCount = Math.Max(0, data.Length - 8);

    List<LibraryElementStatus> results = [];
    var offset = 8;
    var remaining = byteCount;
    var parsed = 0;

    while (remaining >= 4 && parsed < numElements && offset + 4 <= data.Length) {
      var pageLength = (data[offset + 2] << 8) | data[offset + 3];
      var descriptorLength = 4 + pageLength;
      if (pageLength < 0 || offset + descriptorLength > data.Length)
        break;

      var elementAddress = offset + 4 + 1 <= data.Length
        ? (ushort)((data[offset + 4] << 8) | data[offset + 5])
        : (ushort)0;
      var flags = offset + 6 < data.Length ? data[offset + 6] : (byte)0;
      var full = (flags & 0x01) != 0;
      string? barcode = null;

      if (full && pageLength >= 48) {
        var tagOffset = offset + 12;
        if (tagOffset + 32 <= offset + descriptorLength) {
          barcode = System.Text.Encoding.ASCII.GetString(data, tagOffset, 32).Trim('\0', ' ');
          if (string.IsNullOrWhiteSpace(barcode))
            barcode = null;
        }
      }

      results.Add(new LibraryElementStatus {
        Slot = elementAddress,
        ElementType = defaultType,
        Barcode = barcode,
        CartridgeId = full ? barcode ?? $"media-{elementAddress}" : null
      });

      offset += descriptorLength;
      remaining -= descriptorLength;
      parsed++;
    }

    return results;
  }
}
