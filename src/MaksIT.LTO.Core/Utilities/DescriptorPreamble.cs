using System.Buffers.Binary;
using System.Text;


namespace MaksIT.LTO.Core.Utilities;

public static class DescriptorPreamble {
  public const string Magic = "MLTD";

  public const ushort CurrentVersion = 1;

  public const int LayoutSize = 20;

  public static byte[] Create(uint descriptorBlockCount, int ciphertextLength, int blockSize) {
    ArgumentOutOfRangeException.ThrowIfLessThan(blockSize, 20, "blockSize");
    ArgumentOutOfRangeException.ThrowIfNegative(ciphertextLength, "ciphertextLength");
    byte[] array = new byte[blockSize];
    Encoding.ASCII.GetBytes("MLTD").CopyTo(array.AsSpan(0, 4));
    BinaryPrimitives.WriteUInt16LittleEndian(array.AsSpan(4, 2), 1);
    BinaryPrimitives.WriteUInt32LittleEndian(array.AsSpan(6, 4), descriptorBlockCount);
    BinaryPrimitives.WriteUInt32LittleEndian(array.AsSpan(10, 4), (uint)ciphertextLength);
    BinaryPrimitives.WriteUInt32LittleEndian(array.AsSpan(14, 4), 0u);
    return array;
  }

  public static bool TryParse(ReadOnlySpan<byte> block, out uint descriptorBlockCount, out int ciphertextLength, out string? error) {
    descriptorBlockCount = 0u;
    ciphertextLength = 0;
    error = null;
    if (block.Length < 20) {
      error = "Preamble block is too short.";
      return false;
    }
    string text = Encoding.ASCII.GetString(block.Slice(0, 4));
    if (!string.Equals(text, "MLTD", StringComparison.Ordinal)) {
      error = "Unexpected descriptor preamble magic '" + text + "'.";
      return false;
    }
    ushort num = BinaryPrimitives.ReadUInt16LittleEndian(block.Slice(4, 2));
    if (num != 1) {
      error = $"Unsupported descriptor preamble version {num}.";
      return false;
    }
    descriptorBlockCount = BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(6, 4));
    ciphertextLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(10, 4));
    if (descriptorBlockCount == 0) {
      error = "DescriptorBlockCount in preamble is zero.";
      return false;
    }
    if (ciphertextLength < 0) {
      error = "Invalid ciphertext length in preamble.";
      return false;
    }
    return true;
  }
}
