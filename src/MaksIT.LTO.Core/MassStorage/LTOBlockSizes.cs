namespace MaksIT.LTO.Core.MassStorage;

public static class LTOBlockSizes {
  public const uint LTO1 = 65536;    // 64 KB
  public const uint LTO2 = 65536;    // 64 KB
  public const uint LTO3 = 131072;   // 128 KB
  public const uint LTO4 = 131072;   // 128 KB
  public const uint LTO5 = 262144;   // 256 KB
  public const uint LTO6 = 262144;   // 256 KB
  public const uint LTO7 = 524288;   // 512 KB
  public const uint LTO8 = 524288;   // 512 KB
  public const uint LTO9 = 1048576;  // 1 MB

  private static readonly Dictionary<string, ulong> TapeCapacities = new() {
    ["LTO1"] = 100UL * 1024 * 1024 * 1024,
    ["LTO2"] = 200UL * 1024 * 1024 * 1024,
    ["LTO3"] = 400UL * 1024 * 1024 * 1024,
    ["LTO4"] = 800UL * 1024 * 1024 * 1024,
    ["LTO5"] = 1500UL * 1024 * 1024 * 1024,
    ["LTO6"] = 2500UL * 1024 * 1024 * 1024,
    ["LTO7"] = 6000UL * 1024 * 1024 * 1024,
    ["LTO8"] = 12000UL * 1024 * 1024 * 1024,
    ["LTO9"] = 18000UL * 1024 * 1024 * 1024
  };

  public static uint GetBlockSize(string ltoGen) =>
    ltoGen switch {
      "LTO1" => LTO1,
      "LTO2" => LTO2,
      "LTO3" => LTO3,
      "LTO4" => LTO4,
      "LTO5" => LTO5,
      "LTO6" => LTO6,
      "LTO7" => LTO7,
      "LTO8" => LTO8,
      "LTO9" => LTO9,
      _ => throw new ArgumentException("Invalid LTO generation")
    };

  public static ulong GetTapeCapacity(string ltoGen) {
    if (TapeCapacities.TryGetValue(ltoGen, out var capacity))
      return capacity;

    throw new ArgumentException("Invalid LTO generation");
  }

  public static ulong GetMaxBlocks(string ltoGen) {
    var blockSize = GetBlockSize(ltoGen);
    var tapeCapacity = GetTapeCapacity(ltoGen);
    return tapeCapacity / blockSize;
  }
}
