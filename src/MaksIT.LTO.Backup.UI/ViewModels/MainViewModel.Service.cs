using MaksIT.LTO.Backup.Shared;
using CommunityToolkit.Mvvm.Input;


namespace MaksIT.LTO.Backup.UI.ViewModels;

public partial class MainViewModel {
  private string? ResolvedServiceExecutable() {
    var current = _configurationFileService.Current;
    var overridePath = ServiceExePathSettingText?.Trim();
    if (!string.IsNullOrWhiteSpace(overridePath))
      current.Service.ExecutablePath = overridePath;

    return HostServiceManager.ResolveExecutablePath(current.Service);
  }

  private void RefreshServiceStatus() {
    var path = ResolvedServiceExecutable();
    ServiceExePathText = path is null
      ? "Executable: (not found — build/publish MaksIT.LTO.Backup.Service next to the UI, or set path in Service settings)"
      : "Executable: " + path;
    var status = _serviceManager.GetStatus(path);
    ServiceStatusText = $"Status: {status}";
  }

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private void ServiceRefresh() =>
    Execute("Refresh service status", RefreshServiceStatus);

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private void ServiceInstall() =>
    Execute("Install service", () => {
      var executablePath = ResolvedServiceExecutable() ?? throw new InvalidOperationException("Worker executable not found.");
      var result = _serviceManager.Register(executablePath);
      Log(result.Message);
      if (!result.Success)
        throw new InvalidOperationException(result.Message);

      RefreshServiceStatus();
    });

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private void ServiceUninstall() =>
    Execute("Uninstall service", () => {
      var result = _serviceManager.Unregister(ResolvedServiceExecutable());
      Log(result.Message);
      if (!result.Success)
        throw new InvalidOperationException(result.Message);

      RefreshServiceStatus();
    });

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private void ServiceStart() =>
    Execute("Start service", () => {
      var result = _serviceManager.Start(ResolvedServiceExecutable());
      Log(result.Message);
      if (!result.Success)
        throw new InvalidOperationException(result.Message);

      RefreshServiceStatus();
    });

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private void ServiceStop() =>
    Execute("Stop service", () => {
      var result = _serviceManager.Stop(ResolvedServiceExecutable());
      Log(result.Message);
      if (!result.Success)
        throw new InvalidOperationException(result.Message);

      RefreshServiceStatus();
    });
}
