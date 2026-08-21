using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;


namespace MaksIT.LTO.Core.MassStorage;

[SupportedOSPlatform("windows")]
public sealed class WindowsTapeLibrary : ITapeLibrary, IDisposable {
  private enum ElementType {
    AllElements,
    ChangerTransport,
    ChangerSlot,
    ChangerIEPort,
    ChangerDrive
  }

  private struct ChangerElement {
    public ElementType ElementType;

    public uint ElementAddress;
  }

  private struct ChangerElementList {
    public ChangerElement Element;

    public uint NumberOfElements;
  }

  private struct ChangerReadElementStatus {
    public ChangerElementList ElementList;

    public byte VolumeTagInfo;
  }

  private struct ChangerInitializeElementStatus {
    public ChangerElementList ElementList;

    public byte BarCodeScan;
  }

  private struct ChangerMoveMedium {
    public ChangerElement Transport;

    public ChangerElement Source;

    public ChangerElement Destination;

    public byte Flip;
  }

  private struct ChangerElementStatus {
    public ChangerElement Element;

    public ChangerElement SrcElementAddress;

    public uint Flags;

    public uint ExceptionCode;

    public byte TargetId;

    public byte Lun;

    public ushort Reserved;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)]
    public byte[] PrimaryVolumeID;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)]
    public byte[] AlternateVolumeID;
  }

  private struct GetChangerParameters {
    public uint Size;

    public ushort NumberTransportElements;

    public ushort NumberStorageElements;

    public ushort NumberCleanerSlots;

    public ushort NumberIEElements;

    public ushort NumberDataTransferElements;

    public ushort NumberOfDoors;

    public ushort FirstSlotNumber;

    public ushort FirstDriveNumber;

    public ushort FirstTransportNumber;

    public ushort FirstIEPortNumber;

    public ushort FirstCleanerSlotAddress;

    public ushort MagazineSize;

    public uint DriveCleanTimeout;

    public uint Features0;

    public uint Features1;

    public byte MoveFromTransport;

    public byte MoveFromSlot;

    public byte MoveFromIePort;

    public byte MoveFromDrive;

    public byte ExchangeFromTransport;

    public byte ExchangeFromSlot;

    public byte ExchangeFromIePort;

    public byte ExchangeFromDrive;

    public byte LockUnlockCapabilities;

    public byte PositionCapabilities;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public byte[] Reserved1;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] Reserved2;
  }

  private const uint FILE_DEVICE_CHANGER = 48u;

  private const uint FILE_READ_ACCESS = 1u;

  private const uint FILE_WRITE_ACCESS = 2u;

  private const uint METHOD_BUFFERED = 0u;

  private const uint GENERIC_READ = 2147483648u;

  private const uint GENERIC_WRITE = 1073741824u;

  private const uint OPEN_EXISTING = 3u;

  private const uint ELEMENT_STATUS_FULL = 1u;

  private const uint ELEMENT_STATUS_PVOLTAG = 268435456u;

  private const int MaxVolumeIdSize = 36;

  private static readonly uint IOCTL_CHANGER_GET_PARAMETERS = 3162112u;

  private static readonly uint IOCTL_CHANGER_GET_ELEMENT_STATUS = 3194900u;

  private static readonly uint IOCTL_CHANGER_INITIALIZE_ELEMENT_STATUS = 3162136u;

  private static readonly uint IOCTL_CHANGER_MOVE_MEDIUM = 3162148u;

  private readonly ILogger<WindowsTapeLibrary> _logger;

  private readonly SafeFileHandle _handle;

  public string DevicePath { get; }

  public WindowsTapeLibrary(ILogger<WindowsTapeLibrary> logger, string libraryPath) {
    _logger = logger;
    DevicePath = libraryPath;
    _handle = CreateFile(libraryPath, 3221225472u, 0u, IntPtr.Zero, 3u, 0u, IntPtr.Zero);
    if (_handle.IsInvalid) {
      throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to open changer '" + libraryPath + "'.");
    }
    _logger.LogInformation("Opened Windows changer {LibraryPath}.", libraryPath);
  }

  public IReadOnlyList<LibraryElementStatus> GetElementStatus() {
    GetChangerParameters parameters = GetParameters();
    List<LibraryElementStatus> list = new List<LibraryElementStatus>();
    list.AddRange(ReadElements(ElementType.ChangerSlot, parameters.NumberStorageElements, "Slot"));
    list.AddRange(ReadElements(ElementType.ChangerDrive, parameters.NumberDataTransferElements, "Drive"));
    list.AddRange(ReadElements(ElementType.ChangerIEPort, parameters.NumberIEElements, "IEPort"));
    list.AddRange(ReadElements(ElementType.ChangerTransport, parameters.NumberTransportElements, "Transport"));
    return list;
  }

  public void InitializeElementStatus() {
    GetChangerParameters parameters = GetParameters();
    ChangerInitializeElementStatus structure = new ChangerInitializeElementStatus {
      ElementList = new ChangerElementList {
        Element = new ChangerElement {
          ElementType = ElementType.AllElements,
          ElementAddress = 0u
        },
        NumberOfElements = (uint)(parameters.NumberStorageElements + parameters.NumberDataTransferElements + parameters.NumberIEElements + parameters.NumberTransportElements)
      },
      BarCodeScan = 1
    };
    nint num = Marshal.AllocHGlobal(Marshal.SizeOf(structure));
    try {
      Marshal.StructureToPtr(structure, num, fDeleteOld: false);
      if (!DeviceIoControl(_handle, IOCTL_CHANGER_INITIALIZE_ELEMENT_STATUS, num, (uint)Marshal.SizeOf(structure), IntPtr.Zero, 0u, out var _, IntPtr.Zero)) {
        throw new Win32Exception(Marshal.GetLastWin32Error(), "IOCTL_CHANGER_INITIALIZE_ELEMENT_STATUS failed.");
      }
    }
    finally {
      Marshal.FreeHGlobal(num);
    }
  }

  public void MoveMedium(int sourceSlot, int destinationSlot) {
    ChangerElement source = ResolveElement(sourceSlot);
    ChangerElement destination = ResolveElement(destinationSlot);
    ChangerElement transport = new ChangerElement {
      ElementType = ElementType.ChangerTransport,
      ElementAddress = 0u
    };
    ChangerMoveMedium structure = new ChangerMoveMedium {
      Transport = transport,
      Source = source,
      Destination = destination,
      Flip = 0
    };
    nint num = Marshal.AllocHGlobal(Marshal.SizeOf(structure));
    try {
      Marshal.StructureToPtr(structure, num, fDeleteOld: false);
      if (!DeviceIoControl(_handle, IOCTL_CHANGER_MOVE_MEDIUM, num, (uint)Marshal.SizeOf(structure), IntPtr.Zero, 0u, out var _, IntPtr.Zero)) {
        throw new Win32Exception(Marshal.GetLastWin32Error(), $"IOCTL_CHANGER_MOVE_MEDIUM {sourceSlot}->{destinationSlot} failed.");
      }
    }
    finally {
      Marshal.FreeHGlobal(num);
    }
  }

  public void Dispose() => _handle.Dispose();

  private ChangerElement ResolveElement(int address) {
    GetChangerParameters parameters = GetParameters();
    if (address < parameters.NumberStorageElements) {
      return new ChangerElement {
        ElementType = ElementType.ChangerSlot,
        ElementAddress = (uint)address
      };
    }
    int num = 1000;
    if (address >= num && address < num + parameters.NumberDataTransferElements) {
      return new ChangerElement {
        ElementType = ElementType.ChangerDrive,
        ElementAddress = (uint)(address - num)
      };
    }
    return new ChangerElement {
      ElementType = ElementType.ChangerSlot,
      ElementAddress = (uint)address
    };
  }

  private IReadOnlyList<LibraryElementStatus> ReadElements(ElementType type, ushort count, string typeName) {
    if (count == 0) {
      return Array.Empty<LibraryElementStatus>();
    }
    ChangerReadElementStatus structure = new ChangerReadElementStatus {
      ElementList = new ChangerElementList {
        Element = new ChangerElement {
          ElementType = type,
          ElementAddress = 0u
        },
        NumberOfElements = count
      },
      VolumeTagInfo = 1
    };
    int num = Marshal.SizeOf<ChangerElementStatus>();
    int num2 = num * count;
    nint num3 = Marshal.AllocHGlobal(Marshal.SizeOf(structure));
    nint num4 = Marshal.AllocHGlobal(num2);
    try {
      Marshal.StructureToPtr(structure, num3, fDeleteOld: false);
      if (!DeviceIoControl(_handle, IOCTL_CHANGER_GET_ELEMENT_STATUS, num3, (uint)Marshal.SizeOf(structure), num4, (uint)num2, out var lpBytesReturned, IntPtr.Zero)) {
        throw new Win32Exception(Marshal.GetLastWin32Error(), "IOCTL_CHANGER_GET_ELEMENT_STATUS(" + typeName + ") failed.");
      }
      int num5 = (int)(lpBytesReturned / (uint)num);
      List<LibraryElementStatus> list = new List<LibraryElementStatus>();
      for (int i = 0; i < num5; i++) {
        ChangerElementStatus changerElementStatus = Marshal.PtrToStructure<ChangerElementStatus>(num4 + i * num);
        string text = null;
        if ((changerElementStatus.Flags & 0x10000000) != 0 && changerElementStatus.PrimaryVolumeID != null) {
          text = Encoding.ASCII.GetString(changerElementStatus.PrimaryVolumeID).Trim(new char[2] { '\0', ' ' });
          if (string.IsNullOrWhiteSpace(text)) {
            text = null;
          }
        }
        bool flag = (changerElementStatus.Flags & 1) != 0;
        int num6 = (int)((type == ElementType.ChangerDrive) ? (1000 + changerElementStatus.Element.ElementAddress) : changerElementStatus.Element.ElementAddress);
        list.Add(new LibraryElementStatus {
          Slot = num6,
          ElementType = typeName,
          Barcode = text,
          CartridgeId = (flag ? (text ?? $"media-{num6}") : null)
        });
      }
      return list;
    }
    finally {
      Marshal.FreeHGlobal(num3);
      Marshal.FreeHGlobal(num4);
    }
  }

  private GetChangerParameters GetParameters() {
    int num = Marshal.SizeOf<GetChangerParameters>();
    nint num2 = Marshal.AllocHGlobal(num);
    try {
      if (!DeviceIoControl(_handle, IOCTL_CHANGER_GET_PARAMETERS, IntPtr.Zero, 0u, num2, (uint)num, out var _, IntPtr.Zero)) {
        throw new Win32Exception(Marshal.GetLastWin32Error(), "IOCTL_CHANGER_GET_PARAMETERS failed.");
      }
      return Marshal.PtrToStructure<GetChangerParameters>(num2);
    }
    finally {
      Marshal.FreeHGlobal(num2);
    }
  }

  [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
  private static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, nint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, nint hTemplateFile);

  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, nint lpInBuffer, uint nInBufferSize, nint lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, nint lpOverlapped);
}
