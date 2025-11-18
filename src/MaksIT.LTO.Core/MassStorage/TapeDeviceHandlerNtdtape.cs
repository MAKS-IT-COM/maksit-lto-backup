using System.Runtime.InteropServices;

namespace MaksIT.LTO.Core;

public partial class TapeDeviceHandler : IDisposable {

  #region Tape IOCTL Commands from ntddtape.h

  //
  // NtDeviceIoControlFile IoControlCode values for this device.
  //
  // Warning:  Remember that the low two bits of the code specify how the
  //           buffers are passed to the driver!
  //

  private const uint IOCTL_TAPE_ERASE = (FILE_DEVICE_TAPE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x0000 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_TAPE_PREPARE = (FILE_DEVICE_TAPE << 16) | ((FILE_READ_ACCESS) << 14) | (0x0001 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_TAPE_WRITE_MARKS = (FILE_DEVICE_TAPE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x0002 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_TAPE_GET_POSITION = (FILE_DEVICE_TAPE << 16) | (FILE_READ_ACCESS << 14) | (0x0003 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_TAPE_SET_POSITION = (FILE_DEVICE_TAPE << 16) | (FILE_READ_ACCESS << 14) | (0x0004 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_TAPE_GET_DRIVE_PARAMS = (FILE_DEVICE_TAPE << 16) | (FILE_READ_ACCESS << 14) | (0x0005 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_TAPE_SET_DRIVE_PARAMS = (FILE_DEVICE_TAPE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x0006 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_TAPE_GET_MEDIA_PARAMS = (FILE_DEVICE_TAPE << 16) | (FILE_READ_ACCESS << 14) | (0x0007 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_TAPE_SET_MEDIA_PARAMS = (FILE_DEVICE_TAPE << 16) | (FILE_READ_ACCESS << 14) | (0x0008 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_TAPE_GET_STATUS = (FILE_DEVICE_TAPE << 16) | (FILE_READ_ACCESS << 14) | (0x0009 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_TAPE_CREATE_PARTITION = (FILE_DEVICE_TAPE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x000A << 2) | METHOD_BUFFERED;

  //
  // The following device control codes are common for all class drivers.  The
  // functions codes defined here must match all of the other class drivers.
  //
  // Warning: these codes will be replaced in the future with the IOCTL_STORAGE
  // codes included below
  //

  private const uint IOCTL_TAPE_MEDIA_REMOVAL = (FILE_DEVICE_TAPE << 16) | (FILE_READ_ACCESS << 14) | (0x0201 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_TAPE_EJECT_MEDIA = (FILE_DEVICE_TAPE << 16) | (FILE_READ_ACCESS << 14) | (0x0202 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_TAPE_LOAD_MEDIA = (FILE_DEVICE_TAPE << 16) | (FILE_READ_ACCESS << 14) | (0x0203 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_TAPE_RESERVE = (FILE_DEVICE_TAPE << 16) | (FILE_READ_ACCESS << 14) | (0x0204 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_TAPE_RELEASE = (FILE_DEVICE_TAPE << 16) | (FILE_READ_ACCESS << 14) | (0x0205 << 2) | METHOD_BUFFERED;

  private const uint IOCTL_TAPE_CHECK_VERIFY = (FILE_DEVICE_TAPE << 16) | (FILE_READ_ACCESS << 14) | (0x0200 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_TAPE_FIND_NEW_DEVICES = (FILE_DEVICE_TAPE << 16) | (FILE_READ_ACCESS << 14) | (0x0206 << 2) | METHOD_BUFFERED;

  #endregion

  #region Tape IOCTL Structures from ntddtape.h

  //
  // IOCTL_TAPE_ERASE definitions
  //

  public const uint TAPE_ERASE_SHORT = 0;
  public const uint TAPE_ERASE_LONG = 1;

  [StructLayout(LayoutKind.Sequential)]
  public struct TAPE_ERASE {
    public uint Type; // ULONG in C is equivalent to uint in C#
    public byte Immediate; // BOOLEAN in C is typically a byte in C#
  }

  //
  // IOCTL_TAPE_PREPARE definitions
  //

  public const uint TAPE_LOAD = 0;
  public const uint TAPE_UNLOAD = 1;
  public const uint TAPE_TENSION = 2;
  public const uint TAPE_LOCK = 3;
  public const uint TAPE_UNLOCK = 4;
  public const uint TAPE_FORMAT = 5;

  [StructLayout(LayoutKind.Sequential)]
  public struct TAPE_PREPARE {
    public uint Operation; // ULONG in C is equivalent to uint in C#
    public byte Immediate; // BOOLEAN in C is typically a byte in C#
  }

  //
  // IOCTL_TAPE_WRITE_MARKS definitions
  //

  /// <summary>
  /// This type of mark is typically used to denote the end of a set or collection of files rather than individual files. It’s commonly used in situations where data blocks are grouped logically as sets. This can be useful when you want to denote larger logical separations within the tape, but it’s less commonly used for simple end-of-file markers.
  /// </summary>
  public const uint TAPE_SETMARKS = 0;
 
  /// <summary>
  /// This is the standard mark used to indicate the end of a file on the tape. When you have multiple files in a backup, `TAPE_FILEMARKS` is often used between each file or at the end of the backup to signal the end of a logical set of data. For most cases, especially when working with a series of files, this is the most appropriate mark to use to separate or end file data.
  /// </summary>
  public const uint TAPE_FILEMARKS = 1;

  /// <summary>
  /// This is a shorter version of a standard file mark. It’s primarily used when you want to conserve tape space but still need a delimiter between sections. However, not all drives support `TAPE_SHORT_FILEMARKS`, and they may not be as reliable for indicating the end of a data sequence in critical backup scenarios.
  /// </summary>
  public const uint TAPE_SHORT_FILEMARKS = 2;

  /// <summary>
  /// This type of mark takes up more tape space and is a longer version of the file mark, used in situations where a highly visible or robust delimiter is required. This is generally not necessary for most backups but may be useful if you want a very strong physical marker on the tape.
  /// </summary>
  public const uint TAPE_LONG_FILEMARKS = 3;

  [StructLayout(LayoutKind.Sequential)]
  public struct TAPE_WRITE_MARKS {
    public uint Type; // ULONG in C is equivalent to uint in C#
    public uint Count; // ULONG in C is equivalent to uint in C#
    public byte Immediate; // BOOLEAN in C is typically a byte in C#
  }

  //
  // IOCTL_TAPE_SET_POSITION definitions
  //

  public const uint TAPE_ABSOLUTE_POSITION = 0;
  public const uint TAPE_LOGICAL_POSITION = 1;
  public const uint TAPE_PSEUDO_LOGICAL_POSITION = 2;

  [StructLayout(LayoutKind.Sequential)]
  public struct TAPE_GET_POSITION {
    public uint Type;
    public uint Partition;
    public uint OffsetLow;
    public uint OffsetHigh;
  }

  //
  // IOCTL_TAPE_SET_POSITION definitions
  //

  public const uint TAPE_REWIND = 0;
  public const uint TAPE_ABSOLUTE_BLOCK = 1;
  public const uint TAPE_LOGICAL_BLOCK = 2;
  public const uint TAPE_PSEUDO_LOGICAL_BLOCK = 3;
  public const uint TAPE_SPACE_END_OF_DATA = 4;
  public const uint TAPE_SPACE_RELATIVE_BLOCKS = 5;
  public const uint TAPE_SPACE_FILEMARKS = 6;
  public const uint TAPE_SPACE_SEQUENTIAL_FMKS = 7;
  public const uint TAPE_SPACE_SETMARKS = 8;
  public const uint TAPE_SPACE_SEQUENTIAL_SMKS = 9;

  [StructLayout(LayoutKind.Sequential)]
  public struct TAPE_SET_POSITION {
    public uint Method;
    public uint Partition;
    public long Offset;
    public byte Immediate;
  }

  //
  // IOCTL_TAPE_GET_DRIVE_PARAMS definitions
  //

  //
  // Definitions for FeaturesLow parameter
  //

  public const uint TAPE_DRIVE_FIXED = 0x00000001;
  public const uint TAPE_DRIVE_SELECT = 0x00000002;
  public const uint TAPE_DRIVE_INITIATOR = 0x00000004;

  public const uint TAPE_DRIVE_ERASE_SHORT = 0x00000010;
  public const uint TAPE_DRIVE_ERASE_LONG = 0x00000020;
  public const uint TAPE_DRIVE_ERASE_BOP_ONLY = 0x00000040;
  public const uint TAPE_DRIVE_ERASE_IMMEDIATE = 0x00000080;

  public const uint TAPE_DRIVE_TAPE_CAPACITY = 0x00000100;
  public const uint TAPE_DRIVE_TAPE_REMAINING = 0x00000200;
  public const uint TAPE_DRIVE_FIXED_BLOCK = 0x00000400;
  public const uint TAPE_DRIVE_VARIABLE_BLOCK = 0x00000800;

  public const uint TAPE_DRIVE_WRITE_PROTECT = 0x00001000;
  public const uint TAPE_DRIVE_EOT_WZ_SIZE = 0x00002000;

  public const uint TAPE_DRIVE_ECC = 0x00010000;
  public const uint TAPE_DRIVE_COMPRESSION = 0x00020000;
  public const uint TAPE_DRIVE_PADDING = 0x00040000;
  public const uint TAPE_DRIVE_REPORT_SMKS = 0x00080000;

  public const uint TAPE_DRIVE_GET_ABSOLUTE_BLK = 0x00100000;
  public const uint TAPE_DRIVE_GET_LOGICAL_BLK = 0x00200000;
  public const uint TAPE_DRIVE_SET_EOT_WZ_SIZE = 0x00400000;

  public const uint TAPE_DRIVE_EJECT_MEDIA = 0x01000000; //don't use this bit!
                                                         //                                                     //can't be a low features bit!
                                                         //                                                     //reserved; high features only

  //
  // Definitions for FeaturesHigh parameter
  //

  public const uint TAPE_DRIVE_LOAD_UNLOAD = 0x80000001;
  public const uint TAPE_DRIVE_TENSION = 0x80000002;
  public const uint TAPE_DRIVE_LOCK_UNLOCK = 0x80000004;
  public const uint TAPE_DRIVE_REWIND_IMMEDIATE = 0x80000008;

  public const uint TAPE_DRIVE_SET_BLOCK_SIZE = 0x80000010;
  public const uint TAPE_DRIVE_LOAD_UNLD_IMMED = 0x80000040;
  public const uint TAPE_DRIVE_TENSION_IMMED = 0x80000080;
  public const uint TAPE_DRIVE_LOCK_UNLK_IMMED = 0x80000100;

  public const uint TAPE_DRIVE_SET_ECC = 0x80000200;
  public const uint TAPE_DRIVE_SET_COMPRESSION = 0x80000400;
  public const uint TAPE_DRIVE_SET_PADDING = 0x80000800;
  public const uint TAPE_DRIVE_SET_REPORT_SMKS = 0x80001000;

  public const uint TAPE_DRIVE_ABSOLUTE_BLK = 0x80002000;
  public const uint TAPE_DRIVE_ABS_BLK_IMMED = 0x80004000;
  public const uint TAPE_DRIVE_LOGICAL_BLK = 0x80008000;
  public const uint TAPE_DRIVE_LOG_BLK_IMMED = 0x80010000;

  public const uint TAPE_DRIVE_END_OF_DATA = 0x80020000;
  public const uint TAPE_DRIVE_RELATIVE_BLKS = 0x80040000;
  public const uint TAPE_DRIVE_FILEMARKS = 0x80080000;
  public const uint TAPE_DRIVE_SEQUENTIAL_FMKS = 0x80100000;

  public const uint TAPE_DRIVE_SETMARKS = 0x80200000;
  public const uint TAPE_DRIVE_SEQUENTIAL_SMKS = 0x80400000;
  public const uint TAPE_DRIVE_REVERSE_POSITION = 0x80800000;
  public const uint TAPE_DRIVE_SPACE_IMMEDIATE = 0x81000000;

  public const uint TAPE_DRIVE_WRITE_SETMARKS = 0x82000000;
  public const uint TAPE_DRIVE_WRITE_FILEMARKS = 0x84000000;
  public const uint TAPE_DRIVE_WRITE_SHORT_FMKS = 0x88000000;
  public const uint TAPE_DRIVE_WRITE_LONG_FMKS = 0x90000000;

  public const uint TAPE_DRIVE_WRITE_MARK_IMMED = 0xA0000000;
  public const uint TAPE_DRIVE_FORMAT = 0xC0000000;
  public const uint TAPE_DRIVE_FORMAT_IMMEDIATE = 0x80000000;
  public const uint TAPE_DRIVE_HIGH_FEATURES = 0x80000000; //mask for high features flag

  [StructLayout(LayoutKind.Sequential)]
  public struct TAPE_GET_DRIVE_PARAMETERS {
    public uint ECC;
    public uint Compression;
    public uint DataPadding;
    public uint ReportSetmarks;
    public uint DefaultBlockSize;
    public uint MaximumBlockSize;
    public uint MinimumBlockSize;
    public uint MaximumPartitionCount;
    public uint FeaturesLow;
    public uint FeaturesHigh;
    public uint EOTWarningZoneSize;
  }

  //
  // IOCTL_TAPE_SET_DRIVE_PARAMETERS definitions
  //

  [StructLayout(LayoutKind.Sequential)]
  public struct TAPE_SET_DRIVE_PARAMETERS {
    public uint ECC;
    public uint Compression;
    public uint DataPadding;
    public uint ReportSetmarks;
    public uint EOTWarningZoneSize;
  }

  //
  // IOCTL_TAPE_GET_MEDIA_PARAMETERS definitions
  //

  [StructLayout(LayoutKind.Sequential)]
  public struct TAPE_GET_MEDIA_PARAMETERS {
    public uint Capacity;
    public uint Remaining;
    public uint BlockSize;
    public uint PartitionCount;
    public byte WriteProtected;
  }

  //
  // IOCTL_TAPE_SET_MEDIA_PARAMETERS definitions
  //

  [StructLayout(LayoutKind.Sequential)]
  public struct TAPE_SET_MEDIA_PARAMETERS {
    public uint BlockSize;
  }

  //
  // IOCTL_TAPE_CREATE_PARTITION definitions
  //

  public const uint TAPE_FIXED_PARTITIONS = 0;
  public const uint TAPE_SELECT_PARTITIONS = 1;
  public const uint TAPE_INITIATOR_PARTITIONS = 2;

  [StructLayout(LayoutKind.Sequential)]
  public struct TAPE_CREATE_PARTITION {
    public uint Method;
    public uint Count;
    public uint Size;
  }

  //
  // WMI Methods
  //

  public const uint TAPE_QUERY_DRIVE_PARAMETERS = 0;
  public const uint TAPE_QUERY_MEDIA_CAPACITY = 1;
  public const uint TAPE_CHECK_FOR_DRIVE_PROBLEM = 2;
  public const uint TAPE_QUERY_IO_ERROR_DATA = 3;
  public const uint TAPE_QUERY_DEVICE_ERROR_DATA = 4;

  [StructLayout(LayoutKind.Sequential)]
  public struct TAPE_WMI_OPERATIONS {
    public uint Method;
    public uint DataBufferSize;
    public IntPtr DataBuffer;
  }

  //
  // Type of drive errors
  //

  public enum TAPE_DRIVE_PROBLEM_TYPE {
    TapeDriveProblemNone,
    TapeDriveReadWriteWarning,
    TapeDriveReadWriteError,
    TapeDriveReadWarning,
    TapeDriveWriteWarning,
    TapeDriveReadError,
    TapeDriveWriteError,
    TapeDriveHardwareError,
    TapeDriveUnsupportedMedia,
    TapeDriveScsiConnectionError,
    TapeDriveTimetoClean,
    TapeDriveCleanDriveNow,
    TapeDriveMediaLifeExpired,
    TapeDriveSnappedTape
  }
  #endregion

  #region Tape IOCTL Methods from ntddtape.h

  /// <summary>
  /// Erase the tape
  /// </summary>
  /// <param name="type">The type of erase operation. Valid values are <see cref="TAPE_ERASE_SHORT"/> and <see cref="TAPE_ERASE_LONG"/>.</param>
  public int Erase(uint type) {
    TAPE_ERASE erase = new TAPE_ERASE {
      Type = type,
      Immediate = 0
    };

    IntPtr inBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(erase));

    try {
      Marshal.StructureToPtr(erase, inBuffer, false);
      bool result = DeviceIoControl(_tapeHandle, IOCTL_TAPE_ERASE, inBuffer, (uint)Marshal.SizeOf(erase), IntPtr.Zero, 0, out uint bytesReturned, IntPtr.Zero);

      if (result) {
        Console.WriteLine($"Erase Tape ({type}): Success");
      }
      else {
        int error = Marshal.GetLastWin32Error();
        Console.WriteLine($"Erase Tape: Failed with error code {error}");
        return error;
      }
    }
    finally {
      Marshal.FreeHGlobal(inBuffer);
    }

    return 0;
  }

  /// <summary>
  /// Prepare the tape for a specific operation
  /// </summary>
  /// <param name="operation">The type of prepare operation. Valid values are <see cref="TAPE_LOAD"/>, <see cref="TAPE_UNLOAD"/>, <see cref="TAPE_UNLOCK"/> and <see cref="TAPE_FORMAT"/>.</param>
  public void Prepare(uint operation) {
    TAPE_PREPARE prepare = new TAPE_PREPARE {
      Operation = operation,
      Immediate = 0
    };

    IntPtr inBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(prepare));

    try {
      Marshal.StructureToPtr(prepare, inBuffer, false);
      bool result = DeviceIoControl(_tapeHandle, IOCTL_TAPE_PREPARE, inBuffer, (uint)Marshal.SizeOf(prepare), IntPtr.Zero, 0, out uint bytesReturned, IntPtr.Zero);

      if (result) {
        Console.WriteLine($"Prepare Tape ({operation}): Success");
      }
      else {
        int error = Marshal.GetLastWin32Error();
        Console.WriteLine($"Prepare Tape: Failed with error code {error}");
      }
    }
    finally {
      Marshal.FreeHGlobal(inBuffer);
    }
  }

  /// <summary>
  /// Write tape marks.
  /// For marking the end of a backup of multiple files, **`TAPE_FILEMARKS`** is generally the most appropriate choice. It is the standard file delimiter used for distinguishing individual files or logical breaks on the tape. 
  /// If you need a more significant delimiter, such as at the end of an entire backup set, you might consider adding a **`TAPE_SETMARKS`** after the `TAPE_FILEMARKS`, but typically, `TAPE_FILEMARKS` alone is sufficient for marking the end of file sequences in backups.
  /// <param name="type">The type of marks to write. Valid values are <see cref="TAPE_SETMARKS"/>, <see cref="TAPE_FILEMARKS"/>, <see cref="TAPE_SHORT_FILEMARKS"/> and <see cref="TAPE_LONG_FILEMARKS"/>.</param>
  /// <param name="count">The number of marks to write.</param>
  public int WriteMarks(uint type, uint count) {
    TAPE_WRITE_MARKS marks = new TAPE_WRITE_MARKS {
      Type = type,
      Count = count,
      Immediate = 0
    };

    IntPtr inBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(marks));

    try {
      Marshal.StructureToPtr(marks, inBuffer, false);
      bool result = DeviceIoControl(_tapeHandle, IOCTL_TAPE_WRITE_MARKS, inBuffer, (uint)Marshal.SizeOf(marks), IntPtr.Zero, 0, out uint bytesReturned, IntPtr.Zero);

      if (result) {
        Console.WriteLine("Write Marks: Success");
        return 0;
      }
      else {
        int error = Marshal.GetLastWin32Error();
        Console.WriteLine($"Write Marks: Failed with error code {error}");
        return error;
      }
    }
    finally {
      Marshal.FreeHGlobal(inBuffer);
    }
  }


  public class TapePosition {
    public uint? MethodType { get; set; }
    public uint? Partition { get; set; }
    public uint? OffsetLow { get; set; }
    public uint? OffsetHigh { get; set; }
    public int? Error { get; set; }
  }


  /// <summary>
  /// Get the current tape position
  /// </summary>
  /// <param name="type">The type of position to get. Valid values are <see cref="TAPE_ABSOLUTE_POSITION"/>, <see cref="TAPE_LOGICAL_POSITION"/> and <see cref="TAPE_PSEUDO_LOGICAL_POSITION"/>.</param>
  /// <param name="partition">The partition number.</param>
  /// <param name="offsetLow">The low offset value.</param>
  /// <param name="offsetHigh">The high offset value.</param>
  /// <returns>The tape position <see cref="TapePosition"/>.</returns>
  public TapePosition GetPosition(uint type, uint partition = 0, uint offsetLow = 0, uint offsetHigh = 0) {
    TAPE_GET_POSITION position = new TAPE_GET_POSITION {
      Type = 0,
      Partition = partition,
      OffsetLow = offsetLow,
      OffsetHigh = offsetHigh
    };

    IntPtr outBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(position));

    try {
      bool result = DeviceIoControl(_tapeHandle, IOCTL_TAPE_GET_POSITION, IntPtr.Zero, 0, outBuffer, (uint)Marshal.SizeOf(position), out uint bytesReturned, IntPtr.Zero);

      if (result) {
        position = Marshal.PtrToStructure<TAPE_GET_POSITION>(outBuffer);
        Console.WriteLine("Get Position: Success");
        Console.WriteLine($"Type: {position.Type}");
        Console.WriteLine($"Partition: {position.Partition}");
        Console.WriteLine($"OffsetLow: {position.OffsetLow}");
        Console.WriteLine($"OffsetHigh: {position.OffsetHigh}");

        return new TapePosition {
          MethodType = position.Type,
          Partition = position.Partition,
          OffsetLow = position.OffsetLow,
          OffsetHigh = position.OffsetHigh
        };
      }
      else {
        int error = Marshal.GetLastWin32Error();
        Console.WriteLine($"Get Position: Failed with error code {error}");

        return new TapePosition {
          Error = error
        };
      }
    }
    finally {
      Marshal.FreeHGlobal(outBuffer);
    }
  }

  /// <summary>
  /// Set the tape position
  /// </summary>
  /// <param name="method">The method to use for setting the position. Valid values are <see cref="TAPE_REWIND"/>, <see cref="TAPE_ABSOLUTE_BLOCK"/>, <see cref="TAPE_LOGICAL_BLOCK"/>, <see cref="TAPE_PSEUDO_LOGICAL_BLOCK"/>, <see cref="TAPE_SPACE_END_OF_DATA"/>, <see cref="TAPE_SPACE_RELATIVE_BLOCKS"/>, <see cref="TAPE_SPACE_FILEMARKS"/>, <see cref="TAPE_SPACE_SEQUENTIAL_FMKS"/>, <see cref="TAPE_SPACE_SETMARKS"/> and <see cref="TAPE_SPACE_SEQUENTIAL_SMKS"/>.</param>
  /// <param name="partition">The partition number.</param>
  /// <param name="offset">The offset value.</param> 
  public void SetPosition(uint method, uint partition = 0, long offset = 0) {
    TAPE_SET_POSITION position = new TAPE_SET_POSITION {
      Method = method,
      Partition = partition,
      Offset = offset,
      Immediate = 0
    };

    IntPtr inBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(position));
    try {
      Marshal.StructureToPtr(position, inBuffer, false);
      bool result = DeviceIoControl(_tapeHandle, IOCTL_TAPE_SET_POSITION, inBuffer, (uint)Marshal.SizeOf(position), IntPtr.Zero, 0, out uint bytesReturned, IntPtr.Zero);

      if (result) {
        Console.WriteLine($"Set Position ({method}): Success");
      }
      else {
        int error = Marshal.GetLastWin32Error();
        Console.WriteLine($"Set Position ({method}): Failed with error code {error}");
      }
    }
    finally {
      Marshal.FreeHGlobal(inBuffer);
    }
  }

  /// <summary>
  /// Get the drive parameters
  /// </summary>
  /// <param name="maxBlockSize">The maximum block size supported by the drive.</param>
  /// <param name="minBlockSize">The minimum block size supported by the drive.</param> 
  public void GetDriveParameters(out uint minBlockSize, out uint maxBlockSize) {
    TAPE_GET_DRIVE_PARAMETERS driveParams = new TAPE_GET_DRIVE_PARAMETERS {
      ECC = 0,
      Compression = 0,
      DataPadding = 0,
      ReportSetmarks = 0,
      DefaultBlockSize = 0,
      MaximumBlockSize = 0,
      MinimumBlockSize = 0,
      MaximumPartitionCount = 0,
      FeaturesLow = 0,
      FeaturesHigh = 0,
      EOTWarningZoneSize = 0
    };

    IntPtr outBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(driveParams));
    try {
      bool result = DeviceIoControl(_tapeHandle, IOCTL_TAPE_GET_DRIVE_PARAMS, IntPtr.Zero, 0, outBuffer, (uint)Marshal.SizeOf(driveParams), out uint bytesReturned, IntPtr.Zero);

      if (result) {
        driveParams = Marshal.PtrToStructure<TAPE_GET_DRIVE_PARAMETERS>(outBuffer);
        minBlockSize = driveParams.MinimumBlockSize;
        maxBlockSize = driveParams.MaximumBlockSize;
        Console.WriteLine($"Drive Parameters: MinBlockSize = {minBlockSize}, MaxBlockSize = {maxBlockSize}");
      }
      else {
        int error = Marshal.GetLastWin32Error();
        Console.WriteLine($"Get Drive Parameters Failed with error code {error}");
        minBlockSize = 0;
        maxBlockSize = 0;
      }
    }
    finally {
      Marshal.FreeHGlobal(outBuffer);
    }
  }

  /// <summary>
  /// Set the drive parameters
  /// </summary>
  /// <param name="ecc">The error correction code (ECC) setting.</param>
  /// <param name="compression">The compression setting.</param>
  /// <param name="dataPadding">The data padding setting.</param>
  /// <param name="reportSetmarks">The report setmarks setting.</param>
  /// <param name="eotWarningZoneSize">The end-of-tape (EOT) warning zone size setting.</param> 
  public void SetDriveParams(uint ecc, uint compression = 0, uint dataPadding = 0, uint reportSetmarks = 0, uint eotWarningZoneSize = 0) {
    TAPE_SET_DRIVE_PARAMETERS driveParams = new TAPE_SET_DRIVE_PARAMETERS {
      ECC = ecc,
      Compression = compression,
      DataPadding = dataPadding,
      ReportSetmarks = reportSetmarks,
      EOTWarningZoneSize = eotWarningZoneSize
    };

    IntPtr inBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(driveParams));

    try {
      Marshal.StructureToPtr(driveParams, inBuffer, false);
      bool result = DeviceIoControl(_tapeHandle, IOCTL_TAPE_SET_DRIVE_PARAMS, inBuffer, (uint)Marshal.SizeOf(driveParams), IntPtr.Zero, 0, out uint bytesReturned, IntPtr.Zero);

      if (result) {
        Console.WriteLine("Set Drive Params: Success");
      }
      else {
        int error = Marshal.GetLastWin32Error();
        Console.WriteLine($"Set Drive Params Failed with error code {error}");
      }
    }
    finally {
      Marshal.FreeHGlobal(inBuffer);
    }
  }

  public void GetMediaParams(uint capacity = 0, uint remaining = 0, uint blockSize = 0, uint partitionCount = 0, byte writeProtected = 0) {
    TAPE_GET_MEDIA_PARAMETERS mediaParams = new TAPE_GET_MEDIA_PARAMETERS {
      Capacity = 0,
      Remaining = 0,
      BlockSize = 0,
      PartitionCount = 0,
      WriteProtected = 0
    };

    IntPtr outBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(mediaParams));

    try {
      bool result = DeviceIoControl(_tapeHandle, IOCTL_TAPE_GET_MEDIA_PARAMS, IntPtr.Zero, 0, outBuffer, (uint)Marshal.SizeOf(mediaParams), out uint bytesReturned, IntPtr.Zero);

      if (result) {
        mediaParams = Marshal.PtrToStructure<TAPE_GET_MEDIA_PARAMETERS>(outBuffer);
        Console.WriteLine("Get Media Params: Success");
        Console.WriteLine($"Capacity: {mediaParams.Capacity}");
        Console.WriteLine($"Remaining: {mediaParams.Remaining}");
        Console.WriteLine($"BlockSize: {mediaParams.BlockSize}");
        Console.WriteLine($"PartitionCount: {mediaParams.PartitionCount}");
        Console.WriteLine($"WriteProtected: {mediaParams.WriteProtected}");
      }
      else {
        int error = Marshal.GetLastWin32Error();
        Console.WriteLine($"Get Media Params Failed with error code {error}");
      }
    }
    finally {
      Marshal.FreeHGlobal(outBuffer);
    }
  }

  public void SetMediaParams(uint blockSize) {
    TAPE_SET_MEDIA_PARAMETERS mediaParams = new TAPE_SET_MEDIA_PARAMETERS {
      BlockSize = blockSize
    };

    IntPtr inBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(mediaParams));

    try {
      Marshal.StructureToPtr(mediaParams, inBuffer, false);
      bool result = DeviceIoControl(_tapeHandle, IOCTL_TAPE_SET_MEDIA_PARAMS, inBuffer, (uint)Marshal.SizeOf(mediaParams), IntPtr.Zero, 0, out uint bytesReturned, IntPtr.Zero);

      if (!result) {
        int error = Marshal.GetLastWin32Error();
        Console.WriteLine($"Set Block Size Failed with error code {error}");
      }
      else {
        Console.WriteLine($"Set Block Size ({blockSize}): Success");
      }
    }
    finally {
      Marshal.FreeHGlobal(inBuffer);
    }
  }

  public int GetStatus() {
    bool result = DeviceIoControl(_tapeHandle, IOCTL_TAPE_GET_STATUS, IntPtr.Zero, 0, IntPtr.Zero, 0, out uint bytesReturned, IntPtr.Zero);

    if (result) {
      Console.WriteLine("Get Status: Success");
    }
    else {
      int error = Marshal.GetLastWin32Error();
      Console.WriteLine($"Get Status: Failed with error code {error}");

      return error;
    }

    return 0;
  }

  public void CreatePartition(uint method = 0, uint count = 0, uint size = 0) {
    TAPE_CREATE_PARTITION partition = new TAPE_CREATE_PARTITION {
      Method = 0,
      Count = 1,
      Size = 0
    };

    bool result = DeviceIoControl(_tapeHandle, IOCTL_TAPE_CREATE_PARTITION, IntPtr.Zero, 0, IntPtr.Zero, 0, out uint bytesReturned, IntPtr.Zero);

    if (!result) {
      int error = Marshal.GetLastWin32Error();
      Console.WriteLine($"Create Partition: Failed with error code {error}");
    }
    else {
      Console.WriteLine("Create Partition: Success");
    }
  }
  #endregion
}