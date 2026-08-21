namespace MaksIT.LTO.Core.MassStorage;


/// <summary>Shared LTO cartridge-memory attribute encode/decode (SCSI READ/WRITE ATTRIBUTE payloads).</summary>
public static class MamAttributeCodec {
  public static List<LTOCmAttribute> ParseAttributes(byte[] buffer) {
    var tempbuffer = new byte[4];
    Array.Copy(buffer, 0, tempbuffer, 0, 4);
    if (BitConverter.IsLittleEndian)
      Array.Reverse(tempbuffer);

    var availableData = BitConverter.ToInt32(tempbuffer, 0);
    if (availableData < 0 || availableData + 4 > buffer.Length)
      throw new InvalidOperationException($"Invalid MAM available-data length: {availableData}.");

    var attributeBuffer = new byte[availableData];
    Array.Copy(buffer, 4, attributeBuffer, 0, availableData);

    List<LTOCmAttribute> attributes = [];
    var offset = 0;
    const int attributeHeader = 5;

    while (offset < attributeBuffer.Length) {
      var attrId = (ushort)((attributeBuffer[offset] << 8) | attributeBuffer[offset + 1]);
      var length = (ushort)((attributeBuffer[offset + 3] << 8) | attributeBuffer[offset + 4]);

      if (offset + attributeHeader + length > attributeBuffer.Length)
        break;

      var knownAttr = LTOCmKnownAttributes.GetAttributeByAddress(attrId) ?? new LTOCmAttribute {
        Name = $"Unknown (0x{attrId:X4})",
        Writable = false,
        Format = LTOCmAttributeFormat.Unknown,
        Length = -1
      };

      knownAttr.Address = attrId;
      knownAttr.Value = new byte[length];
      Array.Copy(attributeBuffer, offset + attributeHeader, knownAttr.Value, 0, length);

      if (knownAttr.Length != length && knownAttr.Format != LTOCmAttributeFormat.Unknown)
        throw new ArgumentException($"Expected length differs from medium for 0x{attrId:X4}: {knownAttr.Length} vs {length}");

      attributes.Add(knownAttr);
      offset += length + attributeHeader;
    }

    attributes.Sort((x, y) => x.Address.CompareTo(y.Address));
    return attributes;
  }

  public static byte[] BuildReadAttributeCdb(uint allocationLength) {
    var cdb = new byte[16];
    cdb[0] = 0x8C;
    cdb[10] = (byte)((allocationLength >> 16) & 0xFF);
    cdb[11] = (byte)((allocationLength >> 8) & 0xFF);
    cdb[12] = (byte)(allocationLength & 0xFF);
    return cdb;
  }

  public static byte[] BuildWriteAttributeCdb(uint parameterListLength) {
    var cdb = new byte[16];
    cdb[0] = 0x8D;
    cdb[1] = 0x01;
    cdb[10] = (byte)((parameterListLength >> 24) & 0xFF);
    cdb[11] = (byte)((parameterListLength >> 16) & 0xFF);
    cdb[12] = (byte)((parameterListLength >> 8) & 0xFF);
    cdb[13] = (byte)(parameterListLength & 0xFF);
    return cdb;
  }

  public static byte[] BuildWriteAttributeParameterList(LTOCmAttribute attribute) {
    var knownAttr = LTOCmKnownAttributes.GetAttributeByAddress(attribute.Address)
      ?? throw new ArgumentException("Unknown attribute.");

    if (!knownAttr.Writable)
      throw new ArgumentException($"Attribute {knownAttr.Name} is not writable.");

    if (attribute.Value == null)
      throw new ArgumentException("Attribute value cannot be null.");

    var length = attribute.Length > 0 ? attribute.Length : attribute.Value.Length;
    var buffer = new byte[10 + length];
    var parameterDataLength = buffer.Length - 4;
    buffer[0] = (byte)((parameterDataLength >> 24) & 0xFF);
    buffer[1] = (byte)((parameterDataLength >> 16) & 0xFF);
    buffer[2] = (byte)((parameterDataLength >> 8) & 0xFF);
    buffer[3] = (byte)(parameterDataLength & 0xFF);
    buffer[4] = (byte)(attribute.Address >> 8);
    buffer[5] = (byte)(attribute.Address & 0xFF);
    buffer[6] = 0x00;
    buffer[7] = attribute.Format switch {
      LTOCmAttributeFormat.Binary => 0x00,
      LTOCmAttributeFormat.ASCII => 0x01,
      LTOCmAttributeFormat.Text => 0x10,
      _ => throw new ArgumentException("Unknown attribute format")
    };
    buffer[8] = (byte)(length >> 8);
    buffer[9] = (byte)(length & 0xFF);
    Array.Copy(attribute.Value, 0, buffer, 10, length);
    return buffer;
  }
}
