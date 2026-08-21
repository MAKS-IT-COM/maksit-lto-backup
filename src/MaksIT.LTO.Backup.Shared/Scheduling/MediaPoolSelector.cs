using MaksIT.LTO.Backup.Shared.Models;
using MaksIT.LTO.Core.MassStorage;


namespace MaksIT.LTO.Backup.Shared.Scheduling;

public static class MediaPoolSelector {
  public static BackupTape? SelectNext(IReadOnlyList<BackupTape> tapes, IReadOnlySet<string>? availableBarcodes = null) {
    if (tapes.Count == 0) {
      return null;
    }
    IEnumerable<(BackupTape Tape, int Index)> source = tapes
        .Select((BackupTape tape, int index) => (Tape: tape, Index: index))
        .Where(item => !string.IsNullOrWhiteSpace(item.Tape.Barcode));
    if (availableBarcodes != null) {
      source = source.Where(item => availableBarcodes.Contains(Normalize(item.Tape.Barcode)));
    }
    return source
        .OrderBy(item => item.Tape.LastUsedUtc.HasValue ? 1 : 0)
        .ThenBy(item => item.Tape.LastUsedUtc ?? DateTimeOffset.MinValue)
        .ThenBy(item => item.Index)
        .Select(item => item.Tape)
        .FirstOrDefault();
  }

  public static HashSet<string> CollectInventoryBarcodes(IEnumerable<LibraryElementStatus> inventory) {
    HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (LibraryElementStatus item in inventory) {
      if (!string.IsNullOrWhiteSpace(item.Barcode)) {
        hashSet.Add(Normalize(item.Barcode));
      }
    }
    return hashSet;
  }

  public static string Normalize(string barcode) => barcode.Trim();
}
