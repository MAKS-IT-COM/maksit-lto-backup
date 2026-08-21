using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;


namespace MaksIT.LTO.Core.MassStorage;

[SupportedOSPlatform("linux")]
public sealed class LinuxTapeDrive : ITapeDrive, IDisposable {
  private readonly ILogger<LinuxTapeDrive> _logger;

  private readonly int _fd;

  private bool _disposed;

  public string DevicePath { get; }

  public LinuxTapeDrive(ILogger<LinuxTapeDrive> logger, string tapePath) {
    _logger = logger;
    DevicePath = tapePath;
    _fd = LinuxTapeNative.Open(tapePath, 2);
    if (_fd < 0) {
      int lastPInvokeError = Marshal.GetLastPInvokeError();
      throw new IOException($"Failed to open tape device '{tapePath}' (errno {lastPInvokeError}).");
    }
    _logger.LogInformation("Opened Linux tape device {DevicePath} (fd={Fd}).", tapePath, _fd);
  }

  public unsafe int WriteData(byte[] data) {
    ThrowIfDisposed();
    fixed (byte* buffer = data) {
      nint num = LinuxTapeNative.Write(_fd, buffer, (nuint)data.Length);
      if (num < 0) {
        int lastPInvokeError = Marshal.GetLastPInvokeError();
        throw new IOException($"Tape write failed (errno {lastPInvokeError}).");
      }
      return (int)num;
    }
  }

  public unsafe int ReadData(byte[] buffer, int offset, int length) {
    ThrowIfDisposed();
    if (offset < 0 || length < 0 || offset + length > buffer.Length) {
      throw new ArgumentOutOfRangeException("length");
    }
    fixed (byte* buffer2 = &buffer[offset]) {
      nint num = LinuxTapeNative.Read(_fd, buffer2, (nuint)length);
      if (num < 0) {
        int lastPInvokeError = Marshal.GetLastPInvokeError();
        throw new IOException($"Tape read failed (errno {lastPInvokeError}).");
      }
      return (int)num;
    }
  }

  public byte[] ReadData(uint length) {
    byte[] array = new byte[length];
    int num = ReadData(array, 0, array.Length);
    if (num == array.Length) {
      return array;
    }
    byte[] array2 = new byte[num];
    Buffer.BlockCopy(array, 0, array2, 0, num);
    return array2;
  }

  public void WaitForTapeReady() {
    ThrowIfDisposed();
    LinuxTapeNative.Mtget arg = default(LinuxTapeNative.Mtget);
    LinuxTapeNative.ThrowIfFailed(LinuxTapeNative.Ioctl(_fd, LinuxTapeNative.MTIOCGET, ref arg), "MTIOCGET");
  }

  public int Erase(uint type) {
    ThrowIfDisposed();
    try {
      LinuxTapeNative.MtOp(_fd, 13, (type == 1) ? 1 : 0);
      return 0;
    }
    catch (IOException) {
      return Marshal.GetLastPInvokeError();
    }
  }

  public void Prepare(uint operation) {
    ThrowIfDisposed();
    switch (operation) {
      case 0u:
        LinuxTapeNative.MtOp(_fd, 30);
        break;
      case 1u:
        LinuxTapeNative.MtOp(_fd, 7);
        break;
      case 2u:
        LinuxTapeNative.MtOp(_fd, 9);
        break;
    }
  }

  public int WriteMarks(uint type, uint count) {
    ThrowIfDisposed();
    try {
      LinuxTapeNative.MtOp(_fd, 0, (int)count);
      return 0;
    }
    catch (IOException) {
      return Marshal.GetLastPInvokeError();
    }
  }

  public TapePosition GetPosition(uint type, uint partition = 0u, uint offsetLow = 0u, uint offsetHigh = 0u) {
    ThrowIfDisposed();
    try {
      LinuxTapeNative.Mtget arg = default(LinuxTapeNative.Mtget);
      LinuxTapeNative.ThrowIfFailed(LinuxTapeNative.Ioctl(_fd, LinuxTapeNative.MTIOCGET, ref arg), "MTIOCGET");
      return new TapePosition {
        MethodType = type,
        Partition = partition,
        OffsetLow = (uint)arg.MtBlkno,
        OffsetHigh = 0u,
        Error = 0
      };
    }
    catch (IOException) {
      return new TapePosition {
        Error = Marshal.GetLastPInvokeError()
      };
    }
  }

  public void SetPosition(uint method, uint partition = 0u, long offset = 0L) {
    ThrowIfDisposed();
    switch (method) {
      case 0u:
        LinuxTapeNative.MtOp(_fd, 6);
        break;
      case 4u:
        LinuxTapeNative.MtOp(_fd, 12);
        break;
      case 6u:
        if (offset >= 0) {
          LinuxTapeNative.MtOp(_fd, 1, (int)offset);
        }
        else {
          LinuxTapeNative.MtOp(_fd, 2, (int)(-offset));
        }
        break;
      case 5u:
        if (offset >= 0) {
          LinuxTapeNative.MtOp(_fd, 3, (int)offset);
        }
        else {
          LinuxTapeNative.MtOp(_fd, 4, (int)(-offset));
        }
        break;
      default:
        if (method != 2) {
          break;
        }
        goto case 1u;
      case 1u:
        LinuxTapeNative.MtOp(_fd, 6);
        if (offset > 0) {
          LinuxTapeNative.MtOp(_fd, 3, (int)offset);
        }
        break;
    }
  }

  public void SetMediaParams(uint blockSize) {
    ThrowIfDisposed();
    LinuxTapeNative.MtOp(_fd, 20, (int)blockSize);
  }

  public int GetStatus() {
    ThrowIfDisposed();
    try {
      WaitForTapeReady();
      return 0;
    }
    catch {
      return Marshal.GetLastPInvokeError();
    }
  }

  public IReadOnlyList<LTOCmAttribute> ReadCartridgeAttributes() {
    ThrowIfDisposed();
    byte[] array = new byte[32736];
    byte[] cdb = MamAttributeCodec.BuildReadAttributeCdb(32736u);
    LinuxTapeNative.SgIo(_fd, cdb, array, -3);
    return MamAttributeCodec.ParseAttributes(array);
  }

  public void WriteCartridgeAttribute(LTOCmAttribute attribute) {
    ThrowIfDisposed();
    byte[] array = MamAttributeCodec.BuildWriteAttributeParameterList(attribute);
    byte[] cdb = MamAttributeCodec.BuildWriteAttributeCdb((uint)array.Length);
    LinuxTapeNative.SgIo(_fd, cdb, array, -2);
  }

  public void Dispose() {
    if (!_disposed) {
      _disposed = true;
      if (_fd >= 0) {
        LinuxTapeNative.Close(_fd);
      }
    }
  }

  private void ThrowIfDisposed() {
    if (_disposed) {
      throw new ObjectDisposedException("LinuxTapeDrive");
    }
  }
}
