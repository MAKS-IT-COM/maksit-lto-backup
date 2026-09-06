using CommunityToolkit.Mvvm.ComponentModel;
using MaksIT.LTO.Backup.Shared.Models;
using MaksIT.LTO.Core.MassStorage;


namespace MaksIT.LTO.Backup.UI.ViewModels;

public sealed class TapeRow {
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

public sealed class AvailableTapeOption {
  public required string Barcode { get; init; }

  public required string Display { get; init; }
}

public sealed class MonitorElementRow {
  public int Slot { get; init; }

  public required string ElementType { get; init; }

  public required string Occupied { get; init; }

  public string? Barcode { get; init; }

  public string? CartridgeId { get; init; }

  public static MonitorElementRow From(LibraryElementStatus element) => new MonitorElementRow {
    Slot = element.Slot,
    ElementType = element.ElementType,
    Occupied = string.IsNullOrWhiteSpace(element.CartridgeId) ? "No" : "Yes",
    Barcode = element.Barcode,
    CartridgeId = element.CartridgeId
  };
}

public sealed class SlotOption {
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

public sealed record MonitorIntervalOption(int Seconds) {
  public string Display => $"{Seconds} s";
}

public partial class ScheduleToggleOption : ObservableObject {
  public string Name { get; }

  public string Label { get; }

  [ObservableProperty]
  private bool _isChecked;

  public ScheduleToggleOption(string name, string label) {
    Name = name;
    Label = label;
  }
}
