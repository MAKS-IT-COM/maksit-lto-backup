using System.Runtime.InteropServices;

namespace MaksIT.LTO.Core;

// Preparing this file to be able to talk to tape devices using the SCSI interface.
// Sending correct SCSI commands to tape devices will retrieve the LTO-CM Ship Content.
// It is also possible to write to the LTO-CM.
// Check the IBM pdf for instructions.
//      5.2.16 READ ATTRIBUTE - 8Ch
//      5.5 Medium auxiliary memory attributes (MAM).


public partial class TapeDeviceHandler : IDisposable {

    #region Tape IOCTL Commands from ntddscsi.h

    private const uint IOCTL_SCSI_BASE = FILE_DEVICE_CONTROLLER;
    private const uint IOCTL_SCSI_PASS_THROUGH = (IOCTL_SCSI_BASE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x0401 << 2) | METHOD_BUFFERED;
    private const uint IOCTL_SCSI_PASS_THROUGH_DIRECT = (IOCTL_SCSI_BASE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x0405 << 2) | METHOD_BUFFERED;

    #endregion

    #region Tape IOCTL Structures from ntddscsi.h

    // IA Make the following structures so we need to test.

    // This structure is used for SCSI commands
    // It is used to send commands to the tape device.
    // It contains the command descriptor block (CDB) and other parameters.
    // It is used with the IOCTL_SCSI_PASS_THROUGH and IOCTL_SCSI_PASS_THROUGH_DIRECT control codes.
    //
    

    [StructLayout(LayoutKind.Sequential)]
    public struct SCSI_PASS_THROUGH_DIRECT
    {
        public ushort Length;
        public byte ScsiStatus;
        public byte PathId;
        public byte TargetId;
        public byte Lun;
        public byte CdbLength;
        public byte SenseInfoLength;
        public byte DataIn;
        public uint DataTransferLength;
        public uint TimeOutValue;
        public IntPtr DataBuffer;
        public uint SenseInfoOffset;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] Cdb;
    }

    #endregion

    #region Tape IOCTL Methods from ntddscsi.h


    #endregion
}