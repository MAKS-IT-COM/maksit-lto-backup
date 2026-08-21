using MaksIT.Core.Abstractions;


namespace MaksIT.LTO.Backup.Shared.Models;

public sealed class DeviceModes : Enumeration {
  public static readonly DeviceModes Physical = new(1, "Physical");
  public static readonly DeviceModes Emulated = new(2, "Emulated");

  private DeviceModes(int id, string name) : base(id, name) { }

  public static bool Matches(string? value, DeviceModes mode) =>
    string.Equals(value, mode.Name, StringComparison.OrdinalIgnoreCase);
}
