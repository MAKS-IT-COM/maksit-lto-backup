using System.Runtime.InteropServices;
using System.Runtime.Versioning;


namespace MaksIT.LTO.Core.MassStorage;

[SupportedOSPlatform("linux")]
internal static class LinuxTapeNative {
  public struct Mtop {
    public short MtOp;

    public int MtCount;
  }

  public struct Mtget {
    public long MtType;

    public long MtResid;

    public long MtDsreg;

    public long MtGstat;

    public long MtErreg;

    public int MtFileno;

    public int MtBlkno;
  }

  public struct SgIoHdr {
    public int InterfaceId;

    public int DxferDirection;

    public byte CmdLen;

    public byte MxSbLen;

    public ushort IovecCount;

    public uint DxferLen;

    public nint Dxferp;

    public nint Cmdp;

    public nint Sbp;

    public uint Timeout;

    public uint Flags;

    public int PackId;

    public nint UsrPtr;

    public byte Status;

    public byte MaskedStatus;

    public byte MsgStatus;

    public byte SbLenWr;

    public ushort HostStatus;

    public ushort DriverStatus;

    public int Resid;

    public uint Duration;

    public uint Info;
  }

  public const int O_RDWR = 2;

  public const int O_NONBLOCK = 2048;

  public const short MTWEOF = 0;

  public const short MTFSF = 1;

  public const short MTBSF = 2;

  public const short MTFSR = 3;

  public const short MTBSR = 4;

  public const short MTREW = 6;

  public const short MTOFFL = 7;

  public const short MTRETEN = 9;

  public const short MTEOM = 12;

  public const short MTERASE = 13;

  public const short MTSETBLK = 20;

  public const short MTLOAD = 30;

  public static readonly uint MTIOCTOP = 1074294017u;

  public static readonly uint MTIOCGET = 2150657282u;

  public static readonly uint SG_IO = 8837u;

  public const int SG_DXFER_NONE = -1;

  public const int SG_DXFER_TO_DEV = -2;

  public const int SG_DXFER_FROM_DEV = -3;

  [DllImport("libc", EntryPoint = "open", SetLastError = true)]
  public static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string pathname, int flags);

  [DllImport("libc", EntryPoint = "close", SetLastError = true)]
  public static extern int Close(int fd);

  [DllImport("libc", EntryPoint = "read", SetLastError = true)]
  public static extern unsafe nint Read(int fd, byte* buffer, nuint count);

  [DllImport("libc", EntryPoint = "write", SetLastError = true)]
  public static extern unsafe nint Write(int fd, byte* buffer, nuint count);

  [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
  public static extern int Ioctl(int fd, uint request, ref Mtop arg);

  [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
  public static extern int Ioctl(int fd, uint request, ref Mtget arg);

  [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
  public static extern int Ioctl(int fd, uint request, ref SgIoHdr arg);

  public static void ThrowIfFailed(int result, string operation) {
    if (result >= 0) {
      return;
    }
    int lastPInvokeError = Marshal.GetLastPInvokeError();
    throw new IOException($"{operation} failed with errno {lastPInvokeError}.");
  }

  public static void MtOp(int fd, short op, int count = 1) {
    Mtop arg = new Mtop {
      MtOp = op,
      MtCount = count
    };
    ThrowIfFailed(Ioctl(fd, MTIOCTOP, ref arg), $"MTIOCTOP({op})");
  }

  public unsafe static void SgIo(int fd, byte[] cdb, byte[]? data, int dxferDirection, uint timeoutMs = 120000u) {
    byte[] array = new byte[32];
    fixed (byte* cmdp = cdb) {
      fixed (byte* sbp = array) {
        fixed (byte* ptr = data) {
          SgIoHdr arg = new SgIoHdr {
            InterfaceId = 83,
            DxferDirection = dxferDirection,
            CmdLen = (byte)cdb.Length,
            MxSbLen = (byte)array.Length,
            DxferLen = ((data != null) ? ((uint)data.Length) : 0u),
            Dxferp = ((data == null) ? IntPtr.Zero : ((nint)ptr)),
            Cmdp = (nint)cmdp,
            Sbp = (nint)sbp,
            Timeout = timeoutMs
          };
          ThrowIfFailed(Ioctl(fd, SG_IO, ref arg), "SG_IO");
          if (arg.Status != 0 || arg.HostStatus != 0 || arg.DriverStatus != 0) {
            throw new IOException($"SG_IO SCSI status=0x{arg.Status:X2} host=0x{arg.HostStatus:X4} driver=0x{arg.DriverStatus:X4}");
          }
        }
      }
    }
  }
}
