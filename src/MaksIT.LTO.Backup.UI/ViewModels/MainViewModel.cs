using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaksIT.LTO.Backup.Shared;
using MaksIT.LTO.Backup.Shared.Models;
using MaksIT.LTO.Core.MassStorage;


namespace MaksIT.LTO.Backup.UI.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable {
  private readonly BackupOrchestrator _backupOrchestrator;
  private readonly LibraryOrchestrator _libraryOrchestrator;
  private readonly ConfigurationFileService _configurationFileService;
  private readonly IDialogService _dialogs;
  private readonly DispatcherTimer _monitorTimer = new();

  private HostServiceManager _serviceManager;
  private bool _loadingSettings;
  private bool _loadingDriveSelection;
  private bool _monitorRefreshInProgress;
  private bool _handlingTopologyChange;
  private BackupAggregation? _lastAggregationSelection;
  private IReadOnlyList<LibraryElementStatus> _libraryElements = [];

  public IReadOnlyList<string> DeviceModeOptions { get; } = [
    DeviceModes.Emulated.Name,
    DeviceModes.Physical.Name
  ];

  public IReadOnlyList<string> TopologyOptions { get; } = [
    DeviceTopologies.StandaloneDrive.Name,
    DeviceTopologies.TapeLibrary.Name
  ];

  public IReadOnlyList<string> LtoGenerationOptions { get; } = [
    "LTO1", "LTO2", "LTO3", "LTO4", "LTO5", "LTO6", "LTO7", "LTO8", "LTO9"
  ];

  public IReadOnlyList<string> WriteModeOptions { get; } = [
    BackupWriteModes.Overwrite.Name,
    BackupWriteModes.Append.Name
  ];

  public IReadOnlyList<MonitorIntervalOption> MonitorIntervalOptions { get; } = [
    new(2), new(5), new(10), new(30)
  ];

  public IReadOnlyList<ScheduleToggleOption> MonthOptions { get; }

  public IReadOnlyList<ScheduleToggleOption> WeekdayOptions { get; }

  public ObservableCollection<BackupItem> BackupJobs { get; } = [];

  public ObservableCollection<BackupAggregation> Aggregations { get; } = [];

  public ObservableCollection<TapeRow> TapeRows { get; } = [];

  public ObservableCollection<AvailableTapeOption> AvailableTapes { get; } = [];

  public ObservableCollection<string> AggregationJobNames { get; } = [];

  public ObservableCollection<string> RunTimes { get; } = [];

  public ObservableCollection<MonitorElementRow> MonitorInventory { get; } = [];

  public ObservableCollection<LibraryElementStatus> LibraryInventory { get; } = [];

  public ObservableCollection<LoadedCartridgeSnapshot> OccupiedLibraryDrives { get; } = [];

  public ObservableCollection<SlotOption> LoadSources { get; } = [];

  public ObservableCollection<SlotOption> LoadDrives { get; } = [];

  public ObservableCollection<SlotOption> UnloadDrives { get; } = [];

  public ObservableCollection<SlotOption> SourceSlots { get; } = [];

  public ObservableCollection<SlotOption> DestinationSlots { get; } = [];

  [ObservableProperty]
  private string _headerStatusText = "Drive and library status will appear here.";

  [ObservableProperty]
  private bool _liveMonitorEnabled;

  [ObservableProperty]
  private MonitorIntervalOption? _selectedMonitorInterval;

  [ObservableProperty]
  private string _monitorDriveModeText = string.Empty;

  [ObservableProperty]
  private string _monitorDriveTopologyText = string.Empty;

  [ObservableProperty]
  private string _monitorDriveStateText = string.Empty;

  [ObservableProperty]
  private string _monitorDrivePathText = string.Empty;

  [ObservableProperty]
  private string _monitorDriveStatusCodeText = string.Empty;

  [ObservableProperty]
  private string _monitorDriveBlockText = string.Empty;

  [ObservableProperty]
  private string _monitorLibraryModeText = string.Empty;

  [ObservableProperty]
  private string _monitorLibrarySummaryText = string.Empty;

  [ObservableProperty]
  private string _monitorLibraryPathText = string.Empty;

  [ObservableProperty]
  private string _monitorLibraryDrivesText = string.Empty;

  [ObservableProperty]
  private string _monitorUpdatedText = string.Empty;

  [ObservableProperty]
  private string _monitorLibraryErrorText = string.Empty;

  [ObservableProperty]
  private string _standaloneBarcodeText = string.Empty;

  [ObservableProperty]
  private string _libraryBarcodeText = string.Empty;

  [ObservableProperty]
  private string _libraryLoadedSummaryText = "No cartridge in a drive bay.";

  [ObservableProperty]
  private string _libraryLoadedHintText = "Move a cartridge from a storage slot into a drive bay (destination 1000+).";

  [ObservableProperty]
  private LoadedCartridgeSnapshot? _selectedOccupiedDrive;

  [ObservableProperty]
  private SlotOption? _selectedLoadSource;

  [ObservableProperty]
  private SlotOption? _selectedLoadDrive;

  [ObservableProperty]
  private SlotOption? _selectedUnloadDrive;

  [ObservableProperty]
  private SlotOption? _selectedSourceSlot;

  [ObservableProperty]
  private SlotOption? _selectedDestinationSlot;

  [ObservableProperty]
  private BackupItem? _selectedBackup;

  [ObservableProperty]
  private string _jobSummaryText = "Select a backup job to see source, destination, LTO generation, and barcode.";

  [ObservableProperty]
  private string _backupLoadedSummaryText = "Checking loaded cartridge…";

  [ObservableProperty]
  private string _backupLoadedHintText = "Backup/restore require a loaded cartridge (standalone drive tape, or library media in a drive bay).";

  [ObservableProperty]
  private bool _isCartridgeLoaded;

  [ObservableProperty]
  private bool _isBusy;

  [ObservableProperty]
  private string _deviceMode = DeviceModes.Emulated.Name;

  [ObservableProperty]
  private string _selectedTopology = DeviceTopologies.StandaloneDrive.Name;

  [ObservableProperty]
  private string _tapePathText = string.Empty;

  [ObservableProperty]
  private string _libraryPathText = string.Empty;

  [ObservableProperty]
  private string _writeDelayText = "100";

  [ObservableProperty]
  private bool _failFastChecksum;

  [ObservableProperty]
  private string _emulatorDataDirText = string.Empty;

  [ObservableProperty]
  private string _emulatorSlotCountText = "24";

  [ObservableProperty]
  private string _emulatorDriveCountText = "2";

  [ObservableProperty]
  private string _emulatorIePortCountText = "1";

  [ObservableProperty]
  private BackupItem? _selectedSettingsBackup;

  [ObservableProperty]
  private bool _backupDisabled;

  [ObservableProperty]
  private string _backupNameText = string.Empty;

  [ObservableProperty]
  private string _backupBarcodeText = string.Empty;

  [ObservableProperty]
  private string _backupLtoGen = "LTO5";

  [ObservableProperty]
  private string _backupWriteMode = BackupWriteModes.Overwrite.Name;

  [ObservableProperty]
  private string _backupSourcePathText = string.Empty;

  [ObservableProperty]
  private string _backupDestinationPathText = string.Empty;

  [ObservableProperty]
  private AvailableTapeOption? _selectedAvailableTape;

  [ObservableProperty]
  private TapeRow? _selectedTapeRow;

  [ObservableProperty]
  private BackupAggregation? _selectedAggregation;

  [ObservableProperty]
  private bool _aggregationDisabled;

  [ObservableProperty]
  private string _aggregationNameText = string.Empty;

  [ObservableProperty]
  private BackupItem? _selectedAggregationAddJob;

  [ObservableProperty]
  private string? _selectedAggregationJobName;

  [ObservableProperty]
  private string _newRunTimeText = "00:00";

  [ObservableProperty]
  private string? _selectedRunTime;

  [ObservableProperty]
  private string _minIntervalMinutesText = "10";

  [ObservableProperty]
  private string _lastRunUtcText = "Last wave run (UTC): (never)";

  [ObservableProperty]
  private string _serviceStatusText = "Status: Unknown";

  [ObservableProperty]
  private string _serviceExePathText = "Executable: (not found)";

  [ObservableProperty]
  private string _serviceNameText = string.Empty;

  [ObservableProperty]
  private string _serviceDescriptionText = string.Empty;

  [ObservableProperty]
  private string _serviceExePathSettingText = string.Empty;

  [ObservableProperty]
  private string _logText = string.Empty;

  public bool IsTapeLibraryUi =>
    DeviceTopologies.Matches(SelectedTopology, DeviceTopologies.TapeLibrary);

  public bool IsStandaloneDriveUi =>
    !IsTapeLibraryUi;

  public bool IsSavedTapeLibrary =>
    _configurationFileService.Current.IsTapeLibrary;

  public bool OperationsEnabled =>
    IsCartridgeLoaded && !IsBusy;

  public MainViewModel(
    BackupOrchestrator backupOrchestrator,
    LibraryOrchestrator libraryOrchestrator,
    ConfigurationFileService configurationFileService,
    IDialogService dialogs
  ) {
    _backupOrchestrator = backupOrchestrator;
    _libraryOrchestrator = libraryOrchestrator;
    _configurationFileService = configurationFileService;
    _dialogs = dialogs;
    _serviceManager = new HostServiceManager(_configurationFileService.Current.Service.ServiceName);

    MonthOptions = [
      new("January", "Jan"), new("February", "Feb"), new("March", "Mar"),
      new("April", "Apr"), new("May", "May"), new("June", "Jun"),
      new("July", "Jul"), new("August", "Aug"), new("September", "Sep"),
      new("October", "Oct"), new("November", "Nov"), new("December", "Dec")
    ];

    WeekdayOptions = [
      new("Monday", "Mon"), new("Tuesday", "Tue"), new("Wednesday", "Wed"),
      new("Thursday", "Thu"), new("Friday", "Fri"), new("Saturday", "Sat"),
      new("Sunday", "Sun")
    ];

    SelectedMonitorInterval = MonitorIntervalOptions.First(option => option.Seconds == 5);
    _monitorTimer.Tick += (_, _) => RefreshMonitor(silent: true);
    ApplyMonitorInterval();

    RefreshBackupLists();
    LoadSettingsIntoUi();
    RefreshServiceStatus();
    RefreshMonitor(silent: true);
  }

  private bool _disposed;

  public void Dispose() {
    if (_disposed)
      return;

    _disposed = true;
    _monitorTimer.Stop();
    GC.SuppressFinalize(this);
  }

  partial void OnLiveMonitorEnabledChanged(bool value) {
    ApplyMonitorInterval();
    if (value) {
      _monitorTimer.Start();
      RefreshMonitor(silent: true);
      return;
    }

    _monitorTimer.Stop();
  }

  partial void OnSelectedMonitorIntervalChanged(MonitorIntervalOption? value) =>
    ApplyMonitorInterval();

  partial void OnIsBusyChanged(bool value) {
    OnPropertyChanged(nameof(OperationsEnabled));
    NotifyIdleCommands();
  }

  partial void OnIsCartridgeLoadedChanged(bool value) {
    OnPropertyChanged(nameof(OperationsEnabled));
    RunBackupCommand.NotifyCanExecuteChanged();
    RunRestoreCommand.NotifyCanExecuteChanged();
    EraseCommand.NotifyCanExecuteChanged();
  }

  partial void OnSelectedTopologyChanged(string value) {
    OnPropertyChanged(nameof(IsTapeLibraryUi));
    OnPropertyChanged(nameof(IsStandaloneDriveUi));
    if (_loadingSettings || _handlingTopologyChange)
      return;

    _ = HandleTopologyChangedAsync();
  }

  partial void OnSelectedBackupChanged(BackupItem? value) {
    if (!_loadingSettings)
      UpdateJobSummary(value);
  }

  partial void OnSelectedSettingsBackupChanged(BackupItem? value) {
    if (!_loadingSettings)
      LoadSelectedBackupIntoUi(value);
  }

  partial void OnSelectedOccupiedDriveChanged(LoadedCartridgeSnapshot? value) {
    if (_loadingDriveSelection || value is null)
      return;

    ApplySelectedLibraryDrive(value);
  }

  partial void OnSelectedAggregationChanged(BackupAggregation? value) {
    if (_loadingSettings)
      return;

    if (value is not null)
      ApplyAggregationFromUi(_configurationFileService.Current, _lastAggregationSelection);

    _lastAggregationSelection = value;
    LoadSelectedAggregationIntoUi(value);
  }

  private async Task HandleTopologyChangedAsync() {
    var wasStandalone = _configurationFileService.Current.IsStandaloneDrive;
    var nowLibrary = IsTapeLibraryUi;
    if (wasStandalone && nowLibrary) {
      var confirmed = await _dialogs.ConfirmAsync(
        "Switch to TapeLibrary?",
        "Switching to TapeLibrary enables library inventory, tape pools, and scheduled aggregations. Current settings will be saved now. Continue?");
      if (!confirmed) {
        RevertTopologyToStandalone();
        return;
      }

      Execute("Save configuration", SaveConfigurationFromUi);
      if (_configurationFileService.Current.IsStandaloneDrive)
        RevertTopologyToStandalone();
      return;
    }

    UpdateTopologyUi();
  }

  private void RevertTopologyToStandalone() {
    _handlingTopologyChange = true;
    try {
      SelectedTopology = DeviceTopologies.StandaloneDrive.Name;
    }
    finally {
      _handlingTopologyChange = false;
    }

    UpdateTopologyUi();
  }

  private void ApplyMonitorInterval() {
    var seconds = SelectedMonitorInterval?.Seconds ?? 5;
    if (seconds <= 0)
      seconds = 5;

    _monitorTimer.Interval = TimeSpan.FromSeconds(seconds);
  }

  private void Log(string message) {
    if (!Dispatcher.UIThread.CheckAccess()) {
      Dispatcher.UIThread.Post(() => Log(message));
      return;
    }

    LogText += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
  }

  private bool CanRunWhenIdle() =>
    !IsBusy;

  private bool CanRunCartridgeOperation() =>
    IsCartridgeLoaded && !IsBusy;

  private void NotifyIdleCommands() {
    RefreshMonitorNowCommand.NotifyCanExecuteChanged();
    LoadTapeCommand.NotifyCanExecuteChanged();
    EjectTapeCommand.NotifyCanExecuteChanged();
    GetStatusCommand.NotifyCanExecuteChanged();
    ReadAttributesCommand.NotifyCanExecuteChanged();
    WriteStandaloneBarcodeCommand.NotifyCanExecuteChanged();
    WriteLibraryBarcodeCommand.NotifyCanExecuteChanged();
    RefreshInventoryCommand.NotifyCanExecuteChanged();
    LoadIntoDriveCommand.NotifyCanExecuteChanged();
    UnloadFromDriveCommand.NotifyCanExecuteChanged();
    MoveMediumCommand.NotifyCanExecuteChanged();
    RunBackupCommand.NotifyCanExecuteChanged();
    RunRestoreCommand.NotifyCanExecuteChanged();
    EraseCommand.NotifyCanExecuteChanged();
    SaveConfigurationCommand.NotifyCanExecuteChanged();
    ReloadConfigurationCommand.NotifyCanExecuteChanged();
    ServiceRefreshCommand.NotifyCanExecuteChanged();
    ServiceInstallCommand.NotifyCanExecuteChanged();
    ServiceUninstallCommand.NotifyCanExecuteChanged();
    ServiceStartCommand.NotifyCanExecuteChanged();
    ServiceStopCommand.NotifyCanExecuteChanged();
    RefreshAvailableTapesCommand.NotifyCanExecuteChanged();
  }

  private void Execute(string action, Action work) {
    if (IsBusy) {
      Log(action + " skipped: another operation is already running.");
      return;
    }

    IsBusy = true;
    try {
      work();
      if (!action.StartsWith("Save", StringComparison.Ordinal) && !action.StartsWith("Reload", StringComparison.Ordinal))
        Log(action + " completed.");
    }
    catch (Exception ex) {
      Log(action + " failed: " + ex.Message);
    }
    finally {
      IsBusy = false;
      RefreshMonitor(silent: true);
    }
  }

  private async Task ExecuteBackgroundAsync(string action, Action work) {
    if (IsBusy) {
      Log(action + " skipped: another operation is already running.");
      return;
    }

    IsBusy = true;
    HeaderStatusText = action + " in progress…";

    Log(action + " started…");
    try {
      await Task.Run(work).ConfigureAwait(true);
      await Dispatcher.UIThread.InvokeAsync(() => Log(action + " completed."));
    }
    catch (Exception ex) {
      await Dispatcher.UIThread.InvokeAsync(() => Log(action + " failed: " + ex.Message));
    }
    finally {
      await Dispatcher.UIThread.InvokeAsync(() => {
        IsBusy = false;
        RefreshMonitor(silent: true);
      });
    }
  }

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private void RefreshMonitorNow() =>
    RefreshMonitor(silent: false);

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private Task LoadTapeAsync() =>
    ExecuteBackgroundAsync("Load tape", _backupOrchestrator.LoadTape);

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private Task EjectTapeAsync() =>
    ExecuteBackgroundAsync("Eject tape", _backupOrchestrator.EjectTape);

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private Task GetStatusAsync() =>
    ExecuteBackgroundAsync("Get status", () => {
      var status = _backupOrchestrator.GetDeviceStatus();
      Dispatcher.UIThread.Post(() => Log($"Status code: {status}"));
    });

  [RelayCommand(CanExecute = nameof(CanRunCartridgeOperation))]
  private Task EraseAsync() {
    var backup = SelectedBackup;
    return ExecuteBackgroundAsync("Tape erase", () => {
      _backupOrchestrator.EnsureCartridgeLoaded();
      if (backup is null)
        throw new InvalidOperationException("Select backup job first.");

      _backupOrchestrator.TapeErase(backup.LTOGen);
    });
  }

  [RelayCommand(CanExecute = nameof(CanRunCartridgeOperation))]
  private Task RunBackupAsync() {
    var backup = SelectedBackup;
    return ExecuteBackgroundAsync("Backup", () => {
      _backupOrchestrator.EnsureCartridgeLoaded();
      if (backup is null)
        throw new InvalidOperationException("Select backup job first.");

      _backupOrchestrator.Backup(backup);
    });
  }

  [RelayCommand(CanExecute = nameof(CanRunCartridgeOperation))]
  private Task RunRestoreAsync() {
    var backup = SelectedBackup;
    return ExecuteBackgroundAsync("Restore", () => {
      _backupOrchestrator.EnsureCartridgeLoaded();
      if (backup is null)
        throw new InvalidOperationException("Select backup job first.");

      _backupOrchestrator.Restore(backup);
    });
  }

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private Task RefreshInventoryAsync() =>
    ExecuteBackgroundAsync("Inventory", () => Dispatcher.UIThread.Post(() => RefreshMonitor(silent: true)));

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private Task LoadIntoDriveAsync() {
    var source = SelectedLoadSource;
    var drive = SelectedLoadDrive;
    return ExecuteBackgroundAsync("Load into drive", () => {
      if (source is null)
        throw new InvalidOperationException("Select a cartridge (occupied slot) to load.");
      if (drive is null)
        throw new InvalidOperationException("Select an empty drive bay.");
      if (!source.IsOccupied)
        throw new InvalidOperationException("Source must contain a cartridge.");
      if (!drive.IsDrive || drive.IsOccupied)
        throw new InvalidOperationException("Destination must be an empty drive bay.");

      _libraryOrchestrator.MoveMedium(source.Slot, drive.Slot);
    });
  }

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private Task UnloadFromDriveAsync() {
    var drive = SelectedUnloadDrive;
    var elements = _libraryElements.ToList();
    return ExecuteBackgroundAsync("Unload from drive", () => {
      if (drive is null)
        throw new InvalidOperationException("Select a drive bay that has a cartridge.");
      if (!drive.IsDrive || !drive.IsOccupied)
        throw new InvalidOperationException("Selected unload source must be an occupied drive bay.");
      if (elements.Count == 0)
        throw new InvalidOperationException("Refresh inventory first.");

      var emptyStorage = elements.FirstOrDefault(element =>
        !string.Equals(element.ElementType, "Drive", StringComparison.OrdinalIgnoreCase)
        && string.IsNullOrWhiteSpace(element.CartridgeId))
        ?? throw new InvalidOperationException("No empty storage slot available for unload.");
      _libraryOrchestrator.MoveMedium(drive.Slot, emptyStorage.Slot);
    });
  }

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private Task MoveMediumAsync() {
    var source = SelectedSourceSlot;
    var destination = SelectedDestinationSlot;
    return ExecuteBackgroundAsync("Move medium", () => {
      if (source is null)
        throw new InvalidOperationException("Select a source slot.");
      if (destination is null)
        throw new InvalidOperationException("Select a destination slot.");

      _libraryOrchestrator.MoveMedium(source.Slot, destination.Slot);
    });
  }

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private Task ReadAttributesAsync() =>
    ExecuteBackgroundAsync("Read attributes", () => {
      _backupOrchestrator.EnsureCartridgeLoaded();
      var lines = _backupOrchestrator.ReadCartridgeMemory()
        .Select(attribute => $"(0x{attribute.Address:X4}) {attribute.Name}: {attribute.GetValueAsString()}")
        .ToList();
      Dispatcher.UIThread.Post(() => {
        foreach (var line in lines)
          Log(line);
      });
    });

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private Task WriteStandaloneBarcodeAsync() {
    var barcode = StandaloneBarcodeText ?? string.Empty;
    var isLibrary = _configurationFileService.Current.IsTapeLibrary;
    return ExecuteBackgroundAsync("Write barcode", () => {
      if (isLibrary)
        throw new InvalidOperationException("Use the Library tab to assign barcode for a loaded library cartridge.");

      _backupOrchestrator.WriteBarcode(barcode);
    });
  }

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private Task WriteLibraryBarcodeAsync() {
    var barcode = LibraryBarcodeText ?? string.Empty;
    var isLibrary = _configurationFileService.Current.IsTapeLibrary;
    return ExecuteBackgroundAsync("Assign barcode", () => {
      if (!isLibrary)
        throw new InvalidOperationException("Library barcode assignment requires Topology = TapeLibrary.");

      _backupOrchestrator.EnsureCartridgeLoaded();
      _backupOrchestrator.WriteBarcode(barcode);
    });
  }
}
