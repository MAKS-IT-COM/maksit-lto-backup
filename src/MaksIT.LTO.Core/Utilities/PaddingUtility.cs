using System.Buffers.Binary;


namespace MaksIT.LTO.Core.Utilities;

public static class PaddingUtility {
  public static byte[] AddPadding(byte[] data, int blockSize) {
    ArgumentNullException.ThrowIfNull(data, "data");
    ArgumentOutOfRangeException.ThrowIfLessThan(blockSize, 1, "blockSize");
    int num = 4 + data.Length;
    int num2 = (num + blockSize - 1) / blockSize * blockSize;
    byte[] array = new byte[num2];
    BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(0, 4), data.Length);
    Array.Copy(data, 0, array, 4, data.Length);
    return array;
  }

  public static byte[] RemovePadding(byte[] paddedData, int blockSize) {
    ArgumentNullException.ThrowIfNull(paddedData, "paddedData");
    if (paddedData.Length < 4) {
      throw new ArgumentException("Padded buffer is too short to contain a length prefix.", "paddedData");
    }
    int num = BinaryPrimitives.ReadInt32LittleEndian(paddedData.AsSpan(0, 4));
    if (num < 0 || num + 4 > paddedData.Length) {
      throw new ArgumentException("Invalid length prefix in padded buffer.", "paddedData");
    }
    byte[] array = new byte[num];
    Array.Copy(paddedData, 4, array, 0, num);
    return array;
  }
}
