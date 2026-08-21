using MaksIT.Core.Abstractions;


namespace MaksIT.LTO.Backup.Shared.Models;

public sealed class FileHashAlgorithms : Enumeration {
  public static readonly FileHashAlgorithms Sha256 = new(1, "SHA256");
  public static readonly FileHashAlgorithms Crc32 = new(2, "CRC32");

  private FileHashAlgorithms(int id, string name) : base(id, name) { }

  public static bool Matches(string? value, FileHashAlgorithms algorithm) =>
    string.Equals(value, algorithm.Name, StringComparison.OrdinalIgnoreCase);
}
