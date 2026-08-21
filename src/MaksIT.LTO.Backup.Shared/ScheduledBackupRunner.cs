using MaksIT.LTO.Backup.Shared.Models;
using MaksIT.LTO.Backup.Shared.Scheduling;
using MaksIT.LTO.Core.MassStorage;
using Microsoft.Extensions.Logging;


namespace MaksIT.LTO.Backup.Shared;

public sealed class ScheduledBackupRunner(ILogger<ScheduledBackupRunner> logger, ConfigurationFileService configurationFileService, BackupOrchestrator backupOrchestrator, LibraryOrchestrator libraryOrchestrator) {
  public sealed record RunResult(bool Executed, string Message, bool Busy = false, string? SelectedBarcode = null);

  private static readonly object Gate = new object();

  private static bool _running;

  public RunResult RunAggregation(BackupAggregation aggregation, bool automated, DateTimeOffset? utcNow = null) {
    ArgumentNullException.ThrowIfNull(aggregation, "aggregation");
    Configuration current = configurationFileService.Current;
    if (!current.IsTapeLibrary) {
      return new RunResult(Executed: false, "Aggregations require Topology = TapeLibrary. Use the UI or console menu for StandaloneDrive.");
    }
    if (aggregation.Disabled && automated) {
      return new RunResult(Executed: false, "Aggregation '" + aggregation.Name + "' is disabled.");
    }
    if (aggregation.JobNames.Count == 0) {
      return new RunResult(Executed: false, "Aggregation '" + aggregation.Name + "' has no JobNames.");
    }
    ScheduleEvaluator.EvaluationResult evaluationResult = ScheduleEvaluator.Evaluate(automated, aggregation.Schedule, aggregation.LastRunUtc, utcNow);
    if (!evaluationResult.ShouldExecute) {
      return new RunResult(Executed: false, evaluationResult.SkipReason ?? "Execution skipped.");
    }
    lock (Gate) {
      if (_running) {
        return new RunResult(Executed: false, "Another backup aggregation is already running (lease).", Busy: true);
      }
      _running = true;
    }
    try {
      aggregation.LastRunUtc = evaluationResult.Now;
      configurationFileService.Save(current);
      List<string> list = new List<string>();
      foreach (string jobName in aggregation.JobNames) {
        BackupItem backupItem = current.Backups.FirstOrDefault((BackupItem item) => string.Equals(item.Name, jobName, StringComparison.OrdinalIgnoreCase));
        if (backupItem == null) {
          logger.LogWarning("Aggregation {Aggregation}: job '{JobName}' not found; skipping.", aggregation.Name, jobName);
          continue;
        }
        if (backupItem.Disabled) {
          logger.LogInformation("Aggregation {Aggregation}: job '{JobName}' disabled; skipping.", aggregation.Name, jobName);
          continue;
        }
        RunResult runResult = RunJobUnderLease(backupItem);
        if (!runResult.Executed) {
          return new RunResult(Executed: false, "Aggregation '" + aggregation.Name + "' stopped: " + runResult.Message);
        }
        list.Add(backupItem.Name + " (" + runResult.SelectedBarcode + ")");
      }
      if (list.Count == 0) {
        return new RunResult(Executed: false, "Aggregation '" + aggregation.Name + "' had no runnable jobs.");
      }
      return new RunResult(Executed: true, $"Aggregation '{aggregation.Name}' completed: {string.Join(", ", list)}.");
    }
    catch (Exception ex) {
      logger.LogError(ex, "Aggregation {Aggregation} failed.", aggregation.Name);
      return new RunResult(Executed: false, ex.Message);
    }
    finally {
      lock (Gate) {
        _running = false;
      }
    }
  }

  public RunResult RunJob(BackupItem backup) {
    ArgumentNullException.ThrowIfNull(backup, "backup");
    Configuration current = configurationFileService.Current;
    if (!current.IsTapeLibrary) {
      return new RunResult(Executed: false, "Pool-based backup requires Topology = TapeLibrary. Use the UI or console menu for StandaloneDrive.");
    }
    if (backup.Disabled) {
      return new RunResult(Executed: false, "Backup '" + backup.Name + "' is disabled.");
    }
    if (backup.Tapes.Count == 0) {
      return new RunResult(Executed: false, "Backup '" + backup.Name + "' has no Tapes configured.");
    }
    lock (Gate) {
      if (_running) {
        return new RunResult(Executed: false, "Another backup is already running (lease).", Busy: true);
      }
      _running = true;
    }
    try {
      return RunJobUnderLease(backup);
    }
    finally {
      lock (Gate) {
        _running = false;
      }
    }
  }

  public RunResult RunAggregationByName(string aggregationName, bool automated, DateTimeOffset? utcNow = null) {
    ArgumentException.ThrowIfNullOrWhiteSpace(aggregationName, "aggregationName");
    BackupAggregation aggregation = configurationFileService.Current.Aggregations.FirstOrDefault((BackupAggregation item) => string.Equals(item.Name, aggregationName.Trim(), StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("Aggregation '" + aggregationName + "' was not found in configuration.");
    return RunAggregation(aggregation, automated, utcNow);
  }

  public RunResult RunJobByName(string backupName) {
    ArgumentException.ThrowIfNullOrWhiteSpace(backupName, "backupName");
    BackupItem backup = configurationFileService.Current.Backups.FirstOrDefault((BackupItem item) => string.Equals(item.Name, backupName.Trim(), StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("Backup '" + backupName + "' was not found in configuration.");
    return RunJob(backup);
  }

  private RunResult RunJobUnderLease(BackupItem backup) {
    Configuration current = configurationFileService.Current;
    if (backup.Tapes.Count == 0) {
      return new RunResult(Executed: false, "Backup '" + backup.Name + "' has no Tapes configured.");
    }
    IReadOnlyList<LibraryElementStatus> inventory = libraryOrchestrator.Inventory();
    HashSet<string> availableBarcodes = MediaPoolSelector.CollectInventoryBarcodes(inventory);
    BackupTape backupTape = MediaPoolSelector.SelectNext(backup.Tapes, availableBarcodes);
    if (backupTape == null) {
      return new RunResult(Executed: false, "No configured tapes for '" + backup.Name + "' are present in the library inventory.");
    }
    string text = MediaPoolSelector.Normalize(backupTape.Barcode);
    int? num = null;
    try {
      if (logger.IsEnabled(LogLevel.Information)) {
        logger.LogInformation("Selected tape {Barcode} for backup {BackupName}.", text, backup.Name);
      }
      num = libraryOrchestrator.LoadBarcodeIntoDrive(text);
      backupOrchestrator.SelectedLibraryDriveSlot = num;
      backupOrchestrator.Backup(backup, text);
      backupTape.LastUsedUtc = DateTimeOffset.UtcNow;
      configurationFileService.Save(current);
      return new RunResult(Executed: true, $"Backup '{backup.Name}' completed on {text} (drive {num}).", Busy: false, text);
    }
    finally {
      if (num.HasValue) {
        int valueOrDefault = num.GetValueOrDefault();
        if (true) {
          try {
            libraryOrchestrator.UnloadDriveToStorage(valueOrDefault);
          }
          catch (Exception exception) {
            logger.LogWarning(exception, "Failed to unload drive {DriveSlot} after backup {BackupName}.", valueOrDefault, backup.Name);
          }
        }
      }
    }
  }
}
