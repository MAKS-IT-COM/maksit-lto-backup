namespace MaksIT.LTO.Core.MassStorage;

public sealed class LibraryElementStatus {
  public required int Slot { get; set; }

  public required string ElementType { get; set; }

  public string? Barcode { get; set; }

  public string? CartridgeId { get; set; }
}
