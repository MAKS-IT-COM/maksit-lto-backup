using MaksIT.LTO.Core.MassStorage;


namespace MaksIT.LTO.Backup.UI.ViewModels;

public partial class MainViewModel {
  private void RefreshMonitor(bool silent) {
    if (_monitorRefreshInProgress)
      return;

    if (IsBusy) {
      HeaderStatusText = "Monitoring paused while an operation is running.";
      if (!silent)
        Log("Monitor refresh skipped: an operation is running.");

      return;
    }

    _monitorRefreshInProgress = true;
    try {
      var drive = _backupOrchestrator.GetDriveStatusSnapshot();
      var library = _libraryOrchestrator.GetLibraryStatusSnapshot();
      var available = library.Available;
      UpdateTopologyUi();
      MonitorDriveModeText = drive.DeviceMode;
      MonitorDriveTopologyText = drive.Topology;
      MonitorDriveStateText = drive.Error is null ? drive.State : drive.State + ": " + drive.Error;
      MonitorDrivePathText = drive.DevicePath;
      MonitorDriveStatusCodeText = drive.StatusCode.ToString();
      MonitorDriveBlockText = drive.AbsoluteBlock?.ToString() ?? "n/a";
      if (available) {
        MonitorLibraryModeText = library.DeviceMode;
        MonitorLibraryPathText = library.DevicePath;
        MonitorLibrarySummaryText = library.Error is null
          ? $"{library.OccupiedCount}/{library.TotalElements} occupied"
          : "Unavailable";
        MonitorLibraryDrivesText = library.Error is null
          ? $"{library.OccupiedDrives}/{library.DriveCount}"
          : "n/a";
        MonitorUpdatedText = DateTime.Now.ToString("HH:mm:ss");
        MonitorLibraryErrorText = library.Error ?? string.Empty;
        Replace(MonitorInventory, library.Elements.Select(MonitorElementRow.From));
        Replace(LibraryInventory, library.Elements);
        _libraryElements = library.Elements;
        PopulateLibrarySlotCombos(library.Elements);
      }
      else {
        MonitorInventory.Clear();
        LibraryInventory.Clear();
        _libraryElements = [];
        ClearLibrarySlotCombos();
        MonitorLibraryErrorText = library.Error ?? string.Empty;
      }

      var driveLabel = drive.Error is null ? drive.State : "Unavailable";
      HeaderStatusText = available
        ? $"Drive: {driveLabel} · Library: {(library.Error is null ? $"{library.OccupiedCount}/{library.TotalElements} media, drives {library.OccupiedDrives}/{library.DriveCount}" : "Unavailable")} · Updated {DateTime.Now:HH:mm:ss}"
        : $"Drive: {driveLabel} · Topology: StandaloneDrive · Updated {DateTime.Now:HH:mm:ss}";
      UpdateLoadedCartridgeUi();
      if (!silent)
        Log("Monitor refreshed.");
    }
    catch (Exception ex) {
      HeaderStatusText = "Monitor failed: " + ex.Message;
      if (!silent)
        Log("Monitor refresh failed: " + ex.Message);
    }
    finally {
      _monitorRefreshInProgress = false;
    }
  }

  private void ClearLibrarySlotCombos() {
    LoadSources.Clear();
    LoadDrives.Clear();
    UnloadDrives.Clear();
    SourceSlots.Clear();
    DestinationSlots.Clear();
    SelectedLoadSource = null;
    SelectedLoadDrive = null;
    SelectedUnloadDrive = null;
    SelectedSourceSlot = null;
    SelectedDestinationSlot = null;
  }

  private void PopulateLibrarySlotCombos(IReadOnlyList<LibraryElementStatus> elements) {
    var all = elements.Select(SlotOption.From).ToList();
    var occupied = all.Where(option => option.IsOccupied).ToList();
    var emptyDrives = all.Where(option => option.IsDrive && !option.IsOccupied).ToList();
    var occupiedDrives = all.Where(option => option.IsDrive && option.IsOccupied).ToList();
    var emptyStorage = all.Where(option => !option.IsDrive && !option.IsOccupied).ToList();
    var empty = all.Where(option => !option.IsOccupied).ToList();
    var previousLoadSource = SelectedLoadSource?.Slot;
    var previousLoadDrive = SelectedLoadDrive?.Slot;
    var previousUnload = SelectedUnloadDrive?.Slot;
    var previousSource = SelectedSourceSlot?.Slot;
    var previousDestination = SelectedDestinationSlot?.Slot;

    Replace(LoadSources, occupied);
    Replace(LoadDrives, emptyDrives);
    Replace(UnloadDrives, occupiedDrives);
    Replace(SourceSlots, all);
    Replace(DestinationSlots, empty);

    SelectedLoadSource = SelectSlot(LoadSources, previousLoadSource)
      ?? occupied.FirstOrDefault(option => !option.IsDrive)
      ?? occupied.FirstOrDefault();
    SelectedLoadDrive = SelectSlot(LoadDrives, previousLoadDrive) ?? emptyDrives.FirstOrDefault();
    SelectedUnloadDrive = SelectSlot(UnloadDrives, previousUnload) ?? occupiedDrives.FirstOrDefault();
    SelectedSourceSlot = SelectSlot(SourceSlots, previousSource) ?? occupied.FirstOrDefault();
    SelectedDestinationSlot = SelectSlot(DestinationSlots, previousDestination)
      ?? emptyDrives.FirstOrDefault()
      ?? emptyStorage.FirstOrDefault()
      ?? empty.FirstOrDefault();
  }

  private static SlotOption? SelectSlot(IEnumerable<SlotOption> options, int? slot) {
    if (!slot.HasValue)
      return null;

    return options.FirstOrDefault(option => option.Slot == slot.Value);
  }

  private static void Replace<T>(System.Collections.ObjectModel.ObservableCollection<T> target, IEnumerable<T> items) {
    target.Clear();
    foreach (var item in items)
      target.Add(item);
  }
}
