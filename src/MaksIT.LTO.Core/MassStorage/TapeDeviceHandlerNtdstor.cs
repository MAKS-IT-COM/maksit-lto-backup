using System.Runtime.InteropServices;


namespace MaksIT.LTO.Core;

public partial class TapeDeviceHandler : IDisposable {

  #region Storage IOCTL Commands from ntddstor.h

  //
  // The following device control codes are common for all class drivers.  They
  // should be used in place of the older IOCTL_DISK, IOCTL_CDROM and IOCTL_TAPE
  // common codes
  //
  private const uint IOCTL_STORAGE_CHECK_VERIFY = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_READ_ACCESS << 14) | (0x0200 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_CHECK_VERIFY2 = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0200 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_MEDIA_REMOVAL = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_READ_ACCESS << 14) | (0x0201 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_EJECT_MEDIA = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_READ_ACCESS << 14) | (0x0202 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_LOAD_MEDIA = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_READ_ACCESS << 14) | (0x0203 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_LOAD_MEDIA2 = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0203 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_RESERVE = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_READ_ACCESS << 14) | (0x0204 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_RELEASE = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_READ_ACCESS << 14) | (0x0205 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_FIND_NEW_DEVICES = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_READ_ACCESS << 14) | (0x0206 << 2) | METHOD_BUFFERED;

  private const uint IOCTL_STORAGE_EJECTION_CONTROL = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0250 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_MCN_CONTROL = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0251 << 2) | METHOD_BUFFERED;

  private const uint IOCTL_STORAGE_GET_MEDIA_TYPES = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0300 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_GET_MEDIA_TYPES_EX = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0301 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_GET_MEDIA_SERIAL_NUMBER = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0304 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_GET_HOTPLUG_INFO = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0305 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_SET_HOTPLUG_INFO = (FILE_DEVICE_MASS_STORAGE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x0306 << 2) | METHOD_BUFFERED;

  private const uint IOCTL_STORAGE_RESET_BUS = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_READ_ACCESS << 14) | (0x0400 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_RESET_DEVICE = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_READ_ACCESS << 14) | (0x0401 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_BREAK_RESERVATION = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_READ_ACCESS << 14) | (0x0405 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_PERSISTENT_RESERVE_IN = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_READ_ACCESS << 14) | (0x0406 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_PERSISTENT_RESERVE_OUT = (FILE_DEVICE_MASS_STORAGE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x0407 << 2) | METHOD_BUFFERED;

  //
  // This IOCTL includes the same information as IOCTL_STORAGE_GET_DEVICE_NUMBER, plus the device GUID.
  //
  private const uint IOCTL_STORAGE_GET_DEVICE_NUMBER = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0420 << 2) | METHOD_BUFFERED;


  private const uint IOCTL_STORAGE_PREDICT_FAILURE = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0440 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_FAILURE_PREDICTION_CONFIG = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0441 << 2) | METHOD_BUFFERED;

  //
  // This IOCTL retrieves reliability counters for a device.
  //
  private const uint IOCTL_STORAGE_GET_COUNTERS = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x442 << 2) | METHOD_BUFFERED;

  private const uint IOCTL_STORAGE_READ_CAPACITY = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0450 << 2) | METHOD_BUFFERED;

  //
  // IOCTLs 0x0463 to 0x0468 reserved for dependent disk support.
  //

  //
  // IOCTLs 0x0470 to 0x047f reserved for device and stack telemetry interfaces
  //

  private const uint IOCTL_STORAGE_GET_DEVICE_TELEMETRY = (FILE_DEVICE_MASS_STORAGE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x0470 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_DEVICE_TELEMETRY_NOTIFY = (FILE_DEVICE_MASS_STORAGE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x0471 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_DEVICE_TELEMETRY_QUERY_CAPS = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0472 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_GET_DEVICE_TELEMETRY_RAW = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0473 << 2) | METHOD_BUFFERED;

  private const uint IOCTL_STORAGE_SET_TEMPERATURE_THRESHOLD = (FILE_DEVICE_MASS_STORAGE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x0480 << 2) | METHOD_BUFFERED;

  private const uint IOCTL_STORAGE_PROTOCOL_COMMAND = (FILE_DEVICE_MASS_STORAGE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x04F0 << 2) | METHOD_BUFFERED;

  private const uint IOCTL_STORAGE_QUERY_PROPERTY = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0500 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_MANAGE_DATA_SET_ATTRIBUTES = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_WRITE_ACCESS << 14) | (0x0501 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_GET_LB_PROVISIONING_MAP_RESOURCES = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0502 << 2) | METHOD_BUFFERED;

  //
  // IOCTLs 0x0503 to 0x0580 reserved for Enhanced Storage devices.
  //

  //
  // This IOCTL offloads the erasure process to the storage device. There is no guarantee as to the successful
  // deletion or recoverability of the data on the storage device after command completion. This IOCTL is limited
  // to data disks in regular Windows. In WinPE, this IOCTL is supported for both boot and data disks.
  //
  // Initial implementation requires no input and returns no output other than status. Callers should first
  // call FSCTL_LOCK_VOLUME before calling this ioctl to flush out cached data in upper layers. No waiting of
  // outstanding request completion is done before issuing the command to the device.
  //
  private const uint IOCTL_STORAGE_REINITIALIZE_MEDIA = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_WRITE_ACCESS << 14) | (0x0503 << 2) | METHOD_BUFFERED;

  //
  // IOCTLs for bandwidth contracts on storage devices
  // (Move this to ntddsfio if we decide to use a new base)
  //

  private const uint IOCTL_STORAGE_GET_BC_PROPERTIES = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_READ_ACCESS << 14) | (0x0600 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_ALLOCATE_BC_STREAM = (FILE_DEVICE_MASS_STORAGE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x0601 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_FREE_BC_STREAM = (FILE_DEVICE_MASS_STORAGE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x0602 << 2) | METHOD_BUFFERED;

  //
  // IOCTL to check for priority support
  //
  private const uint IOCTL_STORAGE_CHECK_PRIORITY_HINT_SUPPORT = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0620 << 2) | METHOD_BUFFERED;

  //
  // IOCTL for data integrity check support
  //

  private const uint IOCTL_STORAGE_START_DATA_INTEGRITY_CHECK = (FILE_DEVICE_MASS_STORAGE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x0621 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_STOP_DATA_INTEGRITY_CHECK = (FILE_DEVICE_MASS_STORAGE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x0622 << 2) | METHOD_BUFFERED;

  //
  // These ioctl codes are obsolete.  They are defined here to avoid resuing them
  // and to allow class drivers to respond to them more easily.
  //

  private const uint OBSOLETE_IOCTL_STORAGE_RESET_BUS = (FILE_DEVICE_MASS_STORAGE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x0400 << 2) | METHOD_BUFFERED;
  private const uint OBSOLETE_IOCTL_STORAGE_RESET_DEVICE = (FILE_DEVICE_MASS_STORAGE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x0401 << 2) | METHOD_BUFFERED;

  //
  // IOCTLs 0x0643 to 0x0655 reserved for VHD disk support.
  //


  //
  // IOCTLs for firmware upgrade on storage devices
  //

  private const uint IOCTL_STORAGE_FIRMWARE_GET_INFO = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0700 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_FIRMWARE_DOWNLOAD = (FILE_DEVICE_MASS_STORAGE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x0701 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_FIRMWARE_ACTIVATE = (FILE_DEVICE_MASS_STORAGE << 16) | ((FILE_READ_ACCESS | FILE_WRITE_ACCESS) << 14) | (0x0702 << 2) | METHOD_BUFFERED;

  //
  // IOCTL to support Idle Power Management, including Device Wake
  //
  private const uint IOCTL_STORAGE_ENABLE_IDLE_POWER = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0720 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_GET_IDLE_POWERUP_REASON = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0721 << 2) | METHOD_BUFFERED;

  //
  // IOCTLs to allow class drivers to acquire and release active references on
  // a unit.  These should only be used if the class driver previously sent a
  // successful IOCTL_STORAGE_ENABLE_IDLE_POWER request to the port driver.
  //
  private const uint IOCTL_STORAGE_POWER_ACTIVE = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0722 << 2) | METHOD_BUFFERED;
  private const uint IOCTL_STORAGE_POWER_IDLE = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0723 << 2) | METHOD_BUFFERED;

  //
  // This IOCTL indicates that the physical device has triggered some sort of event.
  //
  private const uint IOCTL_STORAGE_EVENT_NOTIFICATION = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0724 << 2) | METHOD_BUFFERED;

  //
  // IOCTL to specify a power cap for a storage device.
  //
  private const uint IOCTL_STORAGE_DEVICE_POWER_CAP = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0725 << 2) | METHOD_BUFFERED;

  //
  // IOCTL to send commands to the RPMB for a storage device.
  //
  private const uint IOCTL_STORAGE_RPMB_COMMAND = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0726 << 2) | METHOD_BUFFERED;

  //
  // IOCTL to manage attributes for storage devices
  //
  private const uint IOCTL_STORAGE_ATTRIBUTE_MANAGEMENT = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0727 << 2) | METHOD_BUFFERED;

  //
  // IOCTL_STORAGE_DIAGNOSTIC IOCTL to query diagnostic data from the storage driver stack
  //
  private const uint IOCTL_STORAGE_DIAGNOSTIC = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0728 << 2) | METHOD_BUFFERED;

  //
  // IOCTLs for storage device depopulation support.
  //

  //
  // IOCTL_STORAGE_GET_PHYSICAL_ELEMENT_STATUS IOCTL to query physical element status from device.
  //
  private const uint IOCTL_STORAGE_GET_PHYSICAL_ELEMENT_STATUS = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0729 << 2) | METHOD_BUFFERED;

  //
  // IOCTL_STORAGE_SET_PHYSICAL_ELEMENT_STATUS IOCTL to set physical element status on device.
  //
  private const uint IOCTL_STORAGE_REMOVE_ELEMENT_AND_TRUNCATE = (FILE_DEVICE_MASS_STORAGE << 16) | (FILE_ANY_ACCESS << 14) | (0x0730 << 2) | METHOD_BUFFERED;

  #endregion

  #region Storage IOCTL Structures from ntddstor.h

  //
  // Note: Function code values of less than 0x800 are reserved for Microsoft. Values of 0x800 and higher can be used by vendors.
  //       So do not use function code of 0x800 and higher to define new IOCTLs in this file.
  //


  //
  // IOCTL_STORAGE_GET_HOTPLUG_INFO
  //

  [StructLayout(LayoutKind.Sequential)]
  public struct STORAGE_HOTPLUG_INFO {
    public uint Size; // version
    public bool MediaRemovable; // ie. zip, jaz, cdrom, mo, etc. vs hdd
    public bool MediaHotplug; // ie. does the device succeed a lock even though its not lockable media?
    public bool DeviceHotplug; // ie. 1394, USB, etc.
    public bool WriteCacheEnableOverride; // This field should not be relied upon because it is no longer used
  }

  //
  // IOCTL_STORAGE_GET_DEVICE_NUMBER
  //
  // input - none
  //
  // output - STORAGE_DEVICE_NUMBER structure
  //          The values in the STORAGE_DEVICE_NUMBER structure are guaranteed
  //          to remain unchanged until the system is rebooted.  They are not
  //          guaranteed to be persistant across boots.

  [StructLayout(LayoutKind.Sequential)]
  public struct STORAGE_DEVICE_NUMBER {
    //
    // The FILE_DEVICE_XXX type for this device.
    //

    public uint DeviceType;

    //
    // The number of this device
    //

    public uint DeviceNumber;

    //
    // If the device is partitionable, the partition number of the device.
    // Otherwise -1
    //

    public uint PartitionNumber;
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct _STORAGE_DEVICE_NUMBERS {
    public uint NumberOfDeviceNumbers;
    public STORAGE_DEVICE_NUMBER DeviceNumber;
  }

  //
  // IOCTL_STORAGE_GET_DEVICE_NUMBER_EX
  //
  // input - none
  //
  // output - STORAGE_DEVICE_NUMBER_EX structure
  //

  //
  // Possible flags that can be set in Flags field of
  // STORAGE_DEVICE_NUMBER_EX structure defined below
  //

  //
  // This flag indicates that deviceguid is randomly created because a deviceguid conflict was observed
  //
  public const uint STORAGE_DEVICE_FLAGS_RANDOM_DEVICEGUID = 0x1;

  //
  // This flag indicates that deviceguid is randomly created because the HW ID was not available
  //
  public const uint STORAGE_DEVICE_FLAGS_DEFAULT_DEVICEGUID = 0x2;

  //
  // This flag indicates that deviceguid is created from the scsi page83 data.
  // If this flag is not set this implies it's created from serial number or is randomly generated.
  //
  public const uint STORAGE_DEVICE_FLAGS_DEVICEGUID_FROM_PAGE83 = 0x4;

  [StructLayout(LayoutKind.Sequential)]
  public struct STORAGE_DEVICE_NUMBER_EX {
    //
    // Sizeof(STORAGE_DEVICE_NUMBER_EX).
    //
    public uint Version;

    //
    // Total size of the structure, including any additional data. Currently
    // this will always be the same as sizeof(STORAGE_DEVICE_NUMBER_EX).
    //
    public uint Size;

    //
    // Flags for the device
    //

    public uint Flags;

    //
    // The FILE_DEVICE_XXX type for this device.
    //

    public uint DeviceType;

    //
    // The number of this device
    //

    public uint DeviceNumber;

    //
    // A globally-unique identification number for this device.
    // A GUID of {0} indicates that a GUID could not be generated. The GUID
    // is based on hardware information that doesn't change with firmware updates
    // (for instance, serial number can be used to form the GUID, but not the firmware
    // revision). The device GUID remains the same across reboots.
    //
    // In general, if a device exposes a globally unique identifier, the storage driver
    // will use that identifier to form the GUID. Otherwise, the storage driver will combine
    // the device's vendor ID, product ID and serial number to create the GUID.
    //
    // If a storage driver detects two devices with the same hardware information (which is
    // an indication of a problem with the device), the driver will generate a random GUID for
    // one of the two devices. When handling IOCTL_STORAGE_GET_DEVICE_NUMBER_EX for the device
    // with the random GUID, the driver will add STORAGE_DEVICE_FLAGS_RANDOM_DEVICEGUID_REASON_CONFLICT
    // to the Flags member of this structure.
    //
    // If a storage device does not provide any identifying information, the driver will generate a random
    // GUID and add STORAGE_DEVICE_FLAGS_RANDOM_DEVICEGUID_REASON_NOHWID to the Flags member of this structure.
    //
    // A random GUID is not persisted and will not be the same after a reboot.
    //

    public Guid DeviceGuid;


    //
    // If the device is partitionable, the partition number of the device.
    // Otherwise -1
    //

    public uint PartitionNumber;
  }

  //
  // Define the structures for scsi resets
  //

  [StructLayout(LayoutKind.Sequential)]
  public struct STORAGE_BUS_RESET_REQUEST {
    public byte PathId;
  }

  //
  // Break reservation is sent to the Adapter/FDO with the given lun information.
  //

  [StructLayout(LayoutKind.Sequential)]
  public struct STORAGE_BREAK_RESERVATION_REQUEST {
    public uint Length;
    public byte _unused;

    public byte PathId;

    public byte TargetId;

    public byte Lun;
  }

  //
  // IOCTL_STORAGE_MEDIA_REMOVAL disables the mechanism
  // on a storage device that ejects media. This function
  // may or may not be supported on storage devices that
  // support removable media.
  //
  // TRUE means prevent media from being removed.
  // FALSE means allow media removal.
  //

  [StructLayout(LayoutKind.Sequential)]
  public struct PREVENT_MEDIA_REMOVAL {
    public bool PreventMediaRemoval;
  }

  //
  //  This is the format of TARGET_DEVICE_CUSTOM_NOTIFICATION.CustomDataBuffer
  //  passed to applications by the classpnp autorun code (via IoReportTargetDeviceChangeAsynchronous).
  //

  [StructLayout(LayoutKind.Sequential)]
  public struct CLASS_MEDIA_CHANGE_CONTEXT {
    public uint MediaChangeCount;
    public uint NewState;
  }

  // begin_ntminitape

  [StructLayout(LayoutKind.Sequential)]
  public struct TAPE_STATISTICS {
    public uint Version;
    public uint Flags;
    public ulong RecoveredWriteOperations;
    public ulong UnrecoveredWriteOperations;
    public ulong RecoveredReadOperations;
    public ulong UnrecoveredReadOperations;
    public byte CompressionRatioRead;
    public byte CompressionRatioWrite;
  }

  public uint RECOVERED_WRITES_VALID = 0x00000001;
  public uint UNRECOVERED_WRITES_VALID = 0x00000002;
  public uint RECOVERED_READS_VALID = 0x00000004;
  public uint UNRECOVERED_READS_VALID = 0x00000008;
  public uint WRITE_COMPRESSION_INFO_VALID = 0x00000010;
  public uint READ_COMPRESSION_INFO_VALID = 0x00000020;

  [StructLayout(LayoutKind.Sequential)]
  public struct TAPE_GET_STATISTICS {
    public uint Operation;
  }

  public uint TAPE_RETURN_STATISTICS = 0;
  public uint TAPE_RETURN_ENV_INFO = 1;
  public uint TAPE_RESET_STATISTICS = 2;

  //
  // IOCTL_STORAGE_GET_MEDIA_TYPES_EX will return an array of DEVICE_MEDIA_INFO
  // structures, one per supported type, embedded in the GET_MEDIA_TYPES struct.
  //

  public enum STORAGE_MEDIA_TYPE {
    // Unknown,                // Format is unknown
    // F5_1Pt2_512,            // 5.25", 1.2MB,  512 bytes/sector
    // F3_1Pt44_512,           // 3.5",  1.44MB, 512 bytes/sector
    // F3_2Pt88_512,           // 3.5",  2.88MB, 512 bytes/sector
    // F3_20Pt8_512,           // 3.5",  20.8MB, 512 bytes/sector
    // F3_720_512,             // 3.5",  720KB,  512 bytes/sector
    // F5_360_512,             // 5.25", 360KB,  512 bytes/sector
    // F5_320_512,             // 5.25", 320KB,  512 bytes/sector
    // F5_320_1024,            // 5.25", 320KB,  1024 bytes/sector
    // F5_180_512,             // 5.25", 180KB,  512 bytes/sector
    // F5_160_512,             // 5.25", 160KB,  512 bytes/sector
    // RemovableMedia,         // Removable media other than floppy
    // FixedMedia,             // Fixed hard disk media
    // F3_120M_512,            // 3.5", 120M Floppy
    // F3_640_512,             // 3.5" ,  640KB,  512 bytes/sector
    // F5_640_512,             // 5.25",  640KB,  512 bytes/sector
    // F5_720_512,             // 5.25",  720KB,  512 bytes/sector
    // F3_1Pt2_512,            // 3.5" ,  1.2Mb,  512 bytes/sector
    // F3_1Pt23_1024,          // 3.5" ,  1.23Mb, 1024 bytes/sector
    // F5_1Pt23_1024,          // 5.25",  1.23MB, 1024 bytes/sector
    // F3_128Mb_512,           // 3.5" MO 128Mb   512 bytes/sector
    // F3_230Mb_512,           // 3.5" MO 230Mb   512 bytes/sector
    // F8_256_128,             // 8",     256KB,  128 bytes/sector
    // F3_200Mb_512,           // 3.5",   200M Floppy (HiFD)

    DDS_4mm = 0x20,            // Tape - DAT DDS1,2,... (all vendors)
    MiniQic,                   // Tape - miniQIC Tape
    Travan,                    // Tape - Travan TR-1,2,3,...
    QIC,                       // Tape - QIC
    MP_8mm,                    // Tape - 8mm Exabyte Metal Particle
    AME_8mm,                   // Tape - 8mm Exabyte Advanced Metal Evap
    AIT1_8mm,                  // Tape - 8mm Sony AIT
    DLT,                       // Tape - DLT Compact IIIxt, IV
    NCTP,                      // Tape - Philips NCTP
    IBM_3480,                  // Tape - IBM 3480
    IBM_3490E,                 // Tape - IBM 3490E
    IBM_Magstar_3590,          // Tape - IBM Magstar 3590
    IBM_Magstar_MP,            // Tape - IBM Magstar MP
    STK_DATA_D3,               // Tape - STK Data D3
    SONY_DTF,                  // Tape - Sony DTF
    DV_6mm,                    // Tape - 6mm Digital Video
    DMI,                       // Tape - Exabyte DMI and compatibles
    SONY_D2,                   // Tape - Sony D2S and D2L
    CLEANER_CARTRIDGE,         // Cleaner - All Drive types that support Drive Cleaners
    CD_ROM,                    // Opt_Disk - CD
    CD_R,                      // Opt_Disk - CD-Recordable (Write Once)
    CD_RW,                     // Opt_Disk - CD-Rewriteable
    DVD_ROM,                   // Opt_Disk - DVD-ROM
    DVD_R,                     // Opt_Disk - DVD-Recordable (Write Once)
    DVD_RW,                    // Opt_Disk - DVD-Rewriteable
    MO_3_RW,                   // Opt_Disk - 3.5" Rewriteable MO Disk
    MO_5_WO,                   // Opt_Disk - MO 5.25" Write Once
    MO_5_RW,                   // Opt_Disk - MO 5.25" Rewriteable (not LIMDOW)
    MO_5_LIMDOW,               // Opt_Disk - MO 5.25" Rewriteable (LIMDOW)
    PC_5_WO,                   // Opt_Disk - Phase Change 5.25" Write Once Optical
    PC_5_RW,                   // Opt_Disk - Phase Change 5.25" Rewriteable
    PD_5_RW,                   // Opt_Disk - PhaseChange Dual Rewriteable
    ABL_5_WO,                  // Opt_Disk - Ablative 5.25" Write Once Optical
    PINNACLE_APEX_5_RW,        // Opt_Disk - Pinnacle Apex 4.6GB Rewriteable Optical
    SONY_12_WO,                // Opt_Disk - Sony 12" Write Once
    PHILIPS_12_WO,             // Opt_Disk - Philips/LMS 12" Write Once
    HITACHI_12_WO,             // Opt_Disk - Hitachi 12" Write Once
    CYGNET_12_WO,              // Opt_Disk - Cygnet/ATG 12" Write Once
    KODAK_14_WO,               // Opt_Disk - Kodak 14" Write Once
    MO_NFR_525,                // Opt_Disk - Near Field Recording (Terastor)
    NIKON_12_RW,               // Opt_Disk - Nikon 12" Rewriteable
    IOMEGA_ZIP,                // Mag_Disk - Iomega Zip
    IOMEGA_JAZ,                // Mag_Disk - Iomega Jaz
    SYQUEST_EZ135,             // Mag_Disk - Syquest EZ135
    SYQUEST_EZFLYER,           // Mag_Disk - Syquest EzFlyer
    SYQUEST_SYJET,             // Mag_Disk - Syquest SyJet
    AVATAR_F2,                 // Mag_Disk - 2.5" Floppy
    MP2_8mm,                   // Tape - 8mm Hitachi
    DST_S,                     // Ampex DST Small Tapes
    DST_M,                     // Ampex DST Medium Tapes
    DST_L,                     // Ampex DST Large Tapes
    VXATape_1,                 // Ecrix 8mm Tape
    VXATape_2,                 // Ecrix 8mm Tape
#if NTDDI_VERSION_05010000
    STK_EAGLE,                 // STK Eagle
#elif NTDDI_WINXP_05010000
        STK_9840,                  // STK 9840
#endif
    LTO_Ultrium,               // IBM, HP, Seagate LTO Ultrium
    LTO_Accelis,               // IBM, HP, Seagate LTO Accelis
    DVD_RAM,                   // Opt_Disk - DVD-RAM
    AIT_8mm,                   // AIT2 or higher
    ADR_1,                     // OnStream ADR Mediatypes
    ADR_2,
    STK_9940,                  // STK 9940
    SAIT,                      // SAIT Tapes
    VXATape                    // VXA (Ecrix 8mm) Tape
  }

  public const uint MEDIA_ERASEABLE = 0x00000001;
  public const uint MEDIA_WRITE_ONCE = 0x00000002;
  public const uint MEDIA_READ_ONLY = 0x00000004;
  public const uint MEDIA_READ_WRITE = 0x00000008;

  public const uint MEDIA_WRITE_PROTECTED = 0x00000100;
  public const uint MEDIA_CURRENTLY_MOUNTED = 0x80000000;

  //
  // Define the different storage bus types
  // Bus types below 128 (0x80) are reserved for Microsoft use
  //

  public enum STORAGE_BUS_TYPE {
    BusTypeUnknown = 0x00,
    BusTypeScsi,
    BusTypeAtapi,
    BusTypeAta,
    BusType1394,
    BusTypeSsa,
    BusTypeFibre,
    BusTypeUsb,
    BusTypeRAID,
    BusTypeiSCSI,
    BusTypeSas,
    BusTypeSata,
    BusTypeSd,
    BusTypeMmc,
    BusTypeVirtual,
    BusTypeFileBackedVirtual,
    BusTypeSpaces,
    BusTypeNvme,
    BusTypeSCM,
    BusTypeUfs,
    BusTypeMax,
    BusTypeMaxReserved = 0x7F
  }


  //
  // Macro to identify which bus types
  // support shared storage
  //


  public static bool SupportsDeviceSharing(STORAGE_BUS_TYPE busType) => busType == STORAGE_BUS_TYPE.BusTypeScsi ||
        busType == STORAGE_BUS_TYPE.BusTypeFibre ||
        busType == STORAGE_BUS_TYPE.BusTypeiSCSI ||
        busType == STORAGE_BUS_TYPE.BusTypeSas ||
        busType == STORAGE_BUS_TYPE.BusTypeSpaces;

  [StructLayout(LayoutKind.Sequential)]
  public struct DEVICE_MEDIA_INFO {
    [StructLayout(LayoutKind.Sequential)]
    public struct DiskInfoStruct {
      public long Cylinders; // LARGE_INTEGER
      public STORAGE_MEDIA_TYPE MediaType;
      public uint TracksPerCylinder;
      public uint SectorsPerTrack;
      public uint BytesPerSector;
      public uint NumberMediaSides;
      public uint MediaCharacteristics; // Bitmask of MEDIA_XXX values.
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RemovableDiskInfoStruct {
      public long Cylinders; // LARGE_INTEGER
      public STORAGE_MEDIA_TYPE MediaType;
      public uint TracksPerCylinder;
      public uint SectorsPerTrack;
      public uint BytesPerSector;
      public uint NumberMediaSides;
      public uint MediaCharacteristics; // Bitmask of MEDIA_XXX values.
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TapeInfoStruct {
      public STORAGE_MEDIA_TYPE MediaType;
      public uint MediaCharacteristics; // Bitmask of MEDIA_XXX values.
      public uint CurrentBlockSize;
      public STORAGE_BUS_TYPE BusType;

      [StructLayout(LayoutKind.Sequential)]
      public struct ScsiInformationStruct {
        public byte MediumType;
        public byte DensityCode;
      }

      [StructLayout(LayoutKind.Explicit)]
      public struct BusSpecificDataUnion {
        [FieldOffset(0)]
        public ScsiInformationStruct ScsiInformation;
      }

      public BusSpecificDataUnion BusSpecificData;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct DeviceSpecificUnion {
      [FieldOffset(0)]
      public DiskInfoStruct DiskInfo;

      [FieldOffset(0)]
      public RemovableDiskInfoStruct RemovableDiskInfo;

      [FieldOffset(0)]
      public TapeInfoStruct TapeInfo;
    }

    public DeviceSpecificUnion DeviceSpecific;
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct GET_MEDIA_TYPES {
    public uint DeviceType; // FILE_DEVICE_XXX values
    public uint MediaInfoCount;
    public DEVICE_MEDIA_INFO MediaInfo;
  }

  //
  // IOCTL_STORAGE_PREDICT_FAILURE
  //
  // input - none
  //
  // output - STORAGE_PREDICT_FAILURE structure
  //          PredictFailure returns zero if no failure predicted and non zero
  //                         if a failure is predicted.
  //
  //          VendorSpecific returns 512 bytes of vendor specific information
  //                         if a failure is predicted
  //

  [StructLayout(LayoutKind.Sequential)]
  public struct STORAGE_PREDICT_FAILURE {
    public uint PredictFailure;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
    public byte[] VendorSpecific;
  }

  //
  // IOCTL_STORAGE_FAILURE_PREDICTION_CONFIG
  //
  // Input - STORAGE_FAILURE_PREDICTION_CONFIG structure.
  //         If the sender wants to enable or disable failure prediction then
  //         the sender should set the "Set" field to TRUE.
  // Output - STORAGE_FAILURE_PREDICTION_CONFIG structure.
  //          If successful, the "Enabled" field will indicate if failure
  //          prediction is currently enabled or not.
  //

  [StructLayout(LayoutKind.Sequential)]
  public struct STORAGE_FAILURE_PREDICTION_CONFIG {
    public uint Version;      // ULONG in C++ is equivalent to uint in C#
    public uint Size;         // ULONG in C++ is equivalent to uint in C#
    public byte Set;          // BOOLEAN in C++ is equivalent to byte in C#
    public byte Enabled;      // BOOLEAN in C++ is equivalent to byte in C#
    public ushort Reserved;   // USHORT in C++ is equivalent to ushort in C#
  }

  public const uint STORAGE_FAILURE_PREDICTION_CONFIG_V1 = 1;

  // end_ntminitape

  //
  // Property Query Structures
  //

  //
  // IOCTL_STORAGE_QUERY_PROPERTY
  //
  // Input Buffer:
  //      a STORAGE_PROPERTY_QUERY structure which describes what type of query
  //      is being done, what property is being queried for, and any additional
  //      parameters which a particular property query requires.
  //
  //  Output Buffer:
  //      Contains a buffer to place the results of the query into.  Since all
  //      property descriptors can be cast into a STORAGE_DESCRIPTOR_HEADER,
  //      the IOCTL can be called once with a small buffer then again using
  //      a buffer as large as the header reports is necessary.
  //


  //
  // Types of queries
  //

  public enum STORAGE_QUERY_TYPE {
    PropertyStandardQuery = 0,          // Retrieves the descriptor
    PropertyExistsQuery,                // Used to test whether the descriptor is supported
    PropertyMaskQuery,                  // Used to retrieve a mask of writeable fields in the descriptor
    PropertyQueryMaxDefined             // use to validate the value
  }

  //
  // define some initial property id's
  //

  public enum STORAGE_PROPERTY_ID {
    StorageDeviceProperty = 0,
    StorageAdapterProperty,
    StorageDeviceIdProperty,
    StorageDeviceUniqueIdProperty,              // See storduid.h for details
    StorageDeviceWriteCacheProperty,
    StorageMiniportProperty,
    StorageAccessAlignmentProperty,
    StorageDeviceSeekPenaltyProperty,
    StorageDeviceTrimProperty,
    StorageDeviceWriteAggregationProperty,
    StorageDeviceDeviceTelemetryProperty,
    StorageDeviceLBProvisioningProperty,
    StorageDevicePowerProperty,
    StorageDeviceCopyOffloadProperty,
    StorageDeviceResiliencyProperty,
    StorageDeviceMediumProductType,
    StorageAdapterRpmbProperty,
    StorageAdapterCryptoProperty,
    // end_winioctl
    StorageDeviceTieringProperty,
    StorageDeviceFaultDomainProperty,
    StorageDeviceClusportProperty,
    // begin_winioctl
    StorageDeviceIoCapabilityProperty = 48,
    StorageAdapterProtocolSpecificProperty,
    StorageDeviceProtocolSpecificProperty,
    StorageAdapterTemperatureProperty,
    StorageDeviceTemperatureProperty,
    StorageAdapterPhysicalTopologyProperty,
    StorageDevicePhysicalTopologyProperty,
    StorageDeviceAttributesProperty,
    StorageDeviceManagementStatus,
    StorageAdapterSerialNumberProperty,
    StorageDeviceLocationProperty,
    StorageDeviceNumaProperty,
    StorageDeviceZonedDeviceProperty,
    StorageDeviceUnsafeShutdownCount
  }

  //
  // Query structure - additional parameters for specific queries can follow
  // the header
  //

  [StructLayout(LayoutKind.Sequential)]
  public struct STORAGE_PROPERTY_QUERY {
    public STORAGE_PROPERTY_ID PropertyId;
    public STORAGE_QUERY_TYPE QueryType;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
    public byte[] AdditionalParameters;
  }


  #endregion

  #region Storage IOCTL Methods from ntddstor.h

  #endregion
}
