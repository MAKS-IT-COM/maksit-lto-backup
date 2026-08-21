using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MaksIT.Core.Logging;
using MaksIT.LTO.Backup.Shared;


namespace MaksIT.LTO.Backup;

public static class Program {
  public static int Main(string[] args) {
    var basePath = AppContext.BaseDirectory;
    var configurationPath = Path.Combine(basePath, "configuration.json");

    var host = Host.CreateDefaultBuilder()
      .ConfigureAppConfiguration(builder => {
        builder.SetBasePath(basePath);
        builder.AddJsonFile("configuration.json", optional: false, reloadOnChange: true);
      })
      .ConfigureServices((_, services) => {
        services.AddSingleton(_ => new ConfigurationFileService(configurationPath));
        services.AddSingleton<BackupOrchestrator>();
        services.AddSingleton<LibraryOrchestrator>();
        services.AddSingleton<ScheduledBackupRunner>();
        services.AddSingleton<Application>();
      })
      .ConfigureLogging((_, logging) => {
        logging.AddConsoleLogger(Path.Combine(basePath, "logs"));
      })
      .Build();

    if (args.Length == 0) {
      host.Services.GetRequiredService<Application>().Run();
      return 0;
    }

    var command = args[0].ToLowerInvariant();
    if (command is "backup" or "--backup")
      return RunCliBackup(host.Services, args);

    if (command is "--help" or "-h" or "/?") {
      PrintHelp();
      return 0;
    }

    host.Services.GetRequiredService<Application>().Run();
    return 0;
  }

  private static int RunCliBackup(IServiceProvider services, string[] args) {
    string? jobName = null;
    string? aggregationName = null;
    var scheduled = false;

    for (var i = 1; i < args.Length; i++) {
      var arg = args[i];
      if (arg is "--scheduled" or "-s") {
        scheduled = true;
        continue;
      }

      if (arg is "--name" or "-n") {
        if (i + 1 >= args.Length) {
          Console.Error.WriteLine("Missing value for --name.");
          return 1;
        }

        jobName = args[++i];
        continue;
      }

      if (arg is "--aggregation" or "-a") {
        if (i + 1 >= args.Length) {
          Console.Error.WriteLine("Missing value for --aggregation.");
          return 1;
        }

        aggregationName = args[++i];
        continue;
      }

      if (arg.StartsWith('-') || jobName is not null || aggregationName is not null) {
        Console.Error.WriteLine($"Unknown argument: {arg}");
        return 1;
      }

      jobName = arg;
    }

    var runner = services.GetRequiredService<ScheduledBackupRunner>();
    try {
      if (!string.IsNullOrWhiteSpace(aggregationName)) {
        var result = runner.RunAggregationByName(aggregationName, scheduled);
        Console.WriteLine(result.Message);
        return result.Executed ? 0 : 2;
      }

      if (string.IsNullOrWhiteSpace(jobName)) {
        Console.Error.WriteLine("Usage: backup --name \"Job\" | backup --aggregation \"Night\" [--scheduled]");
        return 1;
      }

      if (scheduled) {
        Console.Error.WriteLine("--scheduled applies to aggregations only. Use --aggregation \"Name\" --scheduled.");
        return 1;
      }

      var jobResult = runner.RunJobByName(jobName);
      Console.WriteLine(jobResult.Message);
      return jobResult.Executed ? 0 : 2;
    }
    catch (Exception ex) {
      Console.Error.WriteLine(ex.Message);
      return 1;
    }
  }

  private static void PrintHelp() =>
    Console.WriteLine(
      """
      MaksIT.LTO.Backup

      Usage:
        MaksIT.LTO.Backup
        MaksIT.LTO.Backup backup --name "Job"
        MaksIT.LTO.Backup backup --aggregation "Night" [--scheduled]
        MaksIT.LTO.Backup --help

      backup --name
        Library one-shot for one job (Tapes pool; Topology = TapeLibrary).

      backup --aggregation
        Run an ordered aggregation wave. --scheduled applies calendar + catch-up (Worker rules).
      """);
}
