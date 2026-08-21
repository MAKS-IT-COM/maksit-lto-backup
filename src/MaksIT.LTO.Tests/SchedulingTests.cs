using System.Globalization;
using System.Reflection;
using MaksIT.LTO.Backup.Shared;
using MaksIT.LTO.Backup.Shared.Models;
using MaksIT.LTO.Backup.Shared.Scheduling;
using MaksIT.LTO.Core.MassStorage;
using Microsoft.Extensions.Logging;


namespace MaksIT.LTO.Tests;

public class SchedulingTests {
  [Fact]
  public void ScheduleEvaluator_CatchUp_StaysDueAfterExactMinute() {
    var schedule = new BackupSchedule {
      RunWeekday = ["Monday"],
      RunTime = ["00:00"],
      MinIntervalMinutes = 10
    };
    var now = new DateTimeOffset(2026, 7, 20, 0, 30, 0, TimeSpan.Zero);
    Assert.Equal(DayOfWeek.Monday, now.UtcDateTime.DayOfWeek);
    Assert.True(ScheduleEvaluator.Evaluate(automated: true, schedule, null, now).ShouldExecute);
    Assert.False(ScheduleEvaluator.Evaluate(automated: true, schedule, new DateTimeOffset(2026, 7, 20, 0, 5, 0, TimeSpan.Zero), now).ShouldExecute);
  }

  [Fact]
  public void ScheduleEvaluator_EmptyMonthAndWeekday_MatchAny() {
    var schedule = new BackupSchedule {
      RunMonth = [],
      RunWeekday = [],
      RunTime = ["12:00"],
      MinIntervalMinutes = 10
    };
    var utcNow = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
    Assert.True(ScheduleEvaluator.MatchesScheduleCatchUp(utcNow, schedule, null));
  }

  [Fact]
  public void ScheduleEvaluator_Automated_SkipsWithoutSchedule() {
    Assert.False(ScheduleEvaluator.Evaluate(automated: true, null, null).ShouldExecute);
  }

  [Fact]
  public void ScheduleEvaluator_Manual_SkipsCalendarButHonorsInterval() {
    var now = DateTimeOffset.Parse("2026-07-20T12:00:00Z", CultureInfo.InvariantCulture);
    var recent = now.AddMinutes(-5.0);
    var schedule = new BackupSchedule {
      MinIntervalMinutes = 10
    };
    Assert.False(ScheduleEvaluator.Evaluate(automated: false, schedule, recent, now).ShouldExecute);
    Assert.True(ScheduleEvaluator.Evaluate(automated: false, schedule, recent.AddMinutes(-20.0), now).ShouldExecute);
  }

  [Fact]
  public void MediaPoolSelector_PrefersNeverUsedThenOldest() {
    var list = new List<BackupTape> {
      new() {
        Barcode = "A",
        LastUsedUtc = DateTimeOffset.Parse("2026-07-22T00:00:00Z", CultureInfo.InvariantCulture)
      },
      new() {
        Barcode = "B",
        LastUsedUtc = null
      },
      new() {
        Barcode = "C",
        LastUsedUtc = DateTimeOffset.Parse("2026-07-20T00:00:00Z", CultureInfo.InvariantCulture)
      }
    };
    Assert.Equal("B", MediaPoolSelector.SelectNext(list).Barcode);
    list[1].LastUsedUtc = DateTimeOffset.Parse("2026-07-23T00:00:00Z", CultureInfo.InvariantCulture);
    Assert.Equal("C", MediaPoolSelector.SelectNext(list).Barcode);
  }

  [Fact]
  public void MediaPoolSelector_SkipsMissingInventoryBarcodes() {
    var tapes = new List<BackupTape> {
      new() {
        Barcode = "MISSING",
        LastUsedUtc = null
      },
      new() {
        Barcode = "LTO001",
        LastUsedUtc = DateTimeOffset.UtcNow
      }
    };
    var availableBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LTO001" };
    Assert.Equal("LTO001", MediaPoolSelector.SelectNext(tapes, availableBarcodes).Barcode);
  }

  [Fact]
  public void LibraryOrchestrator_LoadAndUnload_UsesEmptyDrive() {
    var root = Path.Combine(Path.GetTempPath(), "lto-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    _ = new EmulatedTapeLibrary(root, 4);
    var configurationFileService = new ConfigurationFileService(Path.Combine(root, "configuration.json"));
    configurationFileService.Save(new Configuration {
      DeviceMode = DeviceModes.Emulated.Name,
      Topology = DeviceTopologies.TapeLibrary.Name,
      Emulator = new EmulatorOptions {
        DataDirectory = root,
        Library = new EmulatorLibraryOptions {
          SlotCount = 4,
          DriveCount = 2,
          IePortCount = 0
        }
      }
    });
    using var loggerFactory = LoggerFactory.Create(_ => { });
    var libraryOrchestrator = new LibraryOrchestrator(configurationFileService, loggerFactory);
    var driveSlot = libraryOrchestrator.LoadBarcodeIntoDrive("LTO001");
    Assert.True((uint)(driveSlot - 1000) <= 1u);
    var loaded = libraryOrchestrator.FindByBarcode("LTO001");
    Assert.NotNull(loaded);
    Assert.Equal("Drive", loaded.ElementType);
    var storageSlot = libraryOrchestrator.UnloadDriveToStorage(driveSlot);
    Assert.True(storageSlot < 1000);
    var unloaded = libraryOrchestrator.FindByBarcode("LTO001");
    Assert.NotNull(unloaded);
    Assert.Equal("Slot", unloaded.ElementType);
  }

  [Fact]
  public void ScheduledBackupRunner_BusyDoesNotStampAggregationLastRun() {
    var root = Path.Combine(Path.GetTempPath(), "lto-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var configurationFileService = new ConfigurationFileService(Path.Combine(root, "configuration.json"));
    var aggregation = new BackupAggregation {
      Name = "Night",
      JobNames = ["Job"],
      Schedule = new BackupSchedule {
        RunTime = [],
        MinIntervalMinutes = 1
      }
    };
    configurationFileService.Save(new Configuration {
      DeviceMode = DeviceModes.Emulated.Name,
      Topology = DeviceTopologies.TapeLibrary.Name,
      Aggregations = [aggregation],
      Backups = [
        new BackupItem {
          Name = "Job",
          LTOGen = "LTO5",
          Source = new WorkingFolder {
            LocalPath = new LocalPath {
              Path = root
            }
          },
          Destination = new WorkingFolder {
            LocalPath = new LocalPath {
              Path = root
            }
          },
          Tapes = [
            new BackupTape {
              Barcode = "LTO001"
            }
          ]
        }
      ],
      Emulator = new EmulatorOptions {
        DataDirectory = root,
        Library = new EmulatorLibraryOptions {
          SlotCount = 4,
          DriveCount = 1
        }
      }
    });
    using var loggerFactory = LoggerFactory.Create(_ => { });
    var runner = new ScheduledBackupRunner(
      loggerFactory.CreateLogger<ScheduledBackupRunner>(),
      configurationFileService,
      new BackupOrchestrator(loggerFactory.CreateLogger<BackupOrchestrator>(), loggerFactory, configurationFileService),
      new LibraryOrchestrator(configurationFileService, loggerFactory));
    var field = typeof(ScheduledBackupRunner).GetField("_running", BindingFlags.Static | BindingFlags.NonPublic);
    Assert.NotNull(field);
    field.SetValue(null, true);
    try {
      var runResult = runner.RunAggregation(configurationFileService.Current.Aggregations[0], automated: true);
      Assert.True(runResult.Busy);
      Assert.False(runResult.Executed);
      Assert.Null(configurationFileService.Current.Aggregations[0].LastRunUtc);
    }
    finally {
      field.SetValue(null, false);
    }
  }
}
