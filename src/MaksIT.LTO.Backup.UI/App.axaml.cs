using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.ApplicationLifetimes;
using MaksIT.Core.Logging;
using MaksIT.LTO.Backup.Shared;


namespace MaksIT.LTO.Backup.UI;

public partial class App : Application {
  private IHost? _host;

  public override void Initialize() =>
    AvaloniaXamlLoader.Load(this);

  public override void OnFrameworkInitializationCompleted() {
    var basePath = AppContext.BaseDirectory;
    var configurationPath = Path.Combine(basePath, "configuration.json");

    _host = Host.CreateDefaultBuilder()
      .ConfigureAppConfiguration(builder => {
        builder.SetBasePath(basePath);
        builder.AddJsonFile("configuration.json", optional: false, reloadOnChange: true);
      })
      .ConfigureServices((_, services) => {
        services.AddSingleton(_ => new ConfigurationFileService(configurationPath));
        services.AddSingleton<BackupOrchestrator>();
        services.AddSingleton<LibraryOrchestrator>();
        services.AddSingleton<ScheduledBackupRunner>();
        services.AddSingleton<MainWindow>();
      })
      .ConfigureLogging((_, logging) => {
        logging.AddConsoleLogger(Path.Combine(basePath, "logs"));
      })
      .Build();

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
      desktop.MainWindow = _host.Services.GetRequiredService<MainWindow>();
      desktop.ShutdownRequested += async (_, _) => {
        if (_host is null)
          return;

        await _host.StopAsync();
        _host.Dispose();
        _host = null;
      };
    }

    base.OnFrameworkInitializationCompleted();
  }
}
