using MaksIT.Core.Abstractions;


namespace MaksIT.LTO.Backup.Shared.Models;

public sealed class BackupWriteModes : Enumeration {
  public static readonly BackupWriteModes Overwrite = new(1, "Overwrite");
  public static readonly BackupWriteModes Append = new(2, "Append");

  private BackupWriteModes(int id, string name) : base(id, name) { }

  public static bool Matches(string? value, BackupWriteModes mode) =>
    string.Equals(value, mode.Name, StringComparison.OrdinalIgnoreCase);
}
