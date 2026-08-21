using System.Text;
using System.Text.Json;


namespace MaksIT.LTO.Core.MassStorage;

public sealed class EmulatedTapeLibrary : ITapeLibrary {
  private sealed class SlotState {
    public string ElementType { get; set; } = "Slot";

    public string? CartridgeId { get; set; }

    public string? Barcode { get; set; }
  }

  private readonly string _statePath;

  private readonly Dictionary<int, SlotState> _slots;

  public string DevicePath { get; }

  public EmulatedTapeLibrary(string dataDirectory, int slotCount = 24, int driveCount = 2) {
    Directory.CreateDirectory(dataDirectory);
    _statePath = Path.Combine(dataDirectory, "library.json");
    DevicePath = "emu://Changer0";
    _slots = LoadOrCreate(slotCount, driveCount);
  }

  public IReadOnlyList<LibraryElementStatus> GetElementStatus() => (from pair in _slots
                                                                    orderby pair.Key
                                                                    select new LibraryElementStatus {
                                                                      Slot = pair.Key,
                                                                      ElementType = pair.Value.ElementType,
                                                                      Barcode = pair.Value.Barcode,
                                                                      CartridgeId = pair.Value.CartridgeId
                                                                    }).ToList();

  public void InitializeElementStatus() => Persist();

  public LibraryElementStatus? GetOccupiedDrive() => GetOccupiedDrives().FirstOrDefault();

  public IReadOnlyList<LibraryElementStatus> GetOccupiedDrives() => (from element in GetElementStatus()
                                                                     where string.Equals(element.ElementType, "Drive", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(element.CartridgeId)
                                                                     select element).ToList();

  public LibraryElementStatus? GetDrive(int driveSlot) => GetElementStatus().FirstOrDefault((LibraryElementStatus element) => element.Slot == driveSlot && string.Equals(element.ElementType, "Drive", StringComparison.OrdinalIgnoreCase));

  public LibraryElementStatus? FindByCartridgeId(string cartridgeId) => GetElementStatus().FirstOrDefault((LibraryElementStatus element) => string.Equals(element.CartridgeId, cartridgeId, StringComparison.OrdinalIgnoreCase));

  public LibraryElementStatus? EnsureMediaInDrive() {
    LibraryElementStatus occupiedDrive = GetOccupiedDrive();
    if (occupiedDrive != null) {
      return occupiedDrive;
    }
    LibraryElementStatus libraryElementStatus = GetElementStatus().FirstOrDefault((LibraryElementStatus element) => string.Equals(element.ElementType, "Slot", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(element.CartridgeId));
    LibraryElementStatus libraryElementStatus2 = GetElementStatus().FirstOrDefault((LibraryElementStatus element) => string.Equals(element.ElementType, "Drive", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(element.CartridgeId));
    if (libraryElementStatus == null || libraryElementStatus2 == null) {
      return null;
    }
    MoveMedium(libraryElementStatus.Slot, libraryElementStatus2.Slot);
    return GetOccupiedDrive();
  }

  public void SetBarcode(int slot, string barcode) {
    if (!_slots.TryGetValue(slot, out SlotState value)) {
      throw new InvalidOperationException($"Slot {slot} does not exist.");
    }
    if (string.IsNullOrWhiteSpace(value.CartridgeId)) {
      throw new InvalidOperationException($"Slot {slot} has no media.");
    }
    value.Barcode = (string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim());
    Persist();
  }

  public void SynchronizeBarcodeWithDrive(EmulatedTapeDrive drive) {
    string text = (drive.DevicePath.StartsWith("emu://", StringComparison.OrdinalIgnoreCase) ? drive.DevicePath.Substring("emu://".Length) : null);
    LibraryElementStatus libraryElementStatus = ((!string.IsNullOrWhiteSpace(text)) ? FindByCartridgeId(text) : null) ?? GetOccupiedDrive();
    if (libraryElementStatus == null) {
      return;
    }
    string text2 = ReadMamBarcode(drive);
    if (!string.IsNullOrWhiteSpace(text2)) {
      if (!string.Equals(text2, libraryElementStatus.Barcode, StringComparison.Ordinal)) {
        SetBarcode(libraryElementStatus.Slot, text2);
      }
    }
    else if (!string.IsNullOrWhiteSpace(libraryElementStatus.Barcode)) {
      WriteMamBarcode(drive, libraryElementStatus.Barcode);
    }
  }

  public void MoveMedium(int sourceSlot, int destinationSlot) {
    if (!_slots.TryGetValue(sourceSlot, out SlotState value) || !_slots.TryGetValue(destinationSlot, out SlotState value2)) {
      throw new InvalidOperationException("Slot does not exist.");
    }
    if (string.IsNullOrEmpty(value.CartridgeId)) {
      throw new InvalidOperationException("Source slot is empty.");
    }
    if (!string.IsNullOrEmpty(value2.CartridgeId)) {
      throw new InvalidOperationException("Destination slot already has media.");
    }
    value2.CartridgeId = value.CartridgeId;
    value2.Barcode = value.Barcode;
    value.CartridgeId = null;
    value.Barcode = null;
    Persist();
  }

  public static string? ReadMamBarcode(ITapeDrive drive) {
    LTOCmAttribute lTOCmAttribute = drive.ReadCartridgeAttributes().FirstOrDefault((LTOCmAttribute item) => item.Address == 2054);
    if (lTOCmAttribute?.Value == null || lTOCmAttribute.Value.Length == 0) {
      return null;
    }
    string text = Encoding.ASCII.GetString(lTOCmAttribute.Value).TrimEnd('\0').Trim();
    return string.IsNullOrWhiteSpace(text) ? null : text;
  }

  public static void WriteMamBarcode(ITapeDrive drive, string barcode) {
    LTOCmAttribute lTOCmAttribute = LTOCmKnownAttributes.GetAttributeByAddress(2054) ?? throw new InvalidOperationException("Barcode MAM attribute is unavailable.");
    byte[] array = new byte[lTOCmAttribute.Length];
    byte[] bytes = Encoding.ASCII.GetBytes(barcode.Trim());
    Array.Copy(bytes, 0, array, 0, Math.Min(bytes.Length, array.Length));
    drive.WriteCartridgeAttribute(new LTOCmAttribute {
      Address = lTOCmAttribute.Address,
      Name = lTOCmAttribute.Name,
      Writable = true,
      Format = lTOCmAttribute.Format,
      Length = lTOCmAttribute.Length,
      Value = array
    });
  }

  private Dictionary<int, SlotState> LoadOrCreate(int slotCount, int driveCount) {
    if (File.Exists(_statePath)) {
      Dictionary<int, SlotState> dictionary = JsonSerializer.Deserialize<Dictionary<int, SlotState>>(File.ReadAllText(_statePath));
      if (dictionary != null) {
        return dictionary;
      }
    }
    Dictionary<int, SlotState> dictionary2 = new Dictionary<int, SlotState>();
    for (int i = 1; i <= slotCount; i++) {
      dictionary2[i] = new SlotState {
        ElementType = "Slot",
        CartridgeId = ((i <= 2) ? $"TAPE-{i:000}" : null),
        Barcode = ((i <= 2) ? $"LTO{i:000}" : null)
      };
    }
    for (int j = 0; j < driveCount; j++) {
      dictionary2[1000 + j] = new SlotState {
        ElementType = "Drive"
      };
    }
    File.WriteAllText(_statePath, JsonSerializer.Serialize(dictionary2, new JsonSerializerOptions {
      WriteIndented = true
    }));
    return dictionary2;
  }

  private void Persist() => File.WriteAllText(_statePath, JsonSerializer.Serialize(_slots, new JsonSerializerOptions {
    WriteIndented = true
  }));
}
