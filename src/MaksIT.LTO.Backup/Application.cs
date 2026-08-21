using System.Reflection;
using System.Text;
using MaksIT.LTO.Backup.Shared;
using MaksIT.LTO.Backup.Shared.Models;


namespace MaksIT.LTO.Backup;

public sealed class Application(
  BackupOrchestrator backupOrchestrator,
  LibraryOrchestrator libraryOrchestrator,
  ConfigurationFileService configurationFileService
) {
  public void Run() {
    Console.OutputEncoding = Encoding.UTF8;
    var version = typeof(Application).Assembly
      .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
      .InformationalVersion?
      .Split('+')[0]
      ?? "0.0.0";

    while (true) {
      var topology = configurationFileService.Current.Topology;
      var isLibrary = configurationFileService.Current.IsTapeLibrary;

      Console.WriteLine($"MaksIT.LTO.Backup v{version}");
      Console.WriteLine("© Maksym Sadovnychyy (MAKS-IT) 2024–2026");
      Console.WriteLine($"Topology: {topology}");
      Console.WriteLine();
      Console.WriteLine("1. Load tape");
      Console.WriteLine("2. Backup");
      Console.WriteLine("3. Restore");
      Console.WriteLine("4. Eject tape");
      Console.WriteLine("5. Get device status");
      Console.WriteLine("6. Tape Erase (Short)");
      Console.WriteLine("7. Read Cartridge Memory");
      if (isLibrary) {
        Console.WriteLine("8. Move medium (library)");
        Console.WriteLine("9. Library inventory");
      }
      else {
        Console.WriteLine("8. (unavailable — set Topology=TapeLibrary)");
        Console.WriteLine("9. (unavailable — set Topology=TapeLibrary)");
      }

      Console.WriteLine("10. Exit");
      Console.Write("Enter your choice: ");

      var choice = Console.ReadLine();
      try {
        switch (choice) {
          case "1":
            backupOrchestrator.LoadTape();
            break;
          case "2":
            SelectBackupAndRun(item => backupOrchestrator.Backup(item));
            break;
          case "3":
            SelectBackupAndRun(backupOrchestrator.Restore);
            break;
          case "4":
            backupOrchestrator.EjectTape();
            break;
          case "5":
            Console.WriteLine($"Device status code: {backupOrchestrator.GetDeviceStatus()}");
            break;
          case "6":
            SelectBackupAndRun(item => backupOrchestrator.TapeErase(item.LTOGen));
            break;
          case "7":
            foreach (var attribute in backupOrchestrator.ReadCartridgeMemory()) {
              Console.WriteLine($"(0x{attribute.Address:X4}) {attribute.Name}: {attribute.GetValueAsString()}");
            }
            break;
          case "8":
            EnsureLibrary();
            Console.Write("Source slot: ");
            var source = int.Parse(Console.ReadLine() ?? "0");
            Console.Write("Destination slot: ");
            var destination = int.Parse(Console.ReadLine() ?? "0");
            libraryOrchestrator.MoveMedium(source, destination);
            break;
          case "9":
            EnsureLibrary();
            foreach (var slot in libraryOrchestrator.Inventory()) {
              Console.WriteLine($"Slot {slot.Slot} [{slot.ElementType}] -> {slot.Barcode ?? "empty"}");
            }
            break;
          case "10":
            return;
          default:
            Console.WriteLine("Invalid option.");
            break;
        }
      }
      catch (Exception ex) {
        Console.WriteLine($"Error: {ex.Message}");
      }

      Console.WriteLine();
    }
  }

  private void EnsureLibrary() {
    if (!configurationFileService.Current.IsTapeLibrary) {
      throw new InvalidOperationException("Library commands require Topology = TapeLibrary in configuration.json.");
    }
  }

  private void SelectBackupAndRun(Action<BackupItem> action) {
    if (!TrySelectLibraryDrive()) {
      return;
    }

    if (backupOrchestrator.Backups.Count == 0) {
      Console.WriteLine("No backups configured.");
      return;
    }

    for (var i = 0; i < backupOrchestrator.Backups.Count; i++) {
      var backup = backupOrchestrator.Backups[i];
      Console.WriteLine($"{i + 1}. {backup.Name} ({backup.LTOGen}) Barcode: {(string.IsNullOrWhiteSpace(backup.Barcode) ? "None" : backup.Barcode)}");
    }

    Console.Write("Select backup index: ");
    if (!int.TryParse(Console.ReadLine(), out var index) || index < 1 || index > backupOrchestrator.Backups.Count) {
      Console.WriteLine("Invalid index.");
      return;
    }

    action(backupOrchestrator.Backups[index - 1]);
  }

  private bool TrySelectLibraryDrive() {
    if (!configurationFileService.Current.IsTapeLibrary) {
      return true;
    }

    var drives = backupOrchestrator.GetOccupiedLibraryDrives();
    if (drives.Count == 0) {
      Console.WriteLine("No cartridge in a library drive. Move media into a drive bay first.");
      return false;
    }

    if (drives.Count == 1) {
      backupOrchestrator.SelectedLibraryDriveSlot = drives[0].DriveSlot;
      Console.WriteLine($"Using {drives[0].Summary}");
      return true;
    }

    Console.WriteLine("Occupied library drives:");
    for (var i = 0; i < drives.Count; i++) {
      Console.WriteLine($"{i + 1}. {drives[i].Summary}");
    }

    Console.Write("Select drive index: ");
    if (!int.TryParse(Console.ReadLine(), out var index) || index < 1 || index > drives.Count) {
      Console.WriteLine("Invalid drive index.");
      return false;
    }

    backupOrchestrator.SelectedLibraryDriveSlot = drives[index - 1].DriveSlot;
    return true;
  }
}
