using System.Runtime.CompilerServices;
using System.Text;
using MaksIT.LTO.Core.MassStorage;


namespace MaksIT.LTO.Core.Utilities;

public static class MamBackupSummary {
  public const string ApplicationName = "MaksITLTO";

  public const string FormatVersion = "MLTO/2";

  public static void Write(ITapeDrive drive, string backupName, DateTime createdUtc, string? ltoGen, int fileCount, uint blockSize, string? contentHash) {
    WriteAscii(drive, 2049, "MaksITLTO", 32);
    WriteAscii(drive, 2059, "MLTO/2", 16);
    WriteAscii(drive, 2055, Truncate(Environment.MachineName, 80), 80);
    InlineArray6<string> buffer = default(InlineArray6<string>);
    buffer[0] = Truncate(backupName, 40);
    buffer[1] = createdUtc.ToString("yyyyMMddHHmm");
    buffer[2] = ltoGen ?? "?";
    buffer[3] = $"n={fileCount}";
    buffer[4] = $"bs={blockSize}";
    buffer[5] = Truncate(contentHash ?? string.Empty, 32);
    string value = string.Join(' ', (ReadOnlySpan<string?>)buffer);
    WriteAscii(drive, 2051, Truncate(value, 160), 160);
    string text = createdUtc.ToString("yyyyMMddHHmm");
    WriteAscii(drive, 2052, (text.Length <= 12) ? text : text.Substring(0, 12), 12);
  }

  private static void WriteAscii(ITapeDrive drive, ushort address, string value, int length) {
    LTOCmAttribute lTOCmAttribute = LTOCmKnownAttributes.GetAttributeByAddress(address) ?? new LTOCmAttribute {
      Address = address,
      Name = $"0x{address:X4}",
      Format = LTOCmAttributeFormat.ASCII,
      Length = length,
      Writable = true
    };
    byte[] array = new byte[length];
    byte[] bytes = Encoding.ASCII.GetBytes(value);
    Array.Copy(bytes, array, Math.Min(bytes.Length, length));
    lTOCmAttribute.Value = array;
    lTOCmAttribute.Length = length;
    drive.WriteCartridgeAttribute(lTOCmAttribute);
  }

  private static string Truncate(string value, int max) => (value.Length <= max) ? value : value.Substring(0, max);
}
