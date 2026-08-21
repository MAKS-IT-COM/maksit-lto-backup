using System.Text;
using MaksIT.LTO.Backup.Shared;
using MaksIT.LTO.Backup.Shared.Models;
using MaksIT.LTO.Core.MassStorage;
using Microsoft.Extensions.Logging;


namespace MaksIT.LTO.Tests;

public class EmulationTests {
  [Fact]
  public void EmulatedTapeDrive_ReadsBackWrittenBlock() {
    string text = Path.Combine(Path.GetTempPath(), "lto-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(text);
    using ILoggerFactory factory = LoggerFactory.Create(delegate {
    });
    using EmulatedTapeDrive emulatedTapeDrive = new EmulatedTapeDrive(factory.CreateLogger<EmulatedTapeDrive>(), text, "drive-a");
    byte[] array = (from x in Enumerable.Range(0, 256)
                    select (byte)x).ToArray();
    Assert.Equal(0, emulatedTapeDrive.WriteData(array));
    emulatedTapeDrive.SetPosition(0u, 0u, 0L);
    byte[] array2 = new byte[array.Length];
    int actual = emulatedTapeDrive.ReadData(array2, 0, array2.Length);
    Assert.Equal(array.Length, actual);
    Assert.Equal(array, array2);
  }

  [Fact]
  public void EmulatedTapeDrive_PersistsLargeBlocksWithoutEmbeddingInJson() {
    string text = Path.Combine(Path.GetTempPath(), "lto-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(text);
    using ILoggerFactory factory = LoggerFactory.Create(delegate {
    });
    using (EmulatedTapeDrive emulatedTapeDrive = new EmulatedTapeDrive(factory.CreateLogger<EmulatedTapeDrive>(), text, "big")) {
      byte[] array = new byte[262144];
      for (int num = 0; num < 32; num++) {
        array[0] = (byte)num;
        array[^1] = (byte)(255 - num);
        Assert.Equal(0, emulatedTapeDrive.WriteData(array));
      }
    }
    string text2 = Path.Combine(text, "big.drive.json");
    string text3 = Path.Combine(text, "big.blocks.bin");
    Assert.True(File.Exists(text2));
    Assert.True(File.Exists(text3));
    Assert.True(new FileInfo(text3).Length >= 8388608);
    Assert.True(new FileInfo(text2).Length < 65536);
    using EmulatedTapeDrive emulatedTapeDrive2 = new EmulatedTapeDrive(factory.CreateLogger<EmulatedTapeDrive>(), text, "big");
    emulatedTapeDrive2.SetPosition(0u, 0u, 0L);
    byte[] array2 = new byte[262144];
    for (int num2 = 0; num2 < 32; num2++) {
      int actual = emulatedTapeDrive2.ReadData(array2, 0, array2.Length);
      Assert.Equal(262144, actual);
      Assert.Equal((byte)num2, array2[0]);
      Assert.Equal((byte)(255 - num2), array2[^1]);
    }
  }

  [Fact]
  public void EmulatedTapeLibrary_MovesMediumBetweenSlots() {
    string text = Path.Combine(Path.GetTempPath(), "lto-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(text);
    EmulatedTapeLibrary emulatedTapeLibrary = new EmulatedTapeLibrary(text, 4, 1);
    IReadOnlyList<LibraryElementStatus> elementStatus = emulatedTapeLibrary.GetElementStatus();
    Assert.Equal("LTO001", elementStatus.Single((LibraryElementStatus s) => s.Slot == 1).Barcode);
    emulatedTapeLibrary.MoveMedium(1, 1000);
    IReadOnlyList<LibraryElementStatus> elementStatus2 = emulatedTapeLibrary.GetElementStatus();
    Assert.Null(elementStatus2.Single((LibraryElementStatus s) => s.Slot == 1).Barcode);
    Assert.Equal("LTO001", elementStatus2.Single((LibraryElementStatus s) => s.Slot == 1000).Barcode);
  }

  [Fact]
  public void BackupAndRestore_RoundTripWithStandaloneEmulatedDrive() {
    string path = Path.Combine(Path.GetTempPath(), "lto-tests", Guid.NewGuid().ToString("N"));
    string text = Path.Combine(path, "src");
    string text2 = Path.Combine(path, "dst");
    string dataDirectory = Path.Combine(path, "emu");
    Directory.CreateDirectory(text);
    Directory.CreateDirectory(text2);
    string path2 = Path.Combine(text, "sample.txt");
    File.WriteAllText(path2, "hello from emulated lto");
    var configuration = new Configuration {
      DeviceMode = DeviceModes.Emulated.Name,
      Topology = DeviceTopologies.StandaloneDrive.Name,
      TapePath = "\\\\.\\Tape0",
      WriteDelay = 0,
      Emulator = new EmulatorOptions {
        DataDirectory = dataDirectory
      },
      Backups = [
        new BackupItem {
          Name = "Test",
          Barcode = "LTO001",
          LTOGen = "LTO5",
          Source = new WorkingFolder {
            LocalPath = new LocalPath {
              Path = text
            }
          },
          Destination = new WorkingFolder {
            LocalPath = new LocalPath {
              Path = text2
            }
          }
        }
      ]
    };
    using ILoggerFactory loggerFactory = LoggerFactory.Create(delegate {
    });
    string configurationPath = Path.Combine(path, "configuration.json");
    ConfigurationFileService configurationFileService = new ConfigurationFileService(configurationPath);
    configurationFileService.Save(configuration);
    BackupOrchestrator backupOrchestrator = new BackupOrchestrator(loggerFactory.CreateLogger<BackupOrchestrator>(), loggerFactory, configurationFileService);
    backupOrchestrator.Backup(configuration.Backups[0]);
    uint blockSize = LTOBlockSizes.GetBlockSize("LTO5");
    BackupDescriptor backupDescriptor = backupOrchestrator.FindDescriptor(blockSize);
    Assert.NotNull(backupDescriptor);
    Assert.Equal(2, backupDescriptor.SchemaVersion);
    Assert.Equal("MLTO", backupDescriptor.FormatId);
    Assert.Equal("Test", backupDescriptor.BackupName);
    Assert.Equal("LTO5", backupDescriptor.LtoGen);
    Assert.False(string.IsNullOrWhiteSpace(backupDescriptor.ContentHash));
    Assert.Single(backupDescriptor.Files);
    Assert.Equal(FileHashAlgorithms.Sha256.Name, backupDescriptor.Files[0].HashAlgorithm);
    Assert.Equal(64, backupDescriptor.Files[0].FileHash.Length);
    backupOrchestrator.Restore(configuration.Backups[0]);
    string path3 = Path.Combine(text2, "sample.txt");
    Assert.True(File.Exists(path3));
    Assert.Equal(File.ReadAllText(path2), File.ReadAllText(path3));
    IReadOnlyList<LTOCmAttribute> collection = backupOrchestrator.ReadCartridgeMemory();
    Assert.Contains((IEnumerable<LTOCmAttribute>)collection, (Predicate<LTOCmAttribute>)((LTOCmAttribute a) => a.Address == 2049 && a.Value != null && Encoding.ASCII.GetString(a.Value).Contains("MaksITLTO")));
  }

  [Fact]
  public void Backup_AppendAddsSecondSetAndRestorePrefersLatestByName() {
    string path = Path.Combine(Path.GetTempPath(), "lto-tests", Guid.NewGuid().ToString("N"));
    string text = Path.Combine(path, "src1");
    string text2 = Path.Combine(path, "src2");
    string text3 = Path.Combine(path, "dst");
    string dataDirectory = Path.Combine(path, "emu");
    Directory.CreateDirectory(text);
    Directory.CreateDirectory(text2);
    Directory.CreateDirectory(text3);
    File.WriteAllText(Path.Combine(text, "a.txt"), "first-set");
    File.WriteAllText(Path.Combine(text2, "b.txt"), "second-set");
    var configuration = new Configuration {
      DeviceMode = DeviceModes.Emulated.Name,
      Topology = DeviceTopologies.StandaloneDrive.Name,
      TapePath = "\\\\.\\Tape0",
      WriteDelay = 0,
      Emulator = new EmulatorOptions {
        DataDirectory = dataDirectory
      },
      Backups = [
        new BackupItem {
          Name = "Set1",
          Barcode = "LTO001",
          LTOGen = "LTO5",
          WriteMode = BackupWriteModes.Overwrite.Name,
          Source = new WorkingFolder {
            LocalPath = new LocalPath {
              Path = text
            }
          },
          Destination = new WorkingFolder {
            LocalPath = new LocalPath {
              Path = text3
            }
          }
        },
        new BackupItem {
          Name = "Set2",
          Barcode = "LTO001",
          LTOGen = "LTO5",
          WriteMode = BackupWriteModes.Append.Name,
          Source = new WorkingFolder {
            LocalPath = new LocalPath {
              Path = text2
            }
          },
          Destination = new WorkingFolder {
            LocalPath = new LocalPath {
              Path = text3
            }
          }
        }
      ]
    };
    using ILoggerFactory loggerFactory = LoggerFactory.Create(delegate {
    });
    ConfigurationFileService configurationFileService = new ConfigurationFileService(Path.Combine(path, "configuration.json"));
    configurationFileService.Save(configuration);
    BackupOrchestrator backupOrchestrator = new BackupOrchestrator(loggerFactory.CreateLogger<BackupOrchestrator>(), loggerFactory, configurationFileService);
    backupOrchestrator.Backup(configuration.Backups[0]);
    backupOrchestrator.Backup(configuration.Backups[1]);
    uint blockSize = LTOBlockSizes.GetBlockSize("LTO5");
    BackupDescriptor backupDescriptor = backupOrchestrator.FindDescriptor(blockSize);
    Assert.NotNull(backupDescriptor);
    Assert.Equal("Set2", backupDescriptor.BackupName);
    BackupDescriptor backupDescriptor2 = backupOrchestrator.FindDescriptor(blockSize, "Set1");
    Assert.NotNull(backupDescriptor2);
    Assert.Equal("Set1", backupDescriptor2.BackupName);
    backupOrchestrator.Restore(configuration.Backups[1]);
    Assert.Equal("second-set", File.ReadAllText(Path.Combine(text3, "b.txt")));
  }

  [Fact]
  public void EmulatedTapeDrive_SpaceEndOfDataMovesPastLastEntry() {
    string text = Path.Combine(Path.GetTempPath(), "lto-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(text);
    using ILoggerFactory factory = LoggerFactory.Create(delegate {
    });
    using EmulatedTapeDrive emulatedTapeDrive = new EmulatedTapeDrive(factory.CreateLogger<EmulatedTapeDrive>(), text, "eod");
    Assert.Equal(0, emulatedTapeDrive.WriteData(new byte[3] { 1, 2, 3 }));
    emulatedTapeDrive.SetPosition(4u, 0u, 0L);
    TapePosition position = emulatedTapeDrive.GetPosition(0u);
    Assert.Equal(1u, position.OffsetLow);
  }

  [Fact]
  public void EmulatedTapeLibrary_BarcodeStaysInSyncWithDriveMam() {
    string text = Path.Combine(Path.GetTempPath(), "lto-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(text);
    using ILoggerFactory factory = LoggerFactory.Create(delegate {
    });
    EmulatedTapeLibrary emulatedTapeLibrary = new EmulatedTapeLibrary(text, 4, 1);
    emulatedTapeLibrary.EnsureMediaInDrive();
    LibraryElementStatus occupiedDrive = emulatedTapeLibrary.GetOccupiedDrive();
    Assert.NotNull(occupiedDrive);
    using EmulatedTapeDrive drive = new EmulatedTapeDrive(factory.CreateLogger<EmulatedTapeDrive>(), text, occupiedDrive.CartridgeId);
    emulatedTapeLibrary.SynchronizeBarcodeWithDrive(drive);
    Assert.Equal(occupiedDrive.Barcode, EmulatedTapeLibrary.ReadMamBarcode(drive));
    EmulatedTapeLibrary.WriteMamBarcode(drive, "SYNC123");
    emulatedTapeLibrary.SynchronizeBarcodeWithDrive(drive);
    Assert.Equal("SYNC123", emulatedTapeLibrary.GetOccupiedDrive()?.Barcode);
    Assert.Equal("SYNC123", EmulatedTapeLibrary.ReadMamBarcode(drive));
  }

  [Fact]
  public void WriteBarcode_UpdatesLibraryInventoryOnlyInTapeLibraryTopology() {
    string text = Path.Combine(Path.GetTempPath(), "lto-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(text);
    Configuration configuration = new Configuration {
      DeviceMode = DeviceModes.Emulated.Name,
      Topology = DeviceTopologies.TapeLibrary.Name,
      Emulator = new EmulatorOptions {
        DataDirectory = text,
        Library = new EmulatorLibraryOptions {
          SlotCount = 4,
          DriveCount = 1
        }
      },
      Backups = new List<BackupItem>()
    };
    using ILoggerFactory loggerFactory = LoggerFactory.Create(delegate {
    });
    ConfigurationFileService configurationFileService = new ConfigurationFileService(Path.Combine(text, "configuration.json"));
    configurationFileService.Save(configuration);
    EmulatedTapeLibrary emulatedTapeLibrary = new EmulatedTapeLibrary(text, 4, 1);
    emulatedTapeLibrary.MoveMedium(1, 1000);
    BackupOrchestrator backupOrchestrator = new BackupOrchestrator(loggerFactory.CreateLogger<BackupOrchestrator>(), loggerFactory, configurationFileService);
    LibraryOrchestrator libraryOrchestrator = new LibraryOrchestrator(configurationFileService, loggerFactory);
    backupOrchestrator.WriteBarcode("JOBBAR");
    LibraryStatusSnapshot libraryStatusSnapshot = libraryOrchestrator.GetLibraryStatusSnapshot();
    LibraryElementStatus libraryElementStatus = libraryStatusSnapshot.Elements.Single((LibraryElementStatus element) => string.Equals(element.ElementType, "Drive", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(element.CartridgeId));
    Assert.Equal("JOBBAR", libraryElementStatus.Barcode);
  }

  [Fact]
  public void BackupOrchestrator_UsesSelectedLibraryDriveWhenMultipleAreLoaded() {
    string text = Path.Combine(Path.GetTempPath(), "lto-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(text);
    Configuration configuration = new Configuration {
      DeviceMode = DeviceModes.Emulated.Name,
      Topology = DeviceTopologies.TapeLibrary.Name,
      Emulator = new EmulatorOptions {
        DataDirectory = text,
        Library = new EmulatorLibraryOptions {
          SlotCount = 4,
          DriveCount = 2
        }
      },
      Backups = new List<BackupItem>()
    };
    using ILoggerFactory loggerFactory = LoggerFactory.Create(delegate {
    });
    ConfigurationFileService configurationFileService = new ConfigurationFileService(Path.Combine(text, "configuration.json"));
    configurationFileService.Save(configuration);
    EmulatedTapeLibrary emulatedTapeLibrary = new EmulatedTapeLibrary(text, 4);
    emulatedTapeLibrary.MoveMedium(1, 1000);
    emulatedTapeLibrary.MoveMedium(2, 1001);
    BackupOrchestrator backupOrchestrator = new BackupOrchestrator(loggerFactory.CreateLogger<BackupOrchestrator>(), loggerFactory, configurationFileService);
    Assert.False(backupOrchestrator.GetLoadedCartridge().IsLoaded);
    Assert.Equal(2, backupOrchestrator.GetOccupiedLibraryDrives().Count);
    backupOrchestrator.SelectedLibraryDriveSlot = 1001;
    backupOrchestrator.WriteBarcode("DRIVE2");
    EmulatedTapeLibrary emulatedTapeLibrary2 = new EmulatedTapeLibrary(text, 4);
    Assert.Equal("DRIVE2", emulatedTapeLibrary2.GetDrive(1001)?.Barcode);
    Assert.Equal("LTO001", emulatedTapeLibrary2.GetDrive(1000)?.Barcode);
  }

  [Fact]
  public void LibraryOrchestrator_IsUnavailableInStandaloneTopology() {
    string text = Path.Combine(Path.GetTempPath(), "lto-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(text);
    Configuration configuration = new Configuration {
      DeviceMode = DeviceModes.Emulated.Name,
      Topology = DeviceTopologies.StandaloneDrive.Name,
      Emulator = new EmulatorOptions {
        DataDirectory = text
      }
    };
    using ILoggerFactory loggerFactory = LoggerFactory.Create(delegate {
    });
    ConfigurationFileService configurationFileService = new ConfigurationFileService(Path.Combine(text, "configuration.json"));
    configurationFileService.Save(configuration);
    LibraryOrchestrator libraryOrchestrator = new LibraryOrchestrator(configurationFileService, loggerFactory);
    Assert.False(libraryOrchestrator.IsAvailable);
    Assert.False(libraryOrchestrator.GetLibraryStatusSnapshot().Available);
    Assert.Throws<InvalidOperationException>(() => libraryOrchestrator.Inventory());
  }
}
