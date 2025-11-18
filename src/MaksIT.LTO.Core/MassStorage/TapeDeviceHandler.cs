using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

// https://github.com/tpn/winsdk-10

namespace MaksIT.LTO.Core;

public partial class TapeDeviceHandler : IDisposable {

  private readonly ILogger<TapeDeviceHandler> _logger;
  private string _tapeDevicePath;
  private SafeFileHandle _tapeHandle;

  public LTOCartridgeMemory? ltoCartridgeMemory;

  private const uint GENERIC_READ = 0x80000000;
  private const uint GENERIC_WRITE = 0x40000000;
  private const uint OPEN_EXISTING = 3;

  // Define IOCTL base
  private const uint FILE_DEVICE_CONTROLLER = 0x00000004;
  private const uint FILE_DEVICE_TAPE = 0x0000001F;
  private const uint FILE_DEVICE_MASS_STORAGE = 0x0000002D;

  // Define access rights
  private const uint FILE_ANY_ACCESS = 0x0000; // any access
  private const uint FILE_READ_ACCESS = 0x0001; // file & pipe
  private const uint FILE_WRITE_ACCESS = 0x0002; // file & pipe

  // Define method
  private const uint METHOD_BUFFERED = 0;


  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern SafeFileHandle CreateFile(
      string lpFileName,
      uint dwDesiredAccess,
      uint dwShareMode,
      IntPtr lpSecurityAttributes,
      uint dwCreationDisposition,
      uint dwFlagsAndAttributes,
      IntPtr hTemplateFile);

  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern bool DeviceIoControl(
      SafeFileHandle hDevice,
      uint dwIoControlCode,
      IntPtr lpInBuffer,
      uint nInBufferSize,
      IntPtr lpOutBuffer,
      uint nOutBufferSize,
      out uint lpBytesReturned,
      IntPtr lpOverlapped);

  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern bool WriteFile(
      SafeFileHandle hFile,
      IntPtr lpBuffer,
      uint nNumberOfBytesToWrite,
      out uint lpNumberOfBytesWritten,
      IntPtr lpOverlapped);

  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern bool ReadFile(
      SafeFileHandle hFile,
      IntPtr lpBuffer,
      uint nNumberOfBytesToRead,
      out uint lpNumberOfBytesRead,
      IntPtr lpOverlapped);

  public TapeDeviceHandler(
    ILogger<TapeDeviceHandler> logger,
    string tapeDevicePath
  ) {
    _logger = logger;
    _tapeDevicePath = tapeDevicePath;
    OpenTapeDevice(GENERIC_READ | GENERIC_WRITE);
    ltoCartridgeMemory = new LTOCartridgeMemory(this);
    }

  [MemberNotNull(nameof(_tapeHandle))]
  private void OpenTapeDevice(uint desiredAccess) {
    _tapeHandle?.Dispose();
    _tapeHandle = CreateFile(_tapeDevicePath, desiredAccess, 0, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
    if (_tapeHandle.IsInvalid) {
      throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
  }


  public int WriteData(byte[] data) {
    IntPtr unmanagedPointer = Marshal.AllocHGlobal(data.Length);
    try {
      Marshal.Copy(data, 0, unmanagedPointer, data.Length);
      bool result = WriteFile(_tapeHandle, unmanagedPointer, (uint)data.Length, out uint bytesWritten, IntPtr.Zero);

      if (result) {
        Console.WriteLine("Write Data: Success");
      }
      else {
        int error = Marshal.GetLastWin32Error();
        Console.WriteLine($"Write Data: Failed with error code {error}");
        return error;
      }
    }
    finally {
      Marshal.FreeHGlobal(unmanagedPointer);
    }

    return 0;
  }

  public byte[] ReadData(uint length) {
    byte[] data = new byte[length];
    IntPtr unmanagedPointer = Marshal.AllocHGlobal((int)length);
    try {
      bool result = ReadFile(_tapeHandle, unmanagedPointer, length, out uint bytesRead, IntPtr.Zero);

      if (result) {
        Marshal.Copy(unmanagedPointer, data, 0, (int)length);
        Console.WriteLine("Read Data: Success");
      }
      else {
        int error = Marshal.GetLastWin32Error();
        Console.WriteLine($"Read Data: Failed with error code {error}");
      }
    }
    finally {
      Marshal.FreeHGlobal(unmanagedPointer);
    }
    return data;
  }

  public int ReadData(byte[] buffer, int offset, int length) {
    IntPtr unmanagedPointer = Marshal.AllocHGlobal(length);
    try {
      bool result = ReadFile(_tapeHandle, unmanagedPointer, (uint)length, out uint bytesRead, IntPtr.Zero);

      if (result) {
        Marshal.Copy(unmanagedPointer, buffer, offset, (int)bytesRead);
        Console.WriteLine("Read Data: Success");
        return (int)bytesRead;
      }
      else {
        int error = Marshal.GetLastWin32Error();
        Console.WriteLine($"Read Data: Failed with error code {error}");
        return 0;
      }
    }
    finally {
      Marshal.FreeHGlobal(unmanagedPointer);
    }
  }

  public void WaitForTapeReady() {
    bool isReady = false;
    while (!isReady) {
      Console.WriteLine("Checking if tape is ready...");
      int errorCode = GetStatus();
      if (errorCode == 0) // Assuming 0 means success/ready
      {
        isReady = true;
      }
      else {
        Thread.Sleep(1000); // Wait 1 second before checking again
      }
    }
  }




  public void Dispose() {
    _tapeHandle?.Dispose();
  }
}
