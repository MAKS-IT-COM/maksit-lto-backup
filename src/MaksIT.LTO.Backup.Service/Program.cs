using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MaksIT.Core.Logging;
using MaksIT.LTO.Backup.Shared;


namespace MaksIT.LTO.Backup.Service;

internal static class Program {
  public static int Main(string[] args) {
    var basePath = AppContext.BaseDirectory;
    var configurationFileService = new ConfigurationFileService();
    var service = configurationFileService.Current.Service;
    var serviceName = string.IsNullOrWhiteSpace(service.ServiceName)
      ? "MaksIT.LTO.Backup"
      : service.ServiceName.Trim();
    var description = string.IsNullOrWhiteSpace(service.Description)
      ? "Schedules LTO library backups using barcode media pools."
      : service.Description;

    if (args.Length > 0) {
      var command = args[0].ToLowerInvariant();
      var exePath = Path.GetFullPath(
        Environment.ProcessPath
        ?? Path.Combine(basePath, OperatingSystem.IsWindows()
          ? "MaksIT.LTO.Backup.Service.exe"
          : "MaksIT.LTO.Backup.Service"));

      switch (command) {
        case "--install":
        case "-i":
          return InstallService(serviceName, exePath, description);
        case "--uninstall":
        case "-u":
          return UninstallService(serviceName);
        case "--start":
          return StartService(serviceName);
        case "--stop":
          return StopService(serviceName);
        case "--status":
          return GetServiceStatus(serviceName);
        case "--help":
        case "-h":
        case "/?":
          PrintHelp(serviceName);
          return 0;
      }
    }

    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings {
      Args = args,
      ContentRootPath = basePath
    });
    if (OperatingSystem.IsWindows()) {
      builder.Services.AddWindowsService(options => {
        options.ServiceName = serviceName;
      });
    }

    if (OperatingSystem.IsLinux())
      builder.Services.AddSystemd();

    var logsPath = Path.Combine(basePath, "logs");
    Directory.CreateDirectory(logsPath);
    builder.Logging.AddConsoleLogger(logsPath);
    builder.Services.AddSingleton(configurationFileService);
    builder.Services.AddSingleton<BackupOrchestrator>();
    builder.Services.AddSingleton<LibraryOrchestrator>();
    builder.Services.AddSingleton<ScheduledBackupRunner>();
    builder.Services.AddHostedService<BackupSchedulerBackgroundService>();
    builder.Build().Run();
    return 0;
  }

  private static void PrintHelp(string serviceName) =>
    Console.WriteLine(
      $"""
      MaksIT.LTO.Backup.Service - scheduled LTO library backups

      Usage: MaksIT.LTO.Backup.Service [command]

      Commands:
        --install, -i    Install the service (Windows SCM or systemd)
        --uninstall, -u  Uninstall the service
        --start          Start the service
        --stop           Stop the service
        --status         Query service status
        --help, -h       Show this help message

      Service Name: {serviceName}
      Config File:  {UserSettingsPath.Get(ConfigurationFileService.ProductFolder)}

      Note: Install/uninstall typically require administrator / root privileges.
      Scheduling runs only when Topology = TapeLibrary.
      """);

  private static int GetServiceStatus(string serviceName) {
    if (OperatingSystem.IsWindows())
      return RunScCommand($"query \"{serviceName}\"");

    if (OperatingSystem.IsLinux())
      return RunSystemctl($"status {serviceName} --no-pager");

    return 1;
  }

  private static int StartService(string serviceName) {
    if (OperatingSystem.IsWindows())
      return RunScCommand($"start \"{serviceName}\"");

    if (OperatingSystem.IsLinux())
      return RunSystemctl($"start {serviceName}");

    return 1;
  }

  private static int StopService(string serviceName) {
    if (OperatingSystem.IsWindows())
      return RunScCommand($"stop \"{serviceName}\"");

    if (OperatingSystem.IsLinux())
      return RunSystemctl($"stop {serviceName}");

    return 1;
  }

  private static int InstallService(string serviceName, string exePath, string description) {
    if (!HostServiceRegistration.IsValidServiceName(serviceName)) {
      Console.WriteLine($"Invalid service name '{serviceName}'. Use letters, digits, '.', '-', '_' or '@'.");
      return 1;
    }

    if (!File.Exists(exePath)) {
      Console.WriteLine($"Executable not found: {exePath}");
      return 1;
    }

    if (OperatingSystem.IsWindows())
      return InstallWindowsService(serviceName, exePath, description);

    if (OperatingSystem.IsLinux())
      return InstallLinuxService(serviceName, exePath, description);

    Console.WriteLine("Service install is only supported on Windows and Linux.");
    return 1;
  }

  private static int UninstallService(string serviceName) {
    if (OperatingSystem.IsWindows())
      return UninstallWindowsService(serviceName);

    if (OperatingSystem.IsLinux())
      return UninstallLinuxService(serviceName);

    Console.WriteLine("Service uninstall is only supported on Windows and Linux.");
    return 1;
  }

  private static int InstallWindowsService(string serviceName, string exePath, string description) {
    Console.WriteLine($"Installing Windows service '{serviceName}'...");
    var exitCode = RunScCommand(HostServiceRegistration.FormatWindowsCreateArguments(serviceName, exePath));
    if (exitCode != 0) {
      Console.WriteLine("Failed to create service. Run from an elevated prompt.");
      return exitCode;
    }

    RunScCommand(HostServiceRegistration.FormatWindowsDescriptionArguments(serviceName, description));
    Console.WriteLine($"Service '{serviceName}' installed successfully.");
    return 0;
  }

  private static int UninstallWindowsService(string serviceName) {
    Console.WriteLine($"Stopping service '{serviceName}'...");
    RunScCommand($"stop \"{serviceName}\"");
    Console.WriteLine($"Uninstalling service '{serviceName}'...");
    return RunScCommand($"delete \"{serviceName}\"");
  }

  private static int InstallLinuxService(string serviceName, string exePath, string description) {
    var unitPath = HostServiceRegistration.GetSystemdUnitPath(serviceName);
    Console.WriteLine($"Installing systemd unit '{unitPath}'...");
    EnsureUnixExecutable(exePath);

    try {
      File.WriteAllText(unitPath, HostServiceRegistration.FormatSystemdUnit(serviceName, exePath, description));
    }
    catch (Exception ex) {
      Console.WriteLine($"Failed to write unit file (need root?): {ex.Message}");
      return 1;
    }

    var reload = RunSystemctl("daemon-reload");
    if (reload != 0)
      return reload;

    var enable = RunSystemctl($"enable {serviceName}");
    if (enable != 0)
      return enable;

    Console.WriteLine($"Service '{serviceName}' installed. Use --start to start it.");
    return 0;
  }

  private static int UninstallLinuxService(string serviceName) {
    RunSystemctl($"stop {serviceName}");
    RunSystemctl($"disable {serviceName}");
    var unitPath = HostServiceRegistration.GetSystemdUnitPath(serviceName);
    try {
      if (File.Exists(unitPath))
        File.Delete(unitPath);
    }
    catch (Exception ex) {
      Console.WriteLine($"Failed to remove unit file (need root?): {ex.Message}");
      return 1;
    }

    return RunSystemctl("daemon-reload");
  }

  private static void EnsureUnixExecutable(string exePath) {
    if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
      return;

    try {
      var mode = File.GetUnixFileMode(exePath);
      File.SetUnixFileMode(
        exePath,
        mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }
    catch (Exception ex) {
      Console.WriteLine($"Warning: could not set execute bit on '{exePath}': {ex.Message}");
    }
  }

  private static int RunScCommand(string arguments) =>
    RunProcess(Path.Combine(Environment.SystemDirectory, "sc.exe"), arguments);

  private static int RunSystemctl(string arguments) =>
    RunProcess("systemctl", arguments);

  private static int RunProcess(string fileName, string arguments) {
    try {
      using var process = Process.Start(new ProcessStartInfo {
        FileName = fileName,
        Arguments = arguments,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      });

      if (process is null) {
        Console.WriteLine($"Failed to start {fileName}.");
        return 1;
      }

      Console.Write(process.StandardOutput.ReadToEnd());
      Console.Write(process.StandardError.ReadToEnd());
      process.WaitForExit();
      return process.ExitCode;
    }
    catch (Exception ex) {
      Console.WriteLine($"Error executing {fileName}: {ex.Message}");
      return 1;
    }
  }
}
