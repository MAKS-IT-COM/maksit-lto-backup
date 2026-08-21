using MaksIT.Core.Abstractions;


namespace MaksIT.LTO.Backup.Shared.Models;

public sealed class DeviceTopologies : Enumeration {
  public static readonly DeviceTopologies StandaloneDrive = new(1, "StandaloneDrive");
  public static readonly DeviceTopologies TapeLibrary = new(2, "TapeLibrary");

  private DeviceTopologies(int id, string name) : base(id, name) { }

  public static bool Matches(string? value, DeviceTopologies topology) =>
    string.Equals(value, topology.Name, StringComparison.OrdinalIgnoreCase);
}
