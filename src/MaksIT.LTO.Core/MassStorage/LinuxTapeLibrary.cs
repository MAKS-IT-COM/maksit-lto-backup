using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;


namespace MaksIT.LTO.Core.MassStorage;

[SupportedOSPlatform("linux")]
public sealed class LinuxTapeLibrary : ITapeLibrary, IDisposable {
  private readonly ILogger<LinuxTapeLibrary> _logger;

  private readonly int _fd;

  private bool _disposed;

  public string DevicePath { get; }

  public LinuxTapeLibrary(ILogger<LinuxTapeLibrary> logger, string libraryPath) {
    _logger = logger;
    DevicePath = libraryPath;
    _fd = LinuxTapeNative.Open(libraryPath, 2);
    if (_fd < 0) {
      int lastPInvokeError = Marshal.GetLastPInvokeError();
      throw new IOException($"Failed to open changer '{libraryPath}' (errno {lastPInvokeError}).");
    }
    _logger.LogInformation("Opened Linux changer {LibraryPath} (fd={Fd}).", libraryPath, _fd);
  }

  public IReadOnlyList<LibraryElementStatus> GetElementStatus() {
    ThrowIfDisposed();
    List<LibraryElementStatus> list = new List<LibraryElementStatus>();
    list.AddRange(ReadElementType(2, "Slot", 0, 256));
    list.AddRange(MapDrives(ReadElementType(4, "Drive", 0, 16)));
    list.AddRange(ReadElementType(3, "IEPort", 0, 16));
    list.AddRange(ReadElementType(1, "Transport", 0, 8));
    return list;
  }

  public void InitializeElementStatus() {
    ThrowIfDisposed();
    byte[] cdb = ScsiMediumChanger.BuildInitializeElementStatusCdb();
    LinuxTapeNative.SgIo(_fd, cdb, null, -1);
  }

  public void MoveMedium(int sourceSlot, int destinationSlot) {
    ThrowIfDisposed();
    ushort source = ToScsiAddress(sourceSlot);
    ushort destination = ToScsiAddress(destinationSlot);
    byte[] cdb = ScsiMediumChanger.BuildMoveMediumCdb(0, source, destination);
    LinuxTapeNative.SgIo(_fd, cdb, null, -1);
  }

  public void Dispose() {
    if (!_disposed) {
      _disposed = true;
      if (_fd >= 0) {
        LinuxTapeNative.Close(_fd);
      }
    }
  }

  private IReadOnlyList<LibraryElementStatus> ReadElementType(byte elementType, string typeName, ushort startingElement, ushort numberOfElements) {
    byte[] data = new byte[65536];
    byte[] cdb = ScsiMediumChanger.BuildReadElementStatusCdb(elementType, startingElement, numberOfElements, 65536u);
    try {
      LinuxTapeNative.SgIo(_fd, cdb, data, -3);
    }
    catch (IOException exception) {
      _logger.LogDebug(exception, "READ ELEMENT STATUS for {Type} returned no data.", typeName);
      return Array.Empty<LibraryElementStatus>();
    }
    return ScsiMediumChanger.ParseElementStatusData(data, typeName);
  }

  private static IReadOnlyList<LibraryElementStatus> MapDrives(IReadOnlyList<LibraryElementStatus> drives) {
    List<LibraryElementStatus> list = new List<LibraryElementStatus>();
    list.AddRange(drives.Select((LibraryElementStatus d) => new LibraryElementStatus {
      Slot = 1000 + d.Slot,
      ElementType = d.ElementType,
      Barcode = d.Barcode,
      CartridgeId = d.CartridgeId
    }));
    return list;
  }

  private static ushort ToScsiAddress(int slot) {
    if (slot >= 1000) {
      return (ushort)(slot - 1000);
    }
    return (ushort)slot;
  }

  private void ThrowIfDisposed() {
    if (_disposed) {
      throw new ObjectDisposedException("LinuxTapeLibrary");
    }
  }
}
