namespace MaksIT.LTO.Backup.Shared.Models;

public abstract class PathBase {
  public required string Path { get; set; }
}

public class LocalPath : PathBase { }

public class PasswordCredentials {
  public required string Username { get; set; }
  public required string Password { get; set; }
}

public class RemotePath : PathBase {
  public PasswordCredentials? PasswordCredentials { get; set; }
  public required string Protocol { get; set; }
}

public class WorkingFolder {
  public LocalPath? LocalPath { get; set; }
  public RemotePath? RemotePath { get; set; }
}

/// <summary>
/// UScheduler-compatible calendar (empty month/weekday = all; times are UTC HH:mm).
/// </summary>
public class BackupSchedule {
  public List<string> RunMonth { get; set; } = [];
  public List<string> RunWeekday { get; set; } = [];
  public List<string> RunTime { get; set; } = [];
  public int MinIntervalMinutes { get; set; } = 10;
}

/// <summary>
/// Cartridge associated with a library backup job for rotation by last-used time.
/// </summary>
public class BackupTape {
  public required string Barcode { get; set; }
  public DateTimeOffset? LastUsedUtc { get; set; }
}

/// <summary>
/// Ordered wave of backup jobs sharing one schedule (Worker runs aggregations, not per-job calendars).
/// </summary>
public class BackupAggregation {
  public required string Name { get; set; }
  public List<string> JobNames { get; set; } = [];
  public BackupSchedule? Schedule { get; set; }
  public bool Disabled { get; set; }
  public DateTimeOffset? LastRunUtc { get; set; }
}

public class BackupItem {
  public required string Name { get; set; }
  /// <summary>
  /// Standalone / single-tape MAM label. Library jobs use <see cref="Tapes"/>.
  /// </summary>
  public string Barcode { get; set; } = string.Empty;
  public required WorkingFolder Source { get; set; }
  public required WorkingFolder Destination { get; set; }
  public required string LTOGen { get; set; }
  /// <summary>
  /// Overwrite replaces the cartridge from BOT. Append adds another FM-separated set.
  /// </summary>
  public string WriteMode { get; set; } = BackupWriteModes.Overwrite.Name;
  /// <summary>
  /// Library media pool (oldest <see cref="BackupTape.LastUsedUtc"/> first).
  /// </summary>
  public List<BackupTape> Tapes { get; set; } = [];
  /// <summary>
  /// When true, aggregations skip this job.
  /// </summary>
  public bool Disabled { get; set; }
}

public class EmulatorLibraryOptions {
  public int SlotCount { get; set; } = 24;
  public int DriveCount { get; set; } = 2;
  public int IePortCount { get; set; } = 1;
}

public class EmulatorOptions {
  public string DataDirectory { get; set; } = ".\\emulator-data";
  public EmulatorLibraryOptions Library { get; set; } = new();
}

public class ServiceOptions {
  public string ServiceName { get; set; } = "MaksIT.LTO.Backup";
  public string Description { get; set; } = "Schedules LTO library backup aggregations using barcode media pools.";
  /// <summary>
  /// Optional path to the Worker executable for UI lifecycle control. Empty = resolve next to UI / default name.
  /// </summary>
  public string ExecutablePath { get; set; } = string.Empty;
}

public class Configuration {
  public string DeviceMode { get; set; } = DeviceModes.Physical.Name;
  /// <summary>
  /// StandaloneDrive = tape drive only. TapeLibrary = changer inventory/move + drive bays.
  /// </summary>
  public string Topology { get; set; } = DeviceTopologies.StandaloneDrive.Name;
  public string TapePath { get; set; } = "\\\\.\\Tape0";
  public string LibraryPath { get; set; } = "\\\\.\\Changer0";
  public int WriteDelay { get; set; } = 100;
  /// <summary>
  /// When true, restore aborts on the first file checksum mismatch instead of only logging a warning.
  /// </summary>
  public bool FailFastOnChecksumMismatch { get; set; } = true;
  public List<BackupItem> Backups { get; set; } = [];
  public List<BackupAggregation> Aggregations { get; set; } = [];
  public EmulatorOptions Emulator { get; set; } = new();
  public ServiceOptions Service { get; set; } = new();

  public bool IsEmulated =>
    DeviceModes.Matches(DeviceMode, DeviceModes.Emulated);

  public bool IsTapeLibrary =>
    DeviceTopologies.Matches(Topology, DeviceTopologies.TapeLibrary);

  public bool IsStandaloneDrive =>
    DeviceTopologies.Matches(Topology, DeviceTopologies.StandaloneDrive);
}
