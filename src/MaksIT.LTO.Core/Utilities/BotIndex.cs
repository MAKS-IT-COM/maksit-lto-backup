using System.Buffers.Binary;
using System.Text;


namespace MaksIT.LTO.Core.Utilities;

public static class BotIndex {
  public sealed class SetEntry {
    public uint PayloadBaseBlock { get; init; }

    public uint DescriptorAbsoluteBlock { get; init; }

    public uint DescriptorBlockCount { get; init; }

    public required byte[] ContentHash { get; init; }

    public required string BackupName { get; init; }
  }

  public sealed class Index {
    public ushort LayoutVersionValue { get; init; } = 1;

    public int SchemaVersion { get; init; }

    public uint BlockSize { get; init; }

    public List<SetEntry> Sets { get; init; } = new List<SetEntry>();
  }

  public const string Magic = "MLTI";

  public const ushort LayoutVersion = 1;

  public const int MaxSets = 16;

  public const int HeaderSize = 16;

  public const int SetSlotSize = 96;

  public const int ContentHashBytes = 32;

  public const int BackupNameBytes = 48;

  public static byte[] Create(Index index, int blockSize) {
    ArgumentOutOfRangeException.ThrowIfLessThan(blockSize, 112, "blockSize");
    if (index.Sets.Count > 16) {
      throw new InvalidOperationException($"BOT index supports at most {16} sets.");
    }
    byte[] array = new byte[blockSize];
    Encoding.ASCII.GetBytes("MLTI").CopyTo(array.AsSpan(0, 4));
    BinaryPrimitives.WriteUInt16LittleEndian(array.AsSpan(4, 2), 1);
    BinaryPrimitives.WriteUInt16LittleEndian(array.AsSpan(6, 2), (ushort)index.SchemaVersion);
    BinaryPrimitives.WriteUInt32LittleEndian(array.AsSpan(8, 4), index.BlockSize);
    array[12] = (byte)index.Sets.Count;
    for (int i = 0; i < index.Sets.Count; i++) {
      WriteSet(array.AsSpan(16 + i * 96, 96), index.Sets[i]);
    }
    return array;
  }

  public static bool TryParse(ReadOnlySpan<byte> block, out Index? index, out string? error) {
    index = null;
    error = null;
    if (block.Length < 16) {
      error = "BOT index block is too short.";
      return false;
    }
    string text = Encoding.ASCII.GetString(block.Slice(0, 4));
    if (!string.Equals(text, "MLTI", StringComparison.Ordinal)) {
      error = "Not a BOT index (magic '" + text + "').";
      return false;
    }
    ushort num = BinaryPrimitives.ReadUInt16LittleEndian(block.Slice(4, 2));
    if (num != 1) {
      error = $"Unsupported BOT index layout version {num}.";
      return false;
    }
    ushort schemaVersion = BinaryPrimitives.ReadUInt16LittleEndian(block.Slice(6, 2));
    uint blockSize = BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(8, 4));
    byte b = block[12];
    if (b > 16) {
      error = $"BOT index set count {b} exceeds max {16}.";
      return false;
    }
    if (block.Length < 16 + b * 96) {
      error = "BOT index truncated.";
      return false;
    }
    List<SetEntry> list = new List<SetEntry>(b);
    for (int i = 0; i < b; i++) {
      list.Add(ReadSet(block.Slice(16 + i * 96, 96)));
    }
    index = new Index {
      LayoutVersionValue = num,
      SchemaVersion = schemaVersion,
      BlockSize = blockSize,
      Sets = list
    };
    return true;
  }

  public static byte[] ContentHashFromHex(string hex) {
    if (hex.Length != 64) {
      throw new ArgumentException("ContentHash must be 64 hex characters.", "hex");
    }
    return Convert.FromHexString(hex);
  }

  public static string ContentHashToHex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

  private static void WriteSet(Span<byte> slot, SetEntry entry) {
    slot.Clear();
    BinaryPrimitives.WriteUInt32LittleEndian(slot.Slice(0, 4), entry.PayloadBaseBlock);
    BinaryPrimitives.WriteUInt32LittleEndian(slot.Slice(4, 4), entry.DescriptorAbsoluteBlock);
    BinaryPrimitives.WriteUInt32LittleEndian(slot.Slice(8, 4), entry.DescriptorBlockCount);
    entry.ContentHash.AsSpan(0, Math.Min(32, entry.ContentHash.Length)).CopyTo(slot.Slice(12, 32));
    byte[] bytes = Encoding.ASCII.GetBytes(entry.BackupName ?? string.Empty);
    bytes.AsSpan(0, Math.Min(48, bytes.Length)).CopyTo(slot.Slice(44, 48));
  }

  private static SetEntry ReadSet(ReadOnlySpan<byte> slot) {
    byte[] contentHash = slot.Slice(12, 32).ToArray();
    ReadOnlySpan<byte> span = slot.Slice(44, 48);
    int num = span.IndexOf((byte)0);
    if (num < 0) {
      num = 48;
    }
    return new SetEntry {
      PayloadBaseBlock = BinaryPrimitives.ReadUInt32LittleEndian(slot.Slice(0, 4)),
      DescriptorAbsoluteBlock = BinaryPrimitives.ReadUInt32LittleEndian(slot.Slice(4, 4)),
      DescriptorBlockCount = BinaryPrimitives.ReadUInt32LittleEndian(slot.Slice(8, 4)),
      ContentHash = contentHash,
      BackupName = Encoding.ASCII.GetString(span.Slice(0, num))
    };
  }
}
