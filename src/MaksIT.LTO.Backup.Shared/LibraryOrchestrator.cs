using MaksIT.LTO.Backup.Shared.Models;
using MaksIT.LTO.Core.MassStorage;
using Microsoft.Extensions.Logging;


namespace MaksIT.LTO.Backup.Shared;

public sealed class LibraryOrchestrator(ConfigurationFileService configurationFileService, ILoggerFactory loggerFactory) {
  private Configuration Configuration => configurationFileService.Current;

  public bool IsAvailable => Configuration.IsTapeLibrary;

  private void EnsureLibraryTopology() {
    if (!Configuration.IsTapeLibrary) {
      throw new InvalidOperationException("Library operations require Topology = TapeLibrary. StandaloneDrive mode uses the tape drive only.");
    }
  }

  private ITapeLibrary CreateLibrary() {
    EnsureLibraryTopology();
    if (Configuration.IsEmulated) {
      return new EmulatedTapeLibrary(Path.GetFullPath(Configuration.Emulator.DataDirectory), Configuration.Emulator.Library.SlotCount, Configuration.Emulator.Library.DriveCount);
    }
    if (OperatingSystem.IsLinux()) {
      return new LinuxTapeLibrary(loggerFactory.CreateLogger<LinuxTapeLibrary>(), Configuration.LibraryPath);
    }
    if (OperatingSystem.IsWindows()) {
      return new WindowsTapeLibrary(loggerFactory.CreateLogger<WindowsTapeLibrary>(), Configuration.LibraryPath);
    }
    throw new PlatformNotSupportedException("Physical tape library access requires Windows or Linux. Use DeviceMode=Emulated on other platforms.");
  }

  public IReadOnlyList<LibraryElementStatus> Inventory() {
    ITapeLibrary tapeLibrary = CreateLibrary();
    return tapeLibrary.GetElementStatus();
  }

  public LibraryStatusSnapshot GetLibraryStatusSnapshot() {
    if (!Configuration.IsTapeLibrary) {
      return new LibraryStatusSnapshot {
        DeviceMode = Configuration.DeviceMode,
        Topology = Configuration.Topology,
        LibraryPath = Configuration.LibraryPath,
        DevicePath = Configuration.LibraryPath,
        Available = false,
        Elements = Array.Empty<LibraryElementStatus>(),
        Error = "Library monitoring is disabled in StandaloneDrive topology."
      };
    }
    try {
      if (Configuration.IsEmulated) {
        EmulatedTapeLibrary emulatedTapeLibrary = new EmulatedTapeLibrary(Path.GetFullPath(Configuration.Emulator.DataDirectory), Configuration.Emulator.Library.SlotCount, Configuration.Emulator.Library.DriveCount);
        foreach (LibraryElementStatus item in from element in emulatedTapeLibrary.GetElementStatus()
                                              where string.Equals(element.ElementType, "Drive", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(element.CartridgeId)
                                              select element) {
          using EmulatedTapeDrive drive = new EmulatedTapeDrive(loggerFactory.CreateLogger<EmulatedTapeDrive>(), Path.GetFullPath(Configuration.Emulator.DataDirectory), item.CartridgeId);
          emulatedTapeLibrary.SynchronizeBarcodeWithDrive(drive);
        }
        return new LibraryStatusSnapshot {
          DeviceMode = Configuration.DeviceMode,
          Topology = Configuration.Topology,
          LibraryPath = Configuration.LibraryPath,
          DevicePath = emulatedTapeLibrary.DevicePath,
          Available = true,
          Elements = emulatedTapeLibrary.GetElementStatus()
        };
      }
      ITapeLibrary tapeLibrary = CreateLibrary();
      return new LibraryStatusSnapshot {
        DeviceMode = Configuration.DeviceMode,
        Topology = Configuration.Topology,
        LibraryPath = Configuration.LibraryPath,
        DevicePath = tapeLibrary.DevicePath,
        Available = true,
        Elements = tapeLibrary.GetElementStatus()
      };
    }
    catch (Exception ex) {
      return new LibraryStatusSnapshot {
        DeviceMode = Configuration.DeviceMode,
        Topology = Configuration.Topology,
        LibraryPath = Configuration.LibraryPath,
        DevicePath = Configuration.LibraryPath,
        Available = true,
        Elements = Array.Empty<LibraryElementStatus>(),
        Error = ex.Message
      };
    }
  }

  public void MoveMedium(int sourceSlot, int destinationSlot) {
    ITapeLibrary tapeLibrary = CreateLibrary();
    tapeLibrary.MoveMedium(sourceSlot, destinationSlot);
  }

  public LibraryElementStatus? FindByBarcode(string barcode) {
    ArgumentException.ThrowIfNullOrWhiteSpace(barcode, "barcode");
    string normalized = barcode.Trim();
    List<LibraryElementStatus> list = (from element in Inventory()
                                       where !string.IsNullOrWhiteSpace(element.Barcode) && string.Equals(element.Barcode.Trim(), normalized, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(element.CartridgeId)
                                       select element).ToList();
    if (list.Count == 0) {
      return null;
    }
    if (list.Count > 1) {
      throw new InvalidOperationException($"Barcode '{normalized}' appears in {list.Count} occupied slots; resolve duplicates before continuing.");
    }
    return list[0];
  }

  public int LoadBarcodeIntoDrive(string barcode) {
    ArgumentException.ThrowIfNullOrWhiteSpace(barcode, "barcode");
    EnsureLibraryTopology();
    LibraryElementStatus libraryElementStatus = FindByBarcode(barcode) ?? throw new InvalidOperationException("No cartridge with barcode '" + barcode.Trim() + "' found in library inventory.");
    if (string.Equals(libraryElementStatus.ElementType, "Drive", StringComparison.OrdinalIgnoreCase)) {
      return libraryElementStatus.Slot;
    }
    LibraryElementStatus libraryElementStatus2 = Inventory().FirstOrDefault((LibraryElementStatus item) => string.Equals(item.ElementType, "Drive", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(item.CartridgeId)) ?? throw new InvalidOperationException("No empty library drive bay is available.");
    MoveMedium(libraryElementStatus.Slot, libraryElementStatus2.Slot);
    return libraryElementStatus2.Slot;
  }

  public int UnloadDriveToStorage(int driveSlot) {
    EnsureLibraryTopology();
    IReadOnlyList<LibraryElementStatus> source = Inventory();
    LibraryElementStatus libraryElementStatus = source.FirstOrDefault((LibraryElementStatus item) => item.Slot == driveSlot && string.Equals(item.ElementType, "Drive", StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"Drive bay {driveSlot} was not found.");
    if (string.IsNullOrWhiteSpace(libraryElementStatus.CartridgeId)) {
      throw new InvalidOperationException($"Drive bay {driveSlot} is already empty.");
    }
    LibraryElementStatus libraryElementStatus2 = source.FirstOrDefault((LibraryElementStatus item) => string.Equals(item.ElementType, "Slot", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(item.CartridgeId)) ?? throw new InvalidOperationException("No empty storage slot is available to unload into.");
    MoveMedium(driveSlot, libraryElementStatus2.Slot);
    return libraryElementStatus2.Slot;
  }

  public IReadOnlyList<LibraryElementStatus> ListDriveBays() {
    EnsureLibraryTopology();
    return (from item in Inventory()
            where string.Equals(item.ElementType, "Drive", StringComparison.OrdinalIgnoreCase)
            orderby item.Slot
            select item).ToList();
  }
}
