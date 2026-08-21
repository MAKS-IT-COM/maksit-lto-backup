using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MaksIT.LTO.Backup.Shared;
using MaksIT.LTO.Backup.Shared.Models;
using MaksIT.LTO.Core.MassStorage;


namespace MaksIT.LTO.Backup.UI;

public partial class MainWindow : Window {
  private sealed class TapeRow {
    public string Barcode { get; set; } = string.Empty;

    public DateTimeOffset? LastUsedUtc { get; set; }

    public string LastUsedUtcDisplay => LastUsedUtc?.ToString("u") ?? "(never)";

    public static TapeRow FromModel(BackupTape tape) => new TapeRow {
      Barcode = tape.Barcode,
      LastUsedUtc = tape.LastUsedUtc
    };

    public BackupTape ToModel() => new BackupTape {
      Barcode = Barcode.Trim(),
      LastUsedUtc = LastUsedUtc
    };
  }

  private sealed class AvailableTapeOption {
    public required string Barcode { get; init; }

    public required string Display { get; init; }
  }

  private sealed class MonitorElementRow {
    public int Slot { get; init; }

    public required string ElementType { get; init; }

    public required string Occupied { get; init; }

    public string? Barcode { get; init; }

    public string? CartridgeId { get; init; }
  }

  private sealed class SlotOption {
    public int Slot { get; init; }

    public required string ElementType { get; init; }

    public bool IsOccupied { get; init; }

    public bool IsDrive { get; init; }

    public string? Barcode { get; init; }

    public string? CartridgeId { get; init; }

    public required string Display { get; init; }

    public static SlotOption From(LibraryElementStatus element) {
      var isOccupied = !string.IsNullOrWhiteSpace(element.CartridgeId);
      var isDrive = string.Equals(element.ElementType, "Drive", StringComparison.OrdinalIgnoreCase);
      var display = isOccupied
        ? $"{element.ElementType} {element.Slot} · {element.Barcode ?? "no barcode"} · {element.CartridgeId}"
        : $"{element.ElementType} {element.Slot} · empty";
      return new SlotOption {
        Slot = element.Slot,
        ElementType = element.ElementType,
        IsOccupied = isOccupied,
        IsDrive = isDrive,
        Barcode = element.Barcode,
        CartridgeId = element.CartridgeId,
        Display = display
      };
    }
  }

  private readonly BackupOrchestrator _backupOrchestrator;

  private readonly LibraryOrchestrator _libraryOrchestrator;

  private readonly ConfigurationFileService _configurationFileService;

  private readonly DispatcherTimer _monitorTimer = new();

  private readonly List<TapeRow> _tapeRows = [];

  private HostServiceManager _serviceManager;

  private bool _loadingSettings;

  private bool _operationInProgress;

  private bool _monitorRefreshInProgress;

  private bool _loadingDriveSelection;

  private BackupAggregation? _lastAggregationSelection;

  private BackupItem? SelectedBackup => BackupCombo.SelectedItem as BackupItem;

  public MainWindow(
    BackupOrchestrator backupOrchestrator,
    LibraryOrchestrator libraryOrchestrator,
    ConfigurationFileService configurationFileService
  ) {
    _backupOrchestrator = backupOrchestrator;
    _libraryOrchestrator = libraryOrchestrator;
    _configurationFileService = configurationFileService;
    _serviceManager = new HostServiceManager(_configurationFileService.Current.Service.ServiceName);
    InitializeComponent();
    _monitorTimer.Tick += (_, _) => RefreshMonitor(silent: true);
    ApplyMonitorInterval();
    Closed += (_, _) => _monitorTimer.Stop();
    RefreshBackupLists();
    LoadSettingsIntoUi();
    RefreshServiceStatus();
    RefreshMonitor(silent: true);
  }

  private void RefreshBackupLists() {
    string selectedName = (BackupCombo.SelectedItem as BackupItem)?.Name ?? (SettingsBackupCombo.SelectedItem as BackupItem)?.Name;
    BackupCombo.ItemsSource = null;
    SettingsBackupCombo.ItemsSource = null;
    BackupCombo.ItemsSource = _backupOrchestrator.Backups;
    SettingsBackupCombo.ItemsSource = _backupOrchestrator.Backups;
    if (_backupOrchestrator.Backups.Count == 0) {
      return;
    }
    int selectedIndex = 0;
    if (!string.IsNullOrWhiteSpace(selectedName)) {
      var anon = _backupOrchestrator.Backups.Select((BackupItem item, int i) => new { item, i }).FirstOrDefault(x => string.Equals(x.item.Name, selectedName, StringComparison.OrdinalIgnoreCase));
      if (anon != null) {
        selectedIndex = anon.i;
      }
    }
    BackupCombo.SelectedIndex = selectedIndex;
    SettingsBackupCombo.SelectedIndex = selectedIndex;
    UpdateJobSummary(SelectedBackup);
  }

  private void UpdateJobSummary(BackupItem? backup) {
    if (backup == null) {
      JobSummaryText.Text = "Select a backup job to see source, destination, LTO generation, and barcode.";
      return;
    }
    JobSummaryText.Text = $"{backup.LTOGen} · barcode {backup.Barcode} · source {GetFolderPath(backup.Source)} → destination {GetFolderPath(backup.Destination)}";
  }

  private void BackupCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) {
    if (!_loadingSettings) {
      UpdateJobSummary(SelectedBackup);
    }
  }

  private void BackupDriveCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) {
    if (!_loadingDriveSelection && base.IsLoaded && BackupDriveCombo.SelectedItem is LoadedCartridgeSnapshot selected) {
      ApplySelectedLibraryDrive(selected);
    }
  }

  private void LibraryActiveDriveCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) {
    if (!_loadingDriveSelection && base.IsLoaded && LibraryActiveDriveCombo.SelectedItem is LoadedCartridgeSnapshot selected) {
      ApplySelectedLibraryDrive(selected);
    }
  }

  private void ApplySelectedLibraryDrive(LoadedCartridgeSnapshot selected) {
    _backupOrchestrator.SelectedLibraryDriveSlot = selected.DriveSlot;
    _loadingDriveSelection = true;
    try {
      if (BackupDriveCombo.SelectedItem != selected) {
        BackupDriveCombo.SelectedItem = (BackupDriveCombo.ItemsSource as IEnumerable<LoadedCartridgeSnapshot>)?.FirstOrDefault((LoadedCartridgeSnapshot item) => item.DriveSlot == selected.DriveSlot) ?? selected;
      }
      if (LibraryActiveDriveCombo.SelectedItem != selected) {
        LibraryActiveDriveCombo.SelectedItem = (LibraryActiveDriveCombo.ItemsSource as IEnumerable<LoadedCartridgeSnapshot>)?.FirstOrDefault((LoadedCartridgeSnapshot item) => item.DriveSlot == selected.DriveSlot) ?? selected;
      }
    }
    finally {
      _loadingDriveSelection = false;
    }
    RefreshLoadedCartridgeSummaries();
  }

  private void RefreshLoadedCartridgeSummaries() {
    LoadedCartridgeSnapshot loadedCartridge = _backupOrchestrator.GetLoadedCartridge();
    bool isTapeLibrary = _configurationFileService.Current.IsTapeLibrary;
    BackupLoadedSummaryText.Text = (loadedCartridge.IsLoaded ? (loadedCartridge.Summary ?? "Cartridge loaded.") : "No cartridge loaded.");
    BackupLoadedHintText.Text = loadedCartridge.Hint ?? (loadedCartridge.IsLoaded ? "Ready for backup/restore/erase on the selected job." : "Load media first (Drive tab for standalone, or Library tab move into a drive bay).");
    BackupOperationsGroup.IsEnabled = loadedCartridge.IsLoaded;
    RunBackupButton.IsEnabled = loadedCartridge.IsLoaded;
    RunRestoreButton.IsEnabled = loadedCartridge.IsLoaded;
    EraseButton.IsEnabled = loadedCartridge.IsLoaded;
    if (isTapeLibrary) {
      LibraryLoadedSummaryText.Text = (loadedCartridge.IsLoaded ? (loadedCartridge.Summary ?? "Cartridge in drive.") : "No cartridge in a drive bay.");
      LibraryLoadedHintText.Text = loadedCartridge.Hint ?? "Move a cartridge from a storage slot into a drive bay, then select the drive if more than one is loaded.";
      if (loadedCartridge.IsLoaded && !string.IsNullOrWhiteSpace(loadedCartridge.Barcode)) {
        LibraryBarcodeText.Text = loadedCartridge.Barcode;
      }
    }
  }

  private void UpdateLoadedCartridgeUi() {
    PopulateActiveDriveCombos();
    BackupDriveSelectorPanel.IsVisible = _configurationFileService.Current.IsTapeLibrary;
    RefreshLoadedCartridgeSummaries();
  }

  private void PopulateActiveDriveCombos() {
    if (!_configurationFileService.Current.IsTapeLibrary) {
      BackupDriveCombo.ItemsSource = null;
      LibraryActiveDriveCombo.ItemsSource = null;
      return;
    }
    IReadOnlyList<LoadedCartridgeSnapshot> occupiedLibraryDrives = _backupOrchestrator.GetOccupiedLibraryDrives();
    int? num = _backupOrchestrator.SelectedLibraryDriveSlot ?? (BackupDriveCombo.SelectedItem as LoadedCartridgeSnapshot)?.DriveSlot ?? (LibraryActiveDriveCombo.SelectedItem as LoadedCartridgeSnapshot)?.DriveSlot;
    _loadingDriveSelection = true;
    try {
      BackupDriveCombo.ItemsSource = occupiedLibraryDrives;
      LibraryActiveDriveCombo.ItemsSource = occupiedLibraryDrives;
      LoadedCartridgeSnapshot loadedCartridgeSnapshot = null;
      if (num.HasValue) {
        int slot = num.GetValueOrDefault();
        if (true) {
          loadedCartridgeSnapshot = occupiedLibraryDrives.FirstOrDefault((LoadedCartridgeSnapshot drive) => drive.DriveSlot == slot);
        }
      }
      if (loadedCartridgeSnapshot == null) {
        loadedCartridgeSnapshot = ((occupiedLibraryDrives.Count == 1) ? occupiedLibraryDrives[0] : null);
      }
      BackupDriveCombo.SelectedItem = loadedCartridgeSnapshot;
      LibraryActiveDriveCombo.SelectedItem = loadedCartridgeSnapshot;
      _backupOrchestrator.SelectedLibraryDriveSlot = loadedCartridgeSnapshot?.DriveSlot;
    }
    finally {
      _loadingDriveSelection = false;
    }
  }

  private void LoadSettingsIntoUi() {
    _loadingSettings = true;
    try {
      Configuration current = _configurationFileService.Current;
      SelectComboValue(DeviceModeCombo, current.DeviceMode);
      SelectComboValue(TopologyCombo, current.Topology);
      TapePathText.Text = current.TapePath;
      LibraryPathText.Text = current.LibraryPath;
      WriteDelayText.Text = current.WriteDelay.ToString();
      FailFastChecksumCheck.IsChecked = current.FailFastOnChecksumMismatch;
      EmulatorDataDirText.Text = current.Emulator.DataDirectory;
      EmulatorSlotCountText.Text = current.Emulator.Library.SlotCount.ToString();
      EmulatorDriveCountText.Text = current.Emulator.Library.DriveCount.ToString();
      EmulatorIePortCountText.Text = current.Emulator.Library.IePortCount.ToString();
      ServiceNameText.Text = current.Service.ServiceName;
      ServiceDescriptionText.Text = current.Service.Description;
      ServiceExePathSettingText.Text = current.Service.ExecutablePath;
      LoadSelectedBackupIntoUi(SettingsBackupCombo.SelectedItem as BackupItem);
      RefreshAggregationLists();
      UpdateTopologyUi();
    }
    finally {
      _loadingSettings = false;
    }
  }

  private async void TopologyCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) {
    if (_loadingSettings || !base.IsLoaded) {
      return;
    }
    bool wasStandalone = _configurationFileService.Current.IsStandaloneDrive;
    bool nowLibrary = string.Equals(GetComboValue(TopologyCombo), DeviceTopologies.TapeLibrary.Name, StringComparison.OrdinalIgnoreCase);
    if (wasStandalone && nowLibrary) {
      if (!(await ConfirmDialog.ShowAsync(this, "Switch to TapeLibrary?", "Switching to TapeLibrary enables library inventory, tape pools, and scheduled aggregations. Current settings will be saved now. Continue?"))) {
        RevertTopologyComboToStandalone();
        return;
      }
      Execute("Save configuration", SaveConfigurationFromUi);
      if (_configurationFileService.Current.IsStandaloneDrive) {
        RevertTopologyComboToStandalone();
      }
    }
    else {
      UpdateTopologyUi();
    }
  }

  private void RevertTopologyComboToStandalone() {
    _loadingSettings = true;
    try {
      SelectComboValue(TopologyCombo, DeviceTopologies.StandaloneDrive.Name);
    }
    finally {
      _loadingSettings = false;
    }
    UpdateTopologyUi();
  }

  private void UpdateTopologyUi() {
    bool flag = string.Equals(GetComboValue(TopologyCombo), DeviceTopologies.TapeLibrary.Name, StringComparison.OrdinalIgnoreCase);
    DriveTab.IsVisible = !flag;
    LibraryTab.IsVisible = flag;
    LibraryPathLabel.IsVisible = flag;
    LibraryPathText.IsVisible = flag;
    EmulatorLibrarySettingsGrid.IsVisible = flag;
    MonitorLibraryGroup.IsVisible = flag;
    MonitorInventoryGrid.IsVisible = flag;
    LibraryUnavailableText.IsVisible = false;
    LibraryLoadedGroup.IsVisible = flag;
    LibraryActionsGroup.IsVisible = flag;
    LibraryInventoryLabel.IsVisible = flag;
    InventoryGrid.IsVisible = flag;
    LibraryTapesGroup.IsVisible = flag;
    AggregationsGroup.IsVisible = flag;
    if (flag) {
      RefreshAvailableTapeBarcodes();
      RefreshAggregationLists();
    }
    UpdateLoadedCartridgeUi();
  }

  private void LoadSelectedBackupIntoUi(BackupItem? backup) {
    if (backup == null) {
      BackupNameText.Text = string.Empty;
      BackupBarcodeText.Text = string.Empty;
      BackupDisabledCheck.IsChecked = false;
      SelectComboValue(BackupLtoGenCombo, "LTO5");
      SelectComboValue(BackupWriteModeCombo, BackupWriteModes.Overwrite.Name);
      BackupSourcePathText.Text = string.Empty;
      BackupDestinationPathText.Text = string.Empty;
      _tapeRows.Clear();
      BackupTapesGrid.ItemsSource = null;
      RefreshAvailableTapeBarcodes();
      return;
    }
    BackupNameText.Text = backup.Name;
    BackupBarcodeText.Text = backup.Barcode;
    BackupDisabledCheck.IsChecked = backup.Disabled;
    SelectComboValue(BackupLtoGenCombo, backup.LTOGen);
    SelectComboValue(BackupWriteModeCombo, string.IsNullOrWhiteSpace(backup.WriteMode) ? BackupWriteModes.Overwrite.Name : backup.WriteMode);
    BackupSourcePathText.Text = GetFolderPath(backup.Source);
    BackupDestinationPathText.Text = GetFolderPath(backup.Destination);
    _tapeRows.Clear();
    foreach (BackupTape tape in backup.Tapes) {
      _tapeRows.Add(TapeRow.FromModel(tape));
    }
    BackupTapesGrid.ItemsSource = null;
    BackupTapesGrid.ItemsSource = _tapeRows;
    RefreshAvailableTapeBarcodes();
  }

  private void ClearScheduleUi() {
    foreach (Control child in ScheduleMonthPanel.Children) {
      if (child is CheckBox checkBox) {
        checkBox.IsChecked = false;
      }
    }
    foreach (Control child2 in ScheduleWeekdayPanel.Children) {
      if (child2 is CheckBox checkBox2) {
        checkBox2.IsChecked = false;
      }
    }
    RunTimesList.Items.Clear();
    MinIntervalMinutesText.Text = "10";
    NewRunTimeText.Text = "00:00";
  }

  private void LoadScheduleIntoUi(BackupSchedule? schedule) {
    ClearScheduleUi();
    if (schedule == null) {
      return;
    }
    foreach (Control child in ScheduleMonthPanel.Children) {
      if (!(child is CheckBox checkBox)) {
        continue;
      }
      object tag = checkBox.Tag;
      string name = tag as string;
      if (name != null) {
        checkBox.IsChecked = schedule.RunMonth.Any((string m) => string.Equals(m, name, StringComparison.OrdinalIgnoreCase));
      }
    }
    foreach (Control child2 in ScheduleWeekdayPanel.Children) {
      if (!(child2 is CheckBox checkBox2)) {
        continue;
      }
      object tag = checkBox2.Tag;
      string name2 = tag as string;
      if (name2 != null) {
        checkBox2.IsChecked = schedule.RunWeekday.Any((string d) => string.Equals(d, name2, StringComparison.OrdinalIgnoreCase));
      }
    }
    foreach (string item in schedule.RunTime) {
      RunTimesList.Items.Add(item);
    }
    MinIntervalMinutesText.Text = schedule.MinIntervalMinutes.ToString();
  }

  private BackupSchedule ReadScheduleFromUi() {
    BackupSchedule backupSchedule = new BackupSchedule {
      RunMonth = (from check in ScheduleMonthPanel.Children.OfType<CheckBox>()
                  where check.IsChecked == true && check.Tag is string
                  select (string)check.Tag).ToList(),
      RunWeekday = (from check in ScheduleWeekdayPanel.Children.OfType<CheckBox>()
                    where check.IsChecked == true && check.Tag is string
                    select (string)check.Tag).ToList(),
      RunTime = RunTimesList.Items.OfType<string>().ToList()
    };
    if (!int.TryParse(MinIntervalMinutesText.Text?.Trim(), out var result) || result < 0) {
      throw new InvalidOperationException("Minimum interval must be a non-negative integer.");
    }
    backupSchedule.MinIntervalMinutes = result;
    return backupSchedule;
  }

  private void AddTape_Click(object? sender, RoutedEventArgs e) {
    if (!(AvailableTapeBarcodeCombo.SelectedItem is AvailableTapeOption availableTapeOption)) {
      Log("Select an available library barcode first.");
      return;
    }
    string barcode = availableTapeOption.Barcode;
    if (_tapeRows.Any((TapeRow row) => string.Equals(row.Barcode, barcode, StringComparison.OrdinalIgnoreCase))) {
      Log("Tape '" + barcode + "' is already in the list.");
      return;
    }
    _tapeRows.Add(new TapeRow {
      Barcode = barcode
    });
    BackupTapesGrid.ItemsSource = null;
    BackupTapesGrid.ItemsSource = _tapeRows;
    RefreshAvailableTapeBarcodes();
  }

  private void RemoveTape_Click(object? sender, RoutedEventArgs e) {
    if (BackupTapesGrid.SelectedItem is TapeRow item) {
      _tapeRows.Remove(item);
      BackupTapesGrid.ItemsSource = null;
      BackupTapesGrid.ItemsSource = _tapeRows;
      RefreshAvailableTapeBarcodes();
    }
  }

  private void RefreshAvailableTapes_Click(object? sender, RoutedEventArgs e) => Execute("Refresh available tapes", RefreshAvailableTapeBarcodes);

  private void RefreshAvailableTapeBarcodes() {
    if (!base.IsLoaded || AvailableTapeBarcodeCombo == null) {
      return;
    }
    if (!_configurationFileService.Current.IsTapeLibrary && !string.Equals(GetComboValue(TopologyCombo), DeviceTopologies.TapeLibrary.Name, StringComparison.OrdinalIgnoreCase)) {
      AvailableTapeBarcodeCombo.ItemsSource = null;
      return;
    }
    HashSet<string> associated;
    try {
      associated = CollectAssociatedBarcodes();
    }
    catch {
      associated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
    List<AvailableTapeOption> itemsSource;
    try {
      itemsSource = (from @group in (from element in _libraryOrchestrator.Inventory()
                                     where !string.IsNullOrWhiteSpace(element.Barcode)
                                     select element).GroupBy<LibraryElementStatus, string>((LibraryElementStatus element) => element.Barcode.Trim(), StringComparer.OrdinalIgnoreCase)
                     where !associated.Contains(@group.Key)
                     select @group).OrderBy<IGrouping<string, LibraryElementStatus>, string>((IGrouping<string, LibraryElementStatus> group) => group.Key, StringComparer.OrdinalIgnoreCase).Select(delegate (IGrouping<string, LibraryElementStatus> group) {
                       LibraryElementStatus libraryElementStatus = group.First();
                       return new AvailableTapeOption {
                         Barcode = libraryElementStatus.Barcode.Trim(),
                         Display = $"{libraryElementStatus.Barcode.Trim()} · {libraryElementStatus.ElementType} {libraryElementStatus.Slot}" + (string.IsNullOrWhiteSpace(libraryElementStatus.CartridgeId) ? "" : (" · " + libraryElementStatus.CartridgeId))
                       };
                     }).ToList();
    }
    catch (Exception ex) {
      AvailableTapeBarcodeCombo.ItemsSource = null;
      Log("Could not load library inventory for tape picker: " + ex.Message);
      return;
    }
    AvailableTapeBarcodeCombo.ItemsSource = itemsSource;
    AvailableTapeBarcodeCombo.SelectedItem = null;
  }

  private HashSet<string> CollectAssociatedBarcodes() {
    HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    string text = (SettingsBackupCombo.SelectedItem as BackupItem)?.Name;
    foreach (BackupItem backup in _configurationFileService.Current.Backups) {
      IEnumerable<string> enumerable = ((string.IsNullOrWhiteSpace(text) || !string.Equals(backup.Name, text, StringComparison.OrdinalIgnoreCase)) ? backup.Tapes.Select((BackupTape tape) => tape.Barcode) : _tapeRows.Select((TapeRow row) => row.Barcode));
      foreach (string item in enumerable) {
        if (!string.IsNullOrWhiteSpace(item)) {
          hashSet.Add(item.Trim());
        }
      }
    }
    foreach (TapeRow tapeRow in _tapeRows) {
      if (!string.IsNullOrWhiteSpace(tapeRow.Barcode)) {
        hashSet.Add(tapeRow.Barcode.Trim());
      }
    }
    return hashSet;
  }

  private void AddRunTime_Click(object? sender, RoutedEventArgs e) {
    string s = NewRunTimeText.Text?.Trim() ?? string.Empty;
    if (!TimeOnly.TryParse(s, out var result)) {
      Log("Run time must be HH:mm (UTC).");
      return;
    }
    string value = result.ToString("HH:mm");
    if (!RunTimesList.Items.OfType<string>().Contains(value)) {
      RunTimesList.Items.Add(value);
    }
    NewRunTimeText.Text = "00:00";
  }

  private void RemoveRunTime_Click(object? sender, RoutedEventArgs e) {
    if (RunTimesList.SelectedItem is string value) {
      RunTimesList.Items.Remove(value);
    }
  }

  private static string GetFolderPath(WorkingFolder folder) {
    if (folder.LocalPath != null) {
      return folder.LocalPath.Path;
    }
    return folder.RemotePath?.Path ?? string.Empty;
  }

  private static void SelectComboValue(ComboBox comboBox, string value) {
    foreach (object item in comboBox.Items) {
      if (item is ComboBoxItem comboBoxItem && string.Equals(comboBoxItem.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase)) {
        comboBox.SelectedItem = comboBoxItem;
        break;
      }
    }
  }

  private static string GetComboValue(ComboBox comboBox) {
    if (comboBox.SelectedItem is ComboBoxItem comboBoxItem) {
      return comboBoxItem.Content?.ToString() ?? string.Empty;
    }
    return comboBox.SelectedItem?.ToString()?.Trim() ?? string.Empty;
  }

  private Configuration BuildConfigurationFromUi() {
    Configuration current = _configurationFileService.Current;
    current.DeviceMode = GetComboValue(DeviceModeCombo);
    current.Topology = GetComboValue(TopologyCombo);
    current.TapePath = TapePathText.Text?.Trim() ?? string.Empty;
    current.LibraryPath = LibraryPathText.Text?.Trim() ?? string.Empty;
    if (!int.TryParse(WriteDelayText.Text?.Trim(), out var result) || result < 0) {
      throw new InvalidOperationException("Write delay must be a non-negative integer.");
    }
    current.WriteDelay = result;
    current.FailFastOnChecksumMismatch = FailFastChecksumCheck.IsChecked == true;
    current.Emulator.DataDirectory = EmulatorDataDirText.Text?.Trim() ?? string.Empty;
    if (!int.TryParse(EmulatorSlotCountText.Text?.Trim(), out var result2) || result2 < 1) {
      throw new InvalidOperationException("Emulator slots must be a positive integer.");
    }
    if (!int.TryParse(EmulatorDriveCountText.Text?.Trim(), out var result3) || result3 < 1) {
      throw new InvalidOperationException("Emulator drives must be a positive integer.");
    }
    if (!int.TryParse(EmulatorIePortCountText.Text?.Trim(), out var result4) || result4 < 0) {
      throw new InvalidOperationException("Emulator I/E ports must be a non-negative integer.");
    }
    current.Emulator.Library.SlotCount = result2;
    current.Emulator.Library.DriveCount = result3;
    current.Emulator.Library.IePortCount = result4;
    current.Service.ServiceName = ServiceNameText.Text?.Trim() ?? "MaksIT.LTO.Backup";
    current.Service.Description = ServiceDescriptionText.Text?.Trim() ?? string.Empty;
    current.Service.ExecutablePath = ServiceExePathSettingText.Text?.Trim() ?? string.Empty;
    _serviceManager = new HostServiceManager(current.Service.ServiceName);
    if (SettingsBackupCombo.SelectedItem is BackupItem backupItem) {
      backupItem.Name = BackupNameText.Text?.Trim() ?? string.Empty;
      backupItem.Barcode = BackupBarcodeText.Text?.Trim() ?? string.Empty;
      backupItem.Disabled = BackupDisabledCheck.IsChecked == true;
      backupItem.LTOGen = GetComboValue(BackupLtoGenCombo);
      backupItem.WriteMode = GetComboValue(BackupWriteModeCombo);
      if (string.IsNullOrWhiteSpace(backupItem.WriteMode)) {
        backupItem.WriteMode = BackupWriteModes.Overwrite.Name;
      }
      backupItem.Tapes = (from row in _tapeRows
                          where !string.IsNullOrWhiteSpace(row.Barcode)
                          select row.ToModel()).ToList();
      string path = BackupSourcePathText.Text?.Trim() ?? string.Empty;
      string path2 = BackupDestinationPathText.Text?.Trim() ?? string.Empty;
      if (backupItem.Source.RemotePath != null) {
        backupItem.Source.RemotePath.Path = path;
      }
      else {
        WorkingFolder source = backupItem.Source;
        if (source.LocalPath == null) {
          WorkingFolder workingFolder = source;
          LocalPath obj = new LocalPath {
            Path = path
          };
          LocalPath localPath = obj;
          workingFolder.LocalPath = obj;
        }
        backupItem.Source.LocalPath.Path = path;
        backupItem.Source.RemotePath = null;
      }
      if (backupItem.Destination.RemotePath != null) {
        backupItem.Destination.RemotePath.Path = path2;
      }
      else {
        WorkingFolder source = backupItem.Destination;
        if (source.LocalPath == null) {
          WorkingFolder workingFolder2 = source;
          LocalPath obj2 = new LocalPath {
            Path = path2
          };
          LocalPath localPath = obj2;
          workingFolder2.LocalPath = obj2;
        }
        backupItem.Destination.LocalPath.Path = path2;
        backupItem.Destination.RemotePath = null;
      }
    }
    ApplyAggregationFromUi(current);
    return current;
  }

  private void SettingsAggregationCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) {
    if (!_loadingSettings) {
      if (SettingsAggregationCombo.SelectedItem is BackupAggregation) {
        ApplyAggregationFromUi(_configurationFileService.Current, _lastAggregationSelection);
      }
      _lastAggregationSelection = SettingsAggregationCombo.SelectedItem as BackupAggregation;
      LoadSelectedAggregationIntoUi(_lastAggregationSelection);
    }
  }

  private void RefreshAggregationLists() {
    if (!base.IsLoaded || SettingsAggregationCombo == null) {
      return;
    }
    string selectedName = (SettingsAggregationCombo.SelectedItem as BackupAggregation)?.Name ?? _configurationFileService.Current.Aggregations.FirstOrDefault()?.Name;
    SettingsAggregationCombo.ItemsSource = null;
    SettingsAggregationCombo.ItemsSource = _configurationFileService.Current.Aggregations;
    AggregationAddJobCombo.ItemsSource = null;
    AggregationAddJobCombo.ItemsSource = _configurationFileService.Current.Backups;
    if (_configurationFileService.Current.Aggregations.Count == 0) {
      LoadSelectedAggregationIntoUi(null);
      return;
    }
    BackupAggregation backupAggregation = _configurationFileService.Current.Aggregations.FirstOrDefault((BackupAggregation item) => string.Equals(item.Name, selectedName, StringComparison.OrdinalIgnoreCase)) ?? _configurationFileService.Current.Aggregations[0];
    SettingsAggregationCombo.SelectedItem = backupAggregation;
    _lastAggregationSelection = backupAggregation;
    LoadSelectedAggregationIntoUi(backupAggregation);
  }

  private void LoadSelectedAggregationIntoUi(BackupAggregation? aggregation) {
    if (aggregation == null) {
      AggregationNameText.Text = string.Empty;
      AggregationDisabledCheck.IsChecked = false;
      AggregationJobsList.Items.Clear();
      ClearScheduleUi();
      LastRunUtcText.Text = "Last wave run (UTC): (never)";
      return;
    }
    AggregationNameText.Text = aggregation.Name;
    AggregationDisabledCheck.IsChecked = aggregation.Disabled;
    AggregationJobsList.Items.Clear();
    foreach (string jobName in aggregation.JobNames) {
      AggregationJobsList.Items.Add(jobName);
    }
    LoadScheduleIntoUi(aggregation.Schedule ?? new BackupSchedule());
    LastRunUtcText.Text = ((!aggregation.LastRunUtc.HasValue) ? "Last wave run (UTC): (never)" : $"Last wave run (UTC): {aggregation.LastRunUtc:u}");
  }

  private void ApplyAggregationFromUi(Configuration config, BackupAggregation? previous = null) {
    BackupAggregation backupAggregation = previous ?? (SettingsAggregationCombo.SelectedItem as BackupAggregation);
    if (backupAggregation != null) {
      backupAggregation.Name = AggregationNameText.Text?.Trim() ?? backupAggregation.Name;
      backupAggregation.Disabled = AggregationDisabledCheck.IsChecked == true;
      backupAggregation.JobNames = AggregationJobsList.Items.OfType<string>().ToList();
      backupAggregation.Schedule = ReadScheduleFromUi();
    }
  }

  private void AddAggregation_Click(object? sender, RoutedEventArgs e) {
    ApplyAggregationFromUi(_configurationFileService.Current);
    var backupAggregation = new BackupAggregation {
      Name = $"Wave {_configurationFileService.Current.Aggregations.Count + 1}",
      Schedule = new BackupSchedule {
        MinIntervalMinutes = 60,
        RunTime = ["00:00"]
      }
    };
    _configurationFileService.Current.Aggregations.Add(backupAggregation);
    RefreshAggregationLists();
    SettingsAggregationCombo.SelectedItem = backupAggregation;
  }

  private void RemoveAggregation_Click(object? sender, RoutedEventArgs e) {
    if (SettingsAggregationCombo.SelectedItem is BackupAggregation item) {
      _configurationFileService.Current.Aggregations.Remove(item);
      _lastAggregationSelection = null;
      RefreshAggregationLists();
    }
  }

  private void AggregationAddJob_Click(object? sender, RoutedEventArgs e) {
    object selectedItem = AggregationAddJobCombo.SelectedItem;
    BackupItem job = selectedItem as BackupItem;
    if (job != null) {
      if (AggregationJobsList.Items.OfType<string>().Any((string name) => string.Equals(name, job.Name, StringComparison.OrdinalIgnoreCase))) {
        Log("Job '" + job.Name + "' is already in this wave.");
      }
      else {
        AggregationJobsList.Items.Add(job.Name);
      }
    }
  }

  private void AggregationRemoveJob_Click(object? sender, RoutedEventArgs e) {
    if (AggregationJobsList.SelectedItem is string value) {
      AggregationJobsList.Items.Remove(value);
    }
  }

  private void AggregationMoveJobUp_Click(object? sender, RoutedEventArgs e) {
    if (AggregationJobsList.SelectedItem is string text) {
      int num = AggregationJobsList.Items.IndexOf(text);
      if (num > 0) {
        AggregationJobsList.Items.RemoveAt(num);
        AggregationJobsList.Items.Insert(num - 1, text);
        AggregationJobsList.SelectedItem = text;
      }
    }
  }

  private void AggregationMoveJobDown_Click(object? sender, RoutedEventArgs e) {
    if (AggregationJobsList.SelectedItem is string text) {
      int num = AggregationJobsList.Items.IndexOf(text);
      if (num >= 0 && num < AggregationJobsList.Items.Count - 1) {
        AggregationJobsList.Items.RemoveAt(num);
        AggregationJobsList.Items.Insert(num + 1, text);
        AggregationJobsList.SelectedItem = text;
      }
    }
  }

  private void SettingsBackupCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) {
    if (!_loadingSettings) {
      LoadSelectedBackupIntoUi(SettingsBackupCombo.SelectedItem as BackupItem);
    }
  }

  private void SaveConfiguration_Click(object? sender, RoutedEventArgs e) => Execute("Save configuration", SaveConfigurationFromUi);

  private void SaveConfigurationFromUi() {
    Configuration configuration = BuildConfigurationFromUi();
    _configurationFileService.Save(configuration);
    RefreshBackupLists();
    LoadSettingsIntoUi();
    UpdateJobSummary(SelectedBackup);
    RefreshMonitor(silent: true);
    RefreshAvailableTapeBarcodes();
    RefreshAggregationLists();
    Log("Configuration saved to " + _configurationFileService.FilePath);
  }

  private void ReloadConfiguration_Click(object? sender, RoutedEventArgs e) => Execute("Reload configuration", delegate {
    _configurationFileService.Reload();
    RefreshBackupLists();
    LoadSettingsIntoUi();
    UpdateJobSummary(SelectedBackup);
    RefreshMonitor(silent: true);
    RefreshAvailableTapeBarcodes();
    RefreshAggregationLists();
    Log("Configuration reloaded from " + _configurationFileService.FilePath);
  });

  private void Log(string message) {
    if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) {
      Avalonia.Threading.Dispatcher.UIThread.Post(delegate {
        Log(message);
      });
      return;
    }
    LogTextBox.Text += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
    LogTextBox.CaretIndex = LogTextBox.Text?.Length ?? 0;
  }

  private void Execute(string action, Action work) {
    if (_operationInProgress) {
      Log(action + " skipped: another operation is already running.");
      return;
    }
    _operationInProgress = true;
    try {
      work();
      if (!action.StartsWith("Save", StringComparison.Ordinal) && !action.StartsWith("Reload", StringComparison.Ordinal)) {
        Log(action + " completed.");
      }
    }
    catch (Exception ex) {
      Log(action + " failed: " + ex.Message);
    }
    finally {
      _operationInProgress = false;
      RefreshMonitor(silent: true);
    }
  }

  private async void ExecuteBackground(string action, Action work) {
    if (_operationInProgress) {
      Log(action + " skipped: another operation is already running.");
      return;
    }
    _operationInProgress = true;
    SetBusyUi(busy: true, action);
    Log(action + " started…");
    try {
      await Task.Run(work).ConfigureAwait(continueOnCapturedContext: true);
      await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(delegate {
        Log(action + " completed.");
      });
    }
    catch (Exception ex) {
      Exception ex2 = ex;
      await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(delegate {
        Log(action + " failed: " + ex2.Message);
      });
    }
    finally {
      await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(delegate {
        _operationInProgress = false;
        SetBusyUi(busy: false, action);
        RefreshMonitor(silent: true);
      });
    }
  }

  private void SetBusyUi(bool busy, string action) {
    RunBackupButton.IsEnabled = !busy;
    RunRestoreButton.IsEnabled = !busy;
    EraseButton.IsEnabled = !busy;
    if (busy) {
      HeaderStatusText.Text = action + " in progress…";
    }
  }

  private void ApplyMonitorInterval() {
    int num = 5;
    if (MonitorIntervalCombo.SelectedItem is ComboBoxItem comboBoxItem && int.TryParse(comboBoxItem.Tag?.ToString(), out var result) && result > 0) {
      num = result;
    }
    _monitorTimer.Interval = TimeSpan.FromSeconds(num);
  }

  private void LiveMonitorCheckBox_Changed(object? sender, RoutedEventArgs e) {
    ApplyMonitorInterval();
    if (LiveMonitorCheckBox.IsChecked == true) {
      _monitorTimer.Start();
      RefreshMonitor(silent: true);
    }
    else {
      _monitorTimer.Stop();
    }
  }

  private void MonitorIntervalCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) {
    if (base.IsLoaded) {
      ApplyMonitorInterval();
    }
  }

  private void RefreshMonitor_Click(object? sender, RoutedEventArgs e) => RefreshMonitor(silent: false);

  private void RefreshMonitor(bool silent) {
    if (_monitorRefreshInProgress) {
      return;
    }
    if (_operationInProgress) {
      HeaderStatusText.Text = "Monitoring paused while an operation is running.";
      if (!silent) {
        Log("Monitor refresh skipped: an operation is running.");
      }
      return;
    }
    _monitorRefreshInProgress = true;
    try {
      DriveStatusSnapshot driveStatusSnapshot = _backupOrchestrator.GetDriveStatusSnapshot();
      LibraryStatusSnapshot libraryStatusSnapshot = _libraryOrchestrator.GetLibraryStatusSnapshot();
      bool available = libraryStatusSnapshot.Available;
      UpdateTopologyUi();
      MonitorDriveModeText.Text = driveStatusSnapshot.DeviceMode;
      MonitorDriveTopologyText.Text = driveStatusSnapshot.Topology;
      MonitorDriveStateText.Text = ((driveStatusSnapshot.Error == null) ? driveStatusSnapshot.State : (driveStatusSnapshot.State + ": " + driveStatusSnapshot.Error));
      MonitorDrivePathText.Text = driveStatusSnapshot.DevicePath;
      MonitorDriveStatusCodeText.Text = driveStatusSnapshot.StatusCode.ToString();
      MonitorDriveBlockText.Text = driveStatusSnapshot.AbsoluteBlock?.ToString() ?? "n/a";
      if (available) {
        MonitorLibraryModeText.Text = libraryStatusSnapshot.DeviceMode;
        MonitorLibraryPathText.Text = libraryStatusSnapshot.DevicePath;
        MonitorLibrarySummaryText.Text = ((libraryStatusSnapshot.Error == null) ? $"{libraryStatusSnapshot.OccupiedCount}/{libraryStatusSnapshot.TotalElements} occupied" : "Unavailable");
        MonitorLibraryDrivesText.Text = ((libraryStatusSnapshot.Error == null) ? $"{libraryStatusSnapshot.OccupiedDrives}/{libraryStatusSnapshot.DriveCount}" : "n/a");
        MonitorUpdatedText.Text = DateTime.Now.ToString("HH:mm:ss");
        MonitorLibraryErrorText.Text = libraryStatusSnapshot.Error ?? string.Empty;
        MonitorInventoryGrid.ItemsSource = libraryStatusSnapshot.Elements.Select(ToMonitorRow).ToList();
        InventoryGrid.ItemsSource = libraryStatusSnapshot.Elements;
        PopulateLibrarySlotCombos(libraryStatusSnapshot.Elements);
      }
      else {
        MonitorInventoryGrid.ItemsSource = null;
        InventoryGrid.ItemsSource = null;
        ClearLibrarySlotCombos();
        MonitorLibraryErrorText.Text = libraryStatusSnapshot.Error ?? string.Empty;
      }
      string value = ((driveStatusSnapshot.Error == null) ? driveStatusSnapshot.State : "Unavailable");
      HeaderStatusText.Text = (available ? $"Drive: {value} · Library: {((libraryStatusSnapshot.Error == null) ? $"{libraryStatusSnapshot.OccupiedCount}/{libraryStatusSnapshot.TotalElements} media, drives {libraryStatusSnapshot.OccupiedDrives}/{libraryStatusSnapshot.DriveCount}" : "Unavailable")} · Updated {DateTime.Now:HH:mm:ss}" : $"Drive: {value} · Topology: StandaloneDrive · Updated {DateTime.Now:HH:mm:ss}");
      UpdateLoadedCartridgeUi();
      if (!silent) {
        Log("Monitor refreshed.");
      }
    }
    catch (Exception ex) {
      HeaderStatusText.Text = "Monitor failed: " + ex.Message;
      if (!silent) {
        Log("Monitor refresh failed: " + ex.Message);
      }
    }
    finally {
      _monitorRefreshInProgress = false;
    }
  }

  private static MonitorElementRow ToMonitorRow(LibraryElementStatus element) => new MonitorElementRow {
    Slot = element.Slot,
    ElementType = element.ElementType,
    Occupied = (string.IsNullOrWhiteSpace(element.CartridgeId) ? "No" : "Yes"),
    Barcode = element.Barcode,
    CartridgeId = element.CartridgeId
  };

  private void ClearLibrarySlotCombos() {
    LoadSourceCombo.ItemsSource = null;
    LoadDriveCombo.ItemsSource = null;
    UnloadDriveCombo.ItemsSource = null;
    SourceSlotCombo.ItemsSource = null;
    DestinationSlotCombo.ItemsSource = null;
  }

  private void PopulateLibrarySlotCombos(IReadOnlyList<LibraryElementStatus> elements) {
    List<SlotOption> list = elements.Select(SlotOption.From).ToList();
    List<SlotOption> list2 = list.Where((SlotOption option) => option.IsOccupied).ToList();
    List<SlotOption> list3 = list.Where((SlotOption option) => option.IsDrive && !option.IsOccupied).ToList();
    List<SlotOption> list4 = list.Where((SlotOption option) => option.IsDrive && option.IsOccupied).ToList();
    List<SlotOption> source = list.Where((SlotOption option) => !option.IsDrive && !option.IsOccupied).ToList();
    List<SlotOption> list5 = list.Where((SlotOption option) => !option.IsOccupied).ToList();
    int? slot = (LoadSourceCombo.SelectedItem as SlotOption)?.Slot;
    int? slot2 = (LoadDriveCombo.SelectedItem as SlotOption)?.Slot;
    int? slot3 = (UnloadDriveCombo.SelectedItem as SlotOption)?.Slot;
    int? slot4 = (SourceSlotCombo.SelectedItem as SlotOption)?.Slot;
    int? slot5 = (DestinationSlotCombo.SelectedItem as SlotOption)?.Slot;
    LoadSourceCombo.ItemsSource = list2;
    LoadDriveCombo.ItemsSource = list3;
    UnloadDriveCombo.ItemsSource = list4;
    SourceSlotCombo.ItemsSource = list;
    DestinationSlotCombo.ItemsSource = list5;
    if (!SelectSlot(LoadSourceCombo, slot)) {
      SelectFirst(LoadSourceCombo, list2.FirstOrDefault((SlotOption option) => !option.IsDrive) ?? list2.FirstOrDefault());
    }
    if (!SelectSlot(LoadDriveCombo, slot2)) {
      SelectFirst(LoadDriveCombo, list3.FirstOrDefault());
    }
    if (!SelectSlot(UnloadDriveCombo, slot3)) {
      SelectFirst(UnloadDriveCombo, list4.FirstOrDefault());
    }
    if (!SelectSlot(SourceSlotCombo, slot4)) {
      SelectFirst(SourceSlotCombo, list2.FirstOrDefault());
    }
    if (!SelectSlot(DestinationSlotCombo, slot5)) {
      SelectFirst(DestinationSlotCombo, list3.FirstOrDefault() ?? source.FirstOrDefault() ?? list5.FirstOrDefault());
    }
  }

  private static bool SelectSlot(ComboBox comboBox, int? slot) {
    if (!slot.HasValue || !(comboBox.ItemsSource is IEnumerable<SlotOption> source)) {
      return false;
    }
    SlotOption slotOption = source.FirstOrDefault((SlotOption option) => option.Slot == slot.Value);
    if (slotOption == null) {
      return false;
    }
    comboBox.SelectedItem = slotOption;
    return true;
  }

  private static bool SelectFirst(ComboBox comboBox, SlotOption? option) {
    if (option == null) {
      comboBox.SelectedIndex = -1;
      return false;
    }
    comboBox.SelectedItem = option;
    return true;
  }

  private void LoadTape_Click(object? sender, RoutedEventArgs e) => ExecuteBackground("Load tape", _backupOrchestrator.LoadTape);

  private void EjectTape_Click(object? sender, RoutedEventArgs e) => ExecuteBackground("Eject tape", _backupOrchestrator.EjectTape);

  private void GetStatus_Click(object? sender, RoutedEventArgs e) => ExecuteBackground("Get status", delegate {
    int status = _backupOrchestrator.GetDeviceStatus();
    Avalonia.Threading.Dispatcher.UIThread.Post(delegate {
      Log($"Status code: {status}");
    });
  });

  private void Erase_Click(object? sender, RoutedEventArgs e) {
    BackupItem backup = SelectedBackup;
    ExecuteBackground("Tape erase", delegate {
      _backupOrchestrator.EnsureCartridgeLoaded();
      if (backup == null) {
        throw new InvalidOperationException("Select backup job first.");
      }
      _backupOrchestrator.TapeErase(backup.LTOGen);
    });
  }

  private void RunBackup_Click(object? sender, RoutedEventArgs e) {
    BackupItem backup = SelectedBackup;
    ExecuteBackground("Backup", delegate {
      _backupOrchestrator.EnsureCartridgeLoaded();
      if (backup == null) {
        throw new InvalidOperationException("Select backup job first.");
      }
      _backupOrchestrator.Backup(backup);
    });
  }

  private void RunRestore_Click(object? sender, RoutedEventArgs e) {
    BackupItem backup = SelectedBackup;
    ExecuteBackground("Restore", delegate {
      _backupOrchestrator.EnsureCartridgeLoaded();
      if (backup == null) {
        throw new InvalidOperationException("Select backup job first.");
      }
      _backupOrchestrator.Restore(backup);
    });
  }

  private void RefreshInventory_Click(object? sender, RoutedEventArgs e) => ExecuteBackground("Inventory", delegate {
    Avalonia.Threading.Dispatcher.UIThread.Post(delegate {
      RefreshMonitor(silent: true);
    });
  });

  private void LoadIntoDrive_Click(object? sender, RoutedEventArgs e) {
    SlotOption source = LoadSourceCombo.SelectedItem as SlotOption;
    SlotOption drive = LoadDriveCombo.SelectedItem as SlotOption;
    ExecuteBackground("Load into drive", delegate {
      if (source == null) {
        throw new InvalidOperationException("Select a cartridge (occupied slot) to load.");
      }
      if (drive == null) {
        throw new InvalidOperationException("Select an empty drive bay.");
      }
      if (!source.IsOccupied) {
        throw new InvalidOperationException("Source must contain a cartridge.");
      }
      if (!drive.IsDrive || drive.IsOccupied) {
        throw new InvalidOperationException("Destination must be an empty drive bay.");
      }
      _libraryOrchestrator.MoveMedium(source.Slot, drive.Slot);
    });
  }

  private void UnloadFromDrive_Click(object? sender, RoutedEventArgs e) {
    SlotOption drive = UnloadDriveCombo.SelectedItem as SlotOption;
    List<LibraryElementStatus> elements = (InventoryGrid.ItemsSource as IEnumerable<LibraryElementStatus>)?.ToList();
    ExecuteBackground("Unload from drive", delegate {
      if (drive == null) {
        throw new InvalidOperationException("Select a drive bay that has a cartridge.");
      }
      if (!drive.IsDrive || !drive.IsOccupied) {
        throw new InvalidOperationException("Selected unload source must be an occupied drive bay.");
      }
      if (elements == null) {
        throw new InvalidOperationException("Refresh inventory first.");
      }
      LibraryElementStatus libraryElementStatus = elements.FirstOrDefault((LibraryElementStatus element) => !string.Equals(element.ElementType, "Drive", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(element.CartridgeId)) ?? throw new InvalidOperationException("No empty storage slot available for unload.");
      _libraryOrchestrator.MoveMedium(drive.Slot, libraryElementStatus.Slot);
    });
  }

  private void MoveMedium_Click(object? sender, RoutedEventArgs e) {
    SlotOption source = SourceSlotCombo.SelectedItem as SlotOption;
    SlotOption destination = DestinationSlotCombo.SelectedItem as SlotOption;
    ExecuteBackground("Move medium", delegate {
      if (source == null) {
        throw new InvalidOperationException("Select a source slot.");
      }
      if (destination == null) {
        throw new InvalidOperationException("Select a destination slot.");
      }
      _libraryOrchestrator.MoveMedium(source.Slot, destination.Slot);
    });
  }

  private void ReadAttributes_Click(object? sender, RoutedEventArgs e) => ExecuteBackground("Read attributes", delegate {
    _backupOrchestrator.EnsureCartridgeLoaded();
    List<string> lines = (from attribute in _backupOrchestrator.ReadCartridgeMemory()
                          select $"(0x{attribute.Address:X4}) {attribute.Name}: {attribute.GetValueAsString()}").ToList();
    Avalonia.Threading.Dispatcher.UIThread.Post(delegate {
      foreach (string item in lines) {
        Log(item);
      }
    });
  });

  private void WriteStandaloneBarcode_Click(object? sender, RoutedEventArgs e) {
    string barcode = StandaloneBarcodeText.Text ?? string.Empty;
    bool isLibrary = _configurationFileService.Current.IsTapeLibrary;
    ExecuteBackground("Write barcode", delegate {
      if (isLibrary) {
        throw new InvalidOperationException("Use the Library tab to assign barcode for a loaded library cartridge.");
      }
      _backupOrchestrator.WriteBarcode(barcode);
    });
  }

  private void WriteLibraryBarcode_Click(object? sender, RoutedEventArgs e) {
    string barcode = LibraryBarcodeText.Text ?? string.Empty;
    bool isLibrary = _configurationFileService.Current.IsTapeLibrary;
    ExecuteBackground("Assign barcode", delegate {
      if (!isLibrary) {
        throw new InvalidOperationException("Library barcode assignment requires Topology = TapeLibrary.");
      }
      _backupOrchestrator.EnsureCartridgeLoaded();
      _backupOrchestrator.WriteBarcode(barcode);
    });
  }

  private string? ResolvedServiceExecutable() {
    Configuration current = _configurationFileService.Current;
    string text = ServiceExePathSettingText.Text?.Trim();
    if (!string.IsNullOrWhiteSpace(text)) {
      current.Service.ExecutablePath = text;
    }
    return HostServiceManager.ResolveExecutablePath(current.Service);
  }

  private void RefreshServiceStatus() {
    string text = ResolvedServiceExecutable();
    ServiceExePathText.Text = ((text == null) ? "Executable: (not found — build/publish MaksIT.LTO.Backup.Service next to the UI, or set path in Service settings)" : ("Executable: " + text));
    HostServiceStatus status = _serviceManager.GetStatus(text);
    ServiceStatusText.Text = $"Status: {status}";
  }

  private void ServiceRefresh_Click(object? sender, RoutedEventArgs e) => Execute("Refresh service status", RefreshServiceStatus);

  private void ServiceInstall_Click(object? sender, RoutedEventArgs e) => Execute("Install service", delegate {
    string executablePath = ResolvedServiceExecutable() ?? throw new InvalidOperationException("Worker executable not found.");
    HostServiceOperationResult hostServiceOperationResult = _serviceManager.Register(executablePath);
    Log(hostServiceOperationResult.Message);
    if (!hostServiceOperationResult.Success) {
      throw new InvalidOperationException(hostServiceOperationResult.Message);
    }
    RefreshServiceStatus();
  });

  private void ServiceUninstall_Click(object? sender, RoutedEventArgs e) => Execute("Uninstall service", delegate {
    HostServiceOperationResult hostServiceOperationResult = _serviceManager.Unregister(ResolvedServiceExecutable());
    Log(hostServiceOperationResult.Message);
    if (!hostServiceOperationResult.Success) {
      throw new InvalidOperationException(hostServiceOperationResult.Message);
    }
    RefreshServiceStatus();
  });

  private void ServiceStart_Click(object? sender, RoutedEventArgs e) => Execute("Start service", delegate {
    HostServiceOperationResult hostServiceOperationResult = _serviceManager.Start(ResolvedServiceExecutable());
    Log(hostServiceOperationResult.Message);
    if (!hostServiceOperationResult.Success) {
      throw new InvalidOperationException(hostServiceOperationResult.Message);
    }
    RefreshServiceStatus();
  });

  private void ServiceStop_Click(object? sender, RoutedEventArgs e) => Execute("Stop service", delegate {
    HostServiceOperationResult hostServiceOperationResult = _serviceManager.Stop(ResolvedServiceExecutable());
    Log(hostServiceOperationResult.Message);
    if (!hostServiceOperationResult.Success) {
      throw new InvalidOperationException(hostServiceOperationResult.Message);
    }
    RefreshServiceStatus();
  });
}
