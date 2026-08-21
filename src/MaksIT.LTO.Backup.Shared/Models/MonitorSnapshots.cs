using MaksIT.LTO.Core.MassStorage;


namespace MaksIT.LTO.Backup.Shared.Models;

public sealed class DriveStatusSnapshot {
  public required string DeviceMode { get; init; }
  public required string Topology { get; init; }
  public required string TapePath { get; init; }
  public required string DevicePath { get; init; }
  public int StatusCode { get; init; }
  public required string State { get; init; }
  public uint? AbsoluteBlock { get; init; }
  public string? Error { get; init; }
  public DateTime QueriedAt { get; init; } = DateTime.Now;
}

public sealed class LibraryStatusSnapshot {
  public required string DeviceMode { get; init; }
  public required string Topology { get; init; }
  public required string LibraryPath { get; init; }
  public required string DevicePath { get; init; }
  public bool Available { get; init; }
  public IReadOnlyList<LibraryElementStatus> Elements { get; init; } = [];
  public int TotalElements => Elements.Count;
  public int OccupiedCount => Elements.Count(e => !string.IsNullOrWhiteSpace(e.CartridgeId));
  public int DriveCount => Elements.Count(e => string.Equals(e.ElementType, "Drive", StringComparison.OrdinalIgnoreCase));
  public int OccupiedDrives => Elements.Count(e =>
    string.Equals(e.ElementType, "Drive", StringComparison.OrdinalIgnoreCase)
    && !string.IsNullOrWhiteSpace(e.CartridgeId));
  public string? Error { get; init; }
  public DateTime QueriedAt { get; init; } = DateTime.Now;
}

public sealed class LoadedCartridgeSnapshot {
  public bool IsLoaded { get; init; }
  public required string Topology { get; init; }
  public string? Summary { get; init; }
  public string? CartridgeId { get; init; }
  public string? Barcode { get; init; }
  public int? DriveSlot { get; init; }
  public string? DevicePath { get; init; }
  public string? Hint { get; init; }
}
