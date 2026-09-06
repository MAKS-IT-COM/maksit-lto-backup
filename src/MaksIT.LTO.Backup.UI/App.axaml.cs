using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.ApplicationLifetimes;
using MaksIT.Core.Logging;
using MaksIT.LTO.Backup.Shared;
using MaksIT.LTO.Backup.UI.ViewModels;


namespace MaksIT.LTO.Backup.UI;

public partial class App : Application {
  private IHost? _host;

  public override void Initialize() =>
    AvaloniaXamlLoader.Load(this);

  public override void OnFrameworkInitializationCompleted() {
    var basePath = AppContext.BaseDirectory;
    var userSettings = UserSettingsPath.Get(ConfigurationFileService.ProductFolder);

    _host = Host.CreateDefaultBuilder()
      .ConfigureAppConfiguration(builder => {
        builder.SetBasePath(basePath);
        builder.AddJsonFile("configuration.json", optional: false, reloadOnChange: true);
        if (File.Exists(userSettings))
          builder.AddJsonFile(userSettings, optional: true, reloadOnChange: true);
      })
      .ConfigureServices((_, services) => {
        services.AddSingleton(_ => new ConfigurationFileService());
        services.AddSingleton<BackupOrchestrator>();
        services.AddSingleton<LibraryOrchestrator>();
        services.AddSingleton<ScheduledBackupRunner>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<MainViewModel>();
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

        _host.Services.GetRequiredService<MainViewModel>().Dispose();
        await _host.StopAsync();
        _host.Dispose();
        _host = null;
      };
    }

    base.OnFrameworkInitializationCompleted();
  }
}
