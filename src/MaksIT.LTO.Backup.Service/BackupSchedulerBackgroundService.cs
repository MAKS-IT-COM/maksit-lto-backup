using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MaksIT.LTO.Backup.Shared;
using MaksIT.LTO.Backup.Shared.Models;


namespace MaksIT.LTO.Backup.Service;

public sealed class BackupSchedulerBackgroundService(
  ILogger<BackupSchedulerBackgroundService> logger,
  ConfigurationFileService configurationFileService,
  ScheduledBackupRunner scheduledBackupRunner
) : BackgroundService {
  private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

  protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    logger.LogInformation("LTO backup aggregation scheduler started.");
    while (!stoppingToken.IsCancellationRequested) {
      try {
        Tick();
      }
      catch (Exception exception) {
        logger.LogError(exception, "Scheduler tick failed.");
      }

      try {
        await Task.Delay(PollInterval, stoppingToken);
      }
      catch (OperationCanceledException) {
        break;
      }
    }
  }

  private void Tick() {
    configurationFileService.Reload();
    var current = configurationFileService.Current;
    if (!current.IsTapeLibrary) {
      logger.LogDebug("Topology is StandaloneDrive; scheduled aggregations are skipped.");
      return;
    }

    foreach (var aggregation in current.Aggregations.ToList()) {
      if (aggregation.Disabled || aggregation.Schedule is null || aggregation.JobNames.Count == 0)
        continue;

      try {
        var result = scheduledBackupRunner.RunAggregation(aggregation, automated: true);
        if (result.Executed)
          logger.LogInformation("{Message}", result.Message);
        else if (result.Busy)
          logger.LogInformation("Aggregation {Name} deferred (lease busy).", aggregation.Name);
        else if (logger.IsEnabled(LogLevel.Debug))
          logger.LogDebug("{Aggregation}: {Message}", aggregation.Name, result.Message);
      }
      catch (Exception exception) {
        logger.LogError(exception, "Aggregation {Name} failed.", aggregation.Name);
      }
    }
  }
}
