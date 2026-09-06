using CommunityToolkit.Mvvm.Input;
using MaksIT.LTO.Backup.Shared;
using MaksIT.LTO.Backup.Shared.Models;


namespace MaksIT.LTO.Backup.UI.ViewModels;

public partial class MainViewModel {
  private void RefreshBackupLists() {
    var selectedName = SelectedBackup?.Name ?? SelectedSettingsBackup?.Name;
    BackupJobs.Clear();
    foreach (var backup in _backupOrchestrator.Backups)
      BackupJobs.Add(backup);

    if (BackupJobs.Count == 0) {
      SelectedBackup = null;
      SelectedSettingsBackup = null;
      return;
    }

    var match = BackupJobs.FirstOrDefault(item => string.Equals(item.Name, selectedName, StringComparison.OrdinalIgnoreCase));
    SelectedBackup = match ?? BackupJobs[0];
    SelectedSettingsBackup = SelectedBackup;
    UpdateJobSummary(SelectedBackup);
  }

  private void UpdateJobSummary(BackupItem? backup) {
    if (backup is null) {
      JobSummaryText = "Select a backup job to see source, destination, LTO generation, and barcode.";
      return;
    }

    JobSummaryText = $"{backup.LTOGen} · barcode {backup.Barcode} · source {GetFolderPath(backup.Source)} → destination {GetFolderPath(backup.Destination)}";
  }

  private void ApplySelectedLibraryDrive(LoadedCartridgeSnapshot selected) {
    _backupOrchestrator.SelectedLibraryDriveSlot = selected.DriveSlot;
    _loadingDriveSelection = true;
    try {
      SelectedOccupiedDrive = OccupiedLibraryDrives.FirstOrDefault(item => item.DriveSlot == selected.DriveSlot) ?? selected;
    }
    finally {
      _loadingDriveSelection = false;
    }

    RefreshLoadedCartridgeSummaries();
  }

  private void RefreshLoadedCartridgeSummaries() {
    var loaded = _backupOrchestrator.GetLoadedCartridge();
    var isTapeLibrary = _configurationFileService.Current.IsTapeLibrary;
    IsCartridgeLoaded = loaded.IsLoaded;
    BackupLoadedSummaryText = loaded.IsLoaded ? (loaded.Summary ?? "Cartridge loaded.") : "No cartridge loaded.";
    BackupLoadedHintText = loaded.Hint ?? (loaded.IsLoaded
      ? "Ready for backup/restore/erase on the selected job."
      : "Load media first (Drive tab for standalone, or Library tab move into a drive bay).");

    if (!isTapeLibrary)
      return;

    LibraryLoadedSummaryText = loaded.IsLoaded ? (loaded.Summary ?? "Cartridge in drive.") : "No cartridge in a drive bay.";
    LibraryLoadedHintText = loaded.Hint ?? "Move a cartridge from a storage slot into a drive bay, then select the drive if more than one is loaded.";
    if (loaded.IsLoaded && !string.IsNullOrWhiteSpace(loaded.Barcode))
      LibraryBarcodeText = loaded.Barcode;
  }

  private void UpdateLoadedCartridgeUi() {
    PopulateActiveDriveCombos();
    RefreshLoadedCartridgeSummaries();
  }

  private void PopulateActiveDriveCombos() {
    if (!_configurationFileService.Current.IsTapeLibrary) {
      OccupiedLibraryDrives.Clear();
      SelectedOccupiedDrive = null;
      return;
    }

    var occupied = _backupOrchestrator.GetOccupiedLibraryDrives();
    var previousSlot = _backupOrchestrator.SelectedLibraryDriveSlot ?? SelectedOccupiedDrive?.DriveSlot;
    _loadingDriveSelection = true;
    try {
      OccupiedLibraryDrives.Clear();
      foreach (var drive in occupied)
        OccupiedLibraryDrives.Add(drive);

      LoadedCartridgeSnapshot? selected = null;
      if (previousSlot.HasValue)
        selected = occupied.FirstOrDefault(drive => drive.DriveSlot == previousSlot.Value);

      selected ??= occupied.Count == 1 ? occupied[0] : null;
      SelectedOccupiedDrive = selected;
      _backupOrchestrator.SelectedLibraryDriveSlot = selected?.DriveSlot;
    }
    finally {
      _loadingDriveSelection = false;
    }
  }

  private void LoadSettingsIntoUi() {
    _loadingSettings = true;
    try {
      var current = _configurationFileService.Current;
      DeviceMode = MatchOption(DeviceModeOptions, current.DeviceMode) ?? current.DeviceMode;
      SelectedTopology = MatchOption(TopologyOptions, current.Topology) ?? current.Topology;
      TapePathText = current.TapePath;
      LibraryPathText = current.LibraryPath;
      WriteDelayText = current.WriteDelay.ToString();
      FailFastChecksum = current.FailFastOnChecksumMismatch;
      EmulatorDataDirText = current.Emulator.DataDirectory;
      EmulatorSlotCountText = current.Emulator.Library.SlotCount.ToString();
      EmulatorDriveCountText = current.Emulator.Library.DriveCount.ToString();
      EmulatorIePortCountText = current.Emulator.Library.IePortCount.ToString();
      ServiceNameText = current.Service.ServiceName;
      ServiceDescriptionText = current.Service.Description;
      ServiceExePathSettingText = current.Service.ExecutablePath;
      LoadSelectedBackupIntoUi(SelectedSettingsBackup);
      RefreshAggregationLists();
      UpdateTopologyUi();
    }
    finally {
      _loadingSettings = false;
    }
  }

  private void UpdateTopologyUi() {
    OnPropertyChanged(nameof(IsTapeLibraryUi));
    OnPropertyChanged(nameof(IsStandaloneDriveUi));
    OnPropertyChanged(nameof(IsSavedTapeLibrary));
    if (IsTapeLibraryUi) {
      RefreshAvailableTapeBarcodes();
      RefreshAggregationLists();
    }

    UpdateLoadedCartridgeUi();
  }

  private void LoadSelectedBackupIntoUi(BackupItem? backup) {
    if (backup is null) {
      BackupNameText = string.Empty;
      BackupBarcodeText = string.Empty;
      BackupDisabled = false;
      BackupLtoGen = "LTO5";
      BackupWriteMode = BackupWriteModes.Overwrite.Name;
      BackupSourcePathText = string.Empty;
      BackupDestinationPathText = string.Empty;
      TapeRows.Clear();
      RefreshAvailableTapeBarcodes();
      return;
    }

    BackupNameText = backup.Name;
    BackupBarcodeText = backup.Barcode;
    BackupDisabled = backup.Disabled;
    BackupLtoGen = MatchOption(LtoGenerationOptions, backup.LTOGen) ?? backup.LTOGen;
    BackupWriteMode = MatchOption(WriteModeOptions, string.IsNullOrWhiteSpace(backup.WriteMode) ? BackupWriteModes.Overwrite.Name : backup.WriteMode)
      ?? BackupWriteModes.Overwrite.Name;
    BackupSourcePathText = GetFolderPath(backup.Source);
    BackupDestinationPathText = GetFolderPath(backup.Destination);
    TapeRows.Clear();
    foreach (var tape in backup.Tapes)
      TapeRows.Add(TapeRow.FromModel(tape));

    RefreshAvailableTapeBarcodes();
  }

  private void ClearScheduleUi() {
    foreach (var month in MonthOptions)
      month.IsChecked = false;

    foreach (var weekday in WeekdayOptions)
      weekday.IsChecked = false;

    RunTimes.Clear();
    MinIntervalMinutesText = "10";
    NewRunTimeText = "00:00";
  }

  private void LoadScheduleIntoUi(BackupSchedule? schedule) {
    ClearScheduleUi();
    if (schedule is null)
      return;

    foreach (var month in MonthOptions)
      month.IsChecked = schedule.RunMonth.Any(name => string.Equals(name, month.Name, StringComparison.OrdinalIgnoreCase));

    foreach (var weekday in WeekdayOptions)
      weekday.IsChecked = schedule.RunWeekday.Any(name => string.Equals(name, weekday.Name, StringComparison.OrdinalIgnoreCase));

    foreach (var time in schedule.RunTime)
      RunTimes.Add(time);

    MinIntervalMinutesText = schedule.MinIntervalMinutes.ToString();
  }

  private BackupSchedule ReadScheduleFromUi() {
    var schedule = new BackupSchedule {
      RunMonth = MonthOptions.Where(option => option.IsChecked).Select(option => option.Name).ToList(),
      RunWeekday = WeekdayOptions.Where(option => option.IsChecked).Select(option => option.Name).ToList(),
      RunTime = RunTimes.ToList()
    };
    if (!int.TryParse(MinIntervalMinutesText?.Trim(), out var minutes) || minutes < 0)
      throw new InvalidOperationException("Minimum interval must be a non-negative integer.");

    schedule.MinIntervalMinutes = minutes;
    return schedule;
  }

  [RelayCommand]
  private void AddTape() {
    if (SelectedAvailableTape is null) {
      Log("Select an available library barcode first.");
      return;
    }

    var barcode = SelectedAvailableTape.Barcode;
    if (TapeRows.Any(row => string.Equals(row.Barcode, barcode, StringComparison.OrdinalIgnoreCase))) {
      Log("Tape '" + barcode + "' is already in the list.");
      return;
    }

    TapeRows.Add(new TapeRow { Barcode = barcode });
    RefreshAvailableTapeBarcodes();
  }

  [RelayCommand]
  private void RemoveTape() {
    if (SelectedTapeRow is null)
      return;

    TapeRows.Remove(SelectedTapeRow);
    RefreshAvailableTapeBarcodes();
  }

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private void RefreshAvailableTapes() =>
    Execute("Refresh available tapes", RefreshAvailableTapeBarcodes);

  private void RefreshAvailableTapeBarcodes() {
    if (!_configurationFileService.Current.IsTapeLibrary && !IsTapeLibraryUi) {
      AvailableTapes.Clear();
      return;
    }

    HashSet<string> associated;
    try {
      associated = CollectAssociatedBarcodes();
    }
    catch {
      associated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    List<AvailableTapeOption> options;
    try {
      options = _libraryOrchestrator.Inventory()
        .Where(element => !string.IsNullOrWhiteSpace(element.Barcode))
        .GroupBy(element => element.Barcode!.Trim(), StringComparer.OrdinalIgnoreCase)
        .Where(group => !associated.Contains(group.Key))
        .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
        .Select(group => {
          var element = group.First();
          return new AvailableTapeOption {
            Barcode = element.Barcode!.Trim(),
            Display = $"{element.Barcode.Trim()} · {element.ElementType} {element.Slot}"
              + (string.IsNullOrWhiteSpace(element.CartridgeId) ? "" : (" · " + element.CartridgeId))
          };
        })
        .ToList();
    }
    catch (Exception ex) {
      AvailableTapes.Clear();
      Log("Could not load library inventory for tape picker: " + ex.Message);
      return;
    }

    AvailableTapes.Clear();
    foreach (var option in options)
      AvailableTapes.Add(option);

    SelectedAvailableTape = null;
  }

  private HashSet<string> CollectAssociatedBarcodes() {
    var associated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var editingName = SelectedSettingsBackup?.Name;
    foreach (var backup in _configurationFileService.Current.Backups) {
      var barcodes = string.IsNullOrWhiteSpace(editingName) || !string.Equals(backup.Name, editingName, StringComparison.OrdinalIgnoreCase)
        ? backup.Tapes.Select(tape => tape.Barcode)
        : TapeRows.Select(row => row.Barcode);
      foreach (var barcode in barcodes) {
        if (!string.IsNullOrWhiteSpace(barcode))
          associated.Add(barcode.Trim());
      }
    }

    foreach (var row in TapeRows) {
      if (!string.IsNullOrWhiteSpace(row.Barcode))
        associated.Add(row.Barcode.Trim());
    }

    return associated;
  }

  [RelayCommand]
  private void AddRunTime() {
    var raw = NewRunTimeText?.Trim() ?? string.Empty;
    if (!TimeOnly.TryParse(raw, out var parsed)) {
      Log("Run time must be HH:mm (UTC).");
      return;
    }

    var value = parsed.ToString("HH:mm");
    if (!RunTimes.Contains(value))
      RunTimes.Add(value);

    NewRunTimeText = "00:00";
  }

  [RelayCommand]
  private void RemoveRunTime() {
    if (SelectedRunTime is null)
      return;

    RunTimes.Remove(SelectedRunTime);
  }

  private static string? MatchOption(IReadOnlyList<string> options, string? value) =>
    options.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase));

  private static string GetFolderPath(WorkingFolder folder) {
    if (folder.LocalPath is not null)
      return folder.LocalPath.Path;

    return folder.RemotePath?.Path ?? string.Empty;
  }

  private void ApplyFolderPath(WorkingFolder folder, string path) {
    if (folder.RemotePath is not null) {
      folder.RemotePath.Path = path;
      return;
    }

    folder.LocalPath ??= new LocalPath { Path = path };
    folder.LocalPath.Path = path;
    folder.RemotePath = null;
  }

  private Configuration BuildConfigurationFromUi() {
    var current = _configurationFileService.Current;
    current.DeviceMode = DeviceMode;
    current.Topology = SelectedTopology;
    current.TapePath = TapePathText?.Trim() ?? string.Empty;
    current.LibraryPath = LibraryPathText?.Trim() ?? string.Empty;
    if (!int.TryParse(WriteDelayText?.Trim(), out var writeDelay) || writeDelay < 0)
      throw new InvalidOperationException("Write delay must be a non-negative integer.");

    current.WriteDelay = writeDelay;
    current.FailFastOnChecksumMismatch = FailFastChecksum;
    current.Emulator.DataDirectory = EmulatorDataDirText?.Trim() ?? string.Empty;
    if (!int.TryParse(EmulatorSlotCountText?.Trim(), out var slots) || slots < 1)
      throw new InvalidOperationException("Emulator slots must be a positive integer.");
    if (!int.TryParse(EmulatorDriveCountText?.Trim(), out var drives) || drives < 1)
      throw new InvalidOperationException("Emulator drives must be a positive integer.");
    if (!int.TryParse(EmulatorIePortCountText?.Trim(), out var iePorts) || iePorts < 0)
      throw new InvalidOperationException("Emulator I/E ports must be a non-negative integer.");

    current.Emulator.Library.SlotCount = slots;
    current.Emulator.Library.DriveCount = drives;
    current.Emulator.Library.IePortCount = iePorts;
    current.Service.ServiceName = ServiceNameText?.Trim() ?? "MaksIT.LTO.Backup";
    current.Service.Description = ServiceDescriptionText?.Trim() ?? string.Empty;
    current.Service.ExecutablePath = ServiceExePathSettingText?.Trim() ?? string.Empty;
    _serviceManager = new HostServiceManager(current.Service.ServiceName);
    if (SelectedSettingsBackup is BackupItem backup) {
      backup.Name = BackupNameText?.Trim() ?? string.Empty;
      backup.Barcode = BackupBarcodeText?.Trim() ?? string.Empty;
      backup.Disabled = BackupDisabled;
      backup.LTOGen = BackupLtoGen;
      backup.WriteMode = string.IsNullOrWhiteSpace(BackupWriteMode) ? BackupWriteModes.Overwrite.Name : BackupWriteMode;
      backup.Tapes = TapeRows
        .Where(row => !string.IsNullOrWhiteSpace(row.Barcode))
        .Select(row => row.ToModel())
        .ToList();
      ApplyFolderPath(backup.Source, BackupSourcePathText?.Trim() ?? string.Empty);
      ApplyFolderPath(backup.Destination, BackupDestinationPathText?.Trim() ?? string.Empty);
    }

    ApplyAggregationFromUi(current);
    return current;
  }

  private void RefreshAggregationLists() {
    var selectedName = SelectedAggregation?.Name ?? _configurationFileService.Current.Aggregations.FirstOrDefault()?.Name;
    Aggregations.Clear();
    foreach (var aggregation in _configurationFileService.Current.Aggregations)
      Aggregations.Add(aggregation);

    SelectedAggregationAddJob = null;
    if (Aggregations.Count == 0) {
      _lastAggregationSelection = null;
      LoadSelectedAggregationIntoUi(null);
      return;
    }

    var selected = Aggregations.FirstOrDefault(item => string.Equals(item.Name, selectedName, StringComparison.OrdinalIgnoreCase))
      ?? Aggregations[0];
    var restoreLoading = _loadingSettings;
    _loadingSettings = true;
    try {
      SelectedAggregation = selected;
    }
    finally {
      _loadingSettings = restoreLoading;
    }

    _lastAggregationSelection = selected;
    LoadSelectedAggregationIntoUi(selected);
  }

  private void LoadSelectedAggregationIntoUi(BackupAggregation? aggregation) {
    if (aggregation is null) {
      AggregationNameText = string.Empty;
      AggregationDisabled = false;
      AggregationJobNames.Clear();
      ClearScheduleUi();
      LastRunUtcText = "Last wave run (UTC): (never)";
      return;
    }

    AggregationNameText = aggregation.Name;
    AggregationDisabled = aggregation.Disabled;
    AggregationJobNames.Clear();
    foreach (var jobName in aggregation.JobNames)
      AggregationJobNames.Add(jobName);

    LoadScheduleIntoUi(aggregation.Schedule ?? new BackupSchedule());
    LastRunUtcText = aggregation.LastRunUtc.HasValue
      ? $"Last wave run (UTC): {aggregation.LastRunUtc:u}"
      : "Last wave run (UTC): (never)";
  }

  private void ApplyAggregationFromUi(Configuration config, BackupAggregation? previous = null) {
    var aggregation = previous ?? SelectedAggregation;
    if (aggregation is null)
      return;

    aggregation.Name = AggregationNameText?.Trim() ?? aggregation.Name;
    aggregation.Disabled = AggregationDisabled;
    aggregation.JobNames = AggregationJobNames.ToList();
    aggregation.Schedule = ReadScheduleFromUi();
  }

  [RelayCommand]
  private void AddAggregation() {
    ApplyAggregationFromUi(_configurationFileService.Current);
    var aggregation = new BackupAggregation {
      Name = $"Wave {_configurationFileService.Current.Aggregations.Count + 1}",
      Schedule = new BackupSchedule {
        MinIntervalMinutes = 60,
        RunTime = ["00:00"]
      }
    };
    _configurationFileService.Current.Aggregations.Add(aggregation);
    RefreshAggregationLists();
    SelectedAggregation = aggregation;
  }

  [RelayCommand]
  private void RemoveAggregation() {
    if (SelectedAggregation is null)
      return;

    _configurationFileService.Current.Aggregations.Remove(SelectedAggregation);
    _lastAggregationSelection = null;
    RefreshAggregationLists();
  }

  [RelayCommand]
  private void AggregationAddJob() {
    if (SelectedAggregationAddJob is null)
      return;

    var name = SelectedAggregationAddJob.Name;
    if (AggregationJobNames.Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase))) {
      Log("Job '" + name + "' is already in this wave.");
      return;
    }

    AggregationJobNames.Add(name);
  }

  [RelayCommand]
  private void AggregationRemoveJob() {
    if (SelectedAggregationJobName is null)
      return;

    AggregationJobNames.Remove(SelectedAggregationJobName);
  }

  [RelayCommand]
  private void AggregationMoveJobUp() {
    if (SelectedAggregationJobName is null)
      return;

    var index = AggregationJobNames.IndexOf(SelectedAggregationJobName);
    if (index <= 0)
      return;

    var name = SelectedAggregationJobName;
    AggregationJobNames.RemoveAt(index);
    AggregationJobNames.Insert(index - 1, name);
    SelectedAggregationJobName = name;
  }

  [RelayCommand]
  private void AggregationMoveJobDown() {
    if (SelectedAggregationJobName is null)
      return;

    var index = AggregationJobNames.IndexOf(SelectedAggregationJobName);
    if (index < 0 || index >= AggregationJobNames.Count - 1)
      return;

    var name = SelectedAggregationJobName;
    AggregationJobNames.RemoveAt(index);
    AggregationJobNames.Insert(index + 1, name);
    SelectedAggregationJobName = name;
  }

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private void SaveConfiguration() =>
    Execute("Save configuration", SaveConfigurationFromUi);

  private void SaveConfigurationFromUi() {
    var configuration = BuildConfigurationFromUi();
    _configurationFileService.Save(configuration);
    RefreshBackupLists();
    LoadSettingsIntoUi();
    UpdateJobSummary(SelectedBackup);
    RefreshMonitor(silent: true);
    RefreshAvailableTapeBarcodes();
    RefreshAggregationLists();
    Log("Configuration saved to " + _configurationFileService.FilePath);
  }

  [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
  private void ReloadConfiguration() =>
    Execute("Reload configuration", () => {
      _configurationFileService.Reload();
      RefreshBackupLists();
      LoadSettingsIntoUi();
      UpdateJobSummary(SelectedBackup);
      RefreshMonitor(silent: true);
      RefreshAvailableTapeBarcodes();
      RefreshAggregationLists();
      Log("Configuration reloaded from " + _configurationFileService.FilePath);
    });
}
