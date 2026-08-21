using System.Net;
using System.Text;
using System.Text.Json;
using MaksIT.Core.Extensions;
using MaksIT.Core.Security;
using MaksIT.LTO.Backup.Shared.Models;
using MaksIT.LTO.Core;
using MaksIT.LTO.Core.MassStorage;
using MaksIT.LTO.Core.Networking;
using MaksIT.LTO.Core.Utilities;
using Microsoft.Extensions.Logging;


namespace MaksIT.LTO.Backup.Shared;

public sealed class BackupOrchestrator(ILogger<BackupOrchestrator> logger, ILoggerFactory loggerFactory, ConfigurationFileService configurationFileService) {
  private sealed record PreparedDescriptor(BackupDescriptor Descriptor, byte[] EncryptedCiphertext, byte[] FramedCiphertext, byte[] PreambleBlock, uint DescriptorBlockCount);

  private readonly ConfigurationFileService _configurationFileService = configurationFileService;

  private readonly string _appPath = AppDomain.CurrentDomain.BaseDirectory;

  private readonly string _secretFileName = "secret.txt";

  private readonly string _secretHistoryFileName = "secret.history.txt";

  private Configuration Configuration => _configurationFileService.Current;

  public IReadOnlyList<BackupItem> Backups => Configuration.Backups;

  public int? SelectedLibraryDriveSlot { get; set; }

  private string SecretPath => Path.Combine(_appPath, _secretFileName);

  private string SecretHistoryPath => Path.Combine(_appPath, _secretHistoryFileName);

  private bool IsEmulated => Configuration.IsEmulated;

  private bool IsTapeLibrary => Configuration.IsTapeLibrary;

  private string GetSecretForEncrypt() {
    List<string> list = LoadSecretsInPriorityOrder();
    if (list.Count > 0) {
      return list[0];
    }
    string text = AESGCMUtility.GenerateKeyBase64();
    File.WriteAllText(SecretPath, text + Environment.NewLine);
    return text;
  }

  private List<string> GetSecretsForDecrypt() {
    List<string> list = LoadSecretsInPriorityOrder();
    if (list.Count == 0) {
      throw new InvalidOperationException("No descriptor secret found. Create secret.txt or set LTO_BACKUP_SECRET.");
    }
    return list;
  }

  private List<string> LoadSecretsInPriorityOrder() {
    List<string> secrets = new List<string>();
    Add(Environment.GetEnvironmentVariable("LTO_BACKUP_SECRET"));
    Add(Environment.GetEnvironmentVariable("LTO_BACKUP_SECRET", EnvironmentVariableTarget.Machine));
    if (File.Exists(SecretPath)) {
      string[] array = File.ReadAllLines(SecretPath);
      foreach (string value in array) {
        Add(value);
      }
    }
    if (File.Exists(SecretHistoryPath)) {
      string[] array2 = File.ReadAllLines(SecretHistoryPath);
      foreach (string value2 in array2) {
        Add(value2);
      }
    }
    return secrets;
    void Add(string? text) {
      if (!string.IsNullOrWhiteSpace(text)) {
        string text2 = text.Trim();
        if (!secrets.Contains<string>(text2, StringComparer.Ordinal)) {
          secrets.Add(text2);
        }
      }
    }
  }

  private EmulatedTapeLibrary CreateEmulatedLibrary() => new EmulatedTapeLibrary(Path.GetFullPath(Configuration.Emulator.DataDirectory), Configuration.Emulator.Library.SlotCount, Configuration.Emulator.Library.DriveCount);

  private static string GetStandaloneTapeId(string tapePath) {
    string fileName = Path.GetFileName(tapePath.Replace('/', '\\').TrimEnd('\\'));
    return string.IsNullOrWhiteSpace(fileName) ? "Tape0" : fileName;
  }

  private static void EnsureSupportedPhysicalOs(string feature) {
    if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux()) {
      return;
    }
    throw new PlatformNotSupportedException(feature + " requires Windows or Linux. Use DeviceMode=Emulated on other platforms.");
  }

  private ITapeDrive CreateDrive() {
    if (!IsEmulated) {
      EnsureSupportedPhysicalOs("Physical tape drive access");
      if (OperatingSystem.IsLinux()) {
        return new LinuxTapeDrive(loggerFactory.CreateLogger<LinuxTapeDrive>(), Configuration.TapePath);
      }
      return new WindowsTapeDrive(loggerFactory.CreateLogger<TapeDeviceHandler>(), Configuration.TapePath);
    }
    string fullPath = Path.GetFullPath(Configuration.Emulator.DataDirectory);
    if (IsTapeLibrary) {
      EmulatedTapeLibrary emulatedTapeLibrary = CreateEmulatedLibrary();
      LibraryElementStatus libraryElementStatus = ResolveLibraryDrive(emulatedTapeLibrary);
      EmulatedTapeDrive emulatedTapeDrive = new EmulatedTapeDrive(loggerFactory.CreateLogger<EmulatedTapeDrive>(), fullPath, libraryElementStatus.CartridgeId);
      emulatedTapeLibrary.SynchronizeBarcodeWithDrive(emulatedTapeDrive);
      return emulatedTapeDrive;
    }
    return new EmulatedTapeDrive(loggerFactory.CreateLogger<EmulatedTapeDrive>(), fullPath, GetStandaloneTapeId(Configuration.TapePath));
  }

  public IReadOnlyList<LoadedCartridgeSnapshot> GetOccupiedLibraryDrives() {
    if (!IsTapeLibrary || !IsEmulated) {
      return Array.Empty<LoadedCartridgeSnapshot>();
    }
    List<LoadedCartridgeSnapshot> list = new List<LoadedCartridgeSnapshot>();
    list.AddRange(CreateEmulatedLibrary().GetOccupiedDrives().Select(ToLoadedSnapshot));
    return list;
  }

  private LibraryElementStatus ResolveLibraryDrive(EmulatedTapeLibrary library) {
    IReadOnlyList<LibraryElementStatus> occupiedDrives = library.GetOccupiedDrives();
    if (occupiedDrives.Count == 0) {
      throw new InvalidOperationException("Tape library topology requires media in a drive bay. Move a cartridge into a drive first.");
    }
    int? selectedLibraryDriveSlot = SelectedLibraryDriveSlot;
    if (selectedLibraryDriveSlot.HasValue) {
      int selectedSlot = selectedLibraryDriveSlot.GetValueOrDefault();
      if (true) {
        return occupiedDrives.FirstOrDefault((LibraryElementStatus drive) => drive.Slot == selectedSlot) ?? throw new InvalidOperationException($"Selected drive bay {selectedSlot} has no cartridge. Choose an occupied drive.");
      }
    }
    if (occupiedDrives.Count == 1) {
      SelectedLibraryDriveSlot = occupiedDrives[0].Slot;
      return occupiedDrives[0];
    }
    throw new InvalidOperationException("Multiple library drives have media. Select which drive bay to use before continuing.");
  }

  private static LoadedCartridgeSnapshot ToLoadedSnapshot(LibraryElementStatus occupied) => new LoadedCartridgeSnapshot {
    IsLoaded = true,
    Topology = DeviceTopologies.TapeLibrary.Name,
    DriveSlot = occupied.Slot,
    CartridgeId = occupied.CartridgeId,
    Barcode = occupied.Barcode,
    DevicePath = "emu://" + occupied.CartridgeId,
    Summary = $"Drive bay {occupied.Slot} · {occupied.CartridgeId} · barcode {occupied.Barcode ?? "(none)"}"
  };

  public LoadedCartridgeSnapshot GetLoadedCartridge() {
    if (IsTapeLibrary) {
      int? selectedLibraryDriveSlot;
      if (IsEmulated) {
        EmulatedTapeLibrary emulatedTapeLibrary = CreateEmulatedLibrary();
        IReadOnlyList<LibraryElementStatus> occupiedDrives = emulatedTapeLibrary.GetOccupiedDrives();
        if (occupiedDrives.Count == 0) {
          return new LoadedCartridgeSnapshot {
            IsLoaded = false,
            Topology = Configuration.Topology,
            Hint = "Move a cartridge into a drive bay on the Library tab before backup or MAM operations."
          };
        }
        selectedLibraryDriveSlot = SelectedLibraryDriveSlot;
        if (selectedLibraryDriveSlot.HasValue) {
          int selectedSlot = selectedLibraryDriveSlot.GetValueOrDefault();
          if (true) {
            LibraryElementStatus libraryElementStatus = occupiedDrives.FirstOrDefault((LibraryElementStatus drive) => drive.Slot == selectedSlot);
            if (libraryElementStatus == null) {
              return new LoadedCartridgeSnapshot {
                IsLoaded = false,
                Topology = Configuration.Topology,
                DriveSlot = selectedSlot,
                Hint = $"Selected drive bay {selectedSlot} is empty. Choose an occupied drive."
              };
            }
            return ToLoadedSnapshot(libraryElementStatus);
          }
        }
        if (occupiedDrives.Count == 1) {
          SelectedLibraryDriveSlot = occupiedDrives[0].Slot;
          return ToLoadedSnapshot(occupiedDrives[0]);
        }
        return new LoadedCartridgeSnapshot {
          IsLoaded = false,
          Topology = Configuration.Topology,
          Hint = "Multiple drives have media. Select the drive bay to use for backup/MAM."
        };
      }
      selectedLibraryDriveSlot = SelectedLibraryDriveSlot;
      string summary;
      if (selectedLibraryDriveSlot.HasValue) {
        int valueOrDefault = selectedLibraryDriveSlot.GetValueOrDefault();
        summary = $"Physical library drive bay {valueOrDefault} · path {Configuration.TapePath}";
      }
      else {
        summary = "Physical library drive path " + Configuration.TapePath;
      }
      return new LoadedCartridgeSnapshot {
        IsLoaded = true,
        Topology = Configuration.Topology,
        DriveSlot = SelectedLibraryDriveSlot,
        DevicePath = Configuration.TapePath,
        Summary = summary,
        Hint = "Ensure the changer has loaded a cartridge into the target drive."
      };
    }
    return new LoadedCartridgeSnapshot {
      IsLoaded = true,
      Topology = Configuration.Topology,
      DevicePath = Configuration.TapePath,
      CartridgeId = GetStandaloneTapeId(Configuration.TapePath),
      Summary = "Standalone drive " + Configuration.TapePath
    };
  }

  public void EnsureCartridgeLoaded() {
    LoadedCartridgeSnapshot loadedCartridge = GetLoadedCartridge();
    if (!loadedCartridge.IsLoaded) {
      throw new InvalidOperationException(loadedCartridge.Hint ?? "No cartridge is loaded.");
    }
  }

  public void EnsureExpectedBarcode(string? expectedBarcode) {
    if (string.IsNullOrWhiteSpace(expectedBarcode)) {
      return;
    }
    string text = expectedBarcode.Trim();
    LoadedCartridgeSnapshot loadedCartridge = GetLoadedCartridge();
    if (!loadedCartridge.IsLoaded) {
      throw new InvalidOperationException(loadedCartridge.Hint ?? "No cartridge is loaded.");
    }
    string text2 = loadedCartridge.Barcode;
    if (string.IsNullOrWhiteSpace(text2) && IsEmulated) {
      using ITapeDrive drive = CreateDrive();
      text2 = EmulatedTapeLibrary.ReadMamBarcode(drive);
    }
    if (string.IsNullOrWhiteSpace(text2)) {
      throw new InvalidOperationException("Expected barcode '" + text + "' but the loaded cartridge has no barcode.");
    }
    if (string.Equals(text2.Trim(), text, StringComparison.OrdinalIgnoreCase)) {
      return;
    }
    throw new InvalidOperationException($"Loaded barcode '{text2.Trim()}' does not match expected '{text}'.");
  }

  private void PathAccessWrapper(WorkingFolder workingFolder, Action<string> action, RemotePathAccessMode mode = RemotePathAccessMode.Read) {
    if (workingFolder.LocalPath != null) {
      action(workingFolder.LocalPath.Path);
      return;
    }
    if (workingFolder.RemotePath?.Protocol != "SMB" || workingFolder.RemotePath.PasswordCredentials == null) {
      throw new InvalidOperationException("Only SMB remote paths with credentials are supported.");
    }
    EnsureSupportedPhysicalOs("SMB remote paths");
    NetworkCredential credential = new NetworkCredential(workingFolder.RemotePath.PasswordCredentials.Username, workingFolder.RemotePath.PasswordCredentials.Password);
    using IRemotePathAccess remotePathAccess = RemotePathAccess.Open(loggerFactory, workingFolder.RemotePath.Path, credential, mode);
    action(remotePathAccess.LocalPath);
  }

  private BackupDescriptor CreateDescriptor(BackupItem backup, uint blockSize, uint payloadBaseBlock) {
    BackupDescriptor descriptor = new BackupDescriptor {
      SchemaVersion = 2,
      FormatId = "MLTO",
      BlockSize = blockSize,
      BackupName = backup.Name,
      CreatedUtc = DateTime.UtcNow,
      LtoGen = backup.LTOGen,
      MediaBarcode = (string.IsNullOrWhiteSpace(backup.Barcode) ? null : backup.Barcode),
      Host = Environment.MachineName
    };
    PathAccessWrapper(backup.Source, delegate (string directoryPath) {
      descriptor.SourceRoot = directoryPath;
      string[] array = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories).OrderBy<string, string>((string filePath) => Path.GetRelativePath(directoryPath, filePath), StringComparer.OrdinalIgnoreCase).ToArray();
      uint num = payloadBaseBlock;
      string[] array2 = array;
      foreach (string text in array2) {
        FileInfo fileInfo = new FileInfo(text);
        string relativePath = Path.GetRelativePath(directoryPath, text);
        uint num3 = (uint)((fileInfo.Length + blockSize - 1) / blockSize);
        if (!FileHashUtility.TryCalculateSha256FromFileInChunks(text, out string hash, out string error, (int)blockSize)) {
          throw new InvalidOperationException("SHA-256 failed for " + text + ": " + error);
        }
        descriptor.Files.Add(new FileDescriptor {
          StartBlock = num,
          NumberOfBlocks = num3,
          FilePath = relativePath,
          FileSize = fileInfo.Length,
          CreationTime = fileInfo.CreationTimeUtc,
          LastModifiedTime = fileInfo.LastWriteTimeUtc,
          FileHash = hash,
          HashAlgorithm = FileHashAlgorithms.Sha256.Name
        });
        num += num3;
      }
    });
    descriptor.ContentHash = FileHashUtility.ComputeContentHash(descriptor.Files.Select((FileDescriptor file) => file.FileHash));
    return descriptor;
  }

  private static PreparedDescriptor PrepareEncryptedDescriptor(BackupDescriptor descriptor, string secret) {
    string s = JsonSerializer.Serialize(descriptor);
    if (!AESGCMUtility.TryEncryptData(Encoding.UTF8.GetBytes(s), secret, out byte[] result, out string errorMessage)) {
      throw new InvalidOperationException(errorMessage);
    }
    byte[] array = result;
    byte[] array2 = PaddingUtility.AddPadding(array, (int)descriptor.BlockSize);
    uint descriptorBlockCount = (uint)(array2.Length / descriptor.BlockSize);
    byte[] preambleBlock = DescriptorPreamble.Create(descriptorBlockCount, array.Length, (int)descriptor.BlockSize);
    return new PreparedDescriptor(descriptor, array, array2, preambleBlock, descriptorBlockCount);
  }

  private static ulong CalculateRequiredBlocksForOverwrite(PreparedDescriptor prepared) {
    ulong num = (ulong)prepared.Descriptor.Files.Sum((FileDescriptor file) => file.NumberOfBlocks);
    return 1 + num + 1 + 1 + prepared.DescriptorBlockCount + 2;
  }

  private static void EnsureSourceUnchanged(string sourcePath, FileDescriptor file, int chunkSize) {
    string text = Path.Combine(sourcePath, file.FilePath);
    FileInfo fileInfo = new FileInfo(text);
    if (!fileInfo.Exists) {
      throw new InvalidOperationException("Source file missing since catalog build: " + file.FilePath);
    }
    if (fileInfo.Length != file.FileSize) {
      throw new InvalidOperationException($"Source file size changed since catalog build: {file.FilePath} (was {file.FileSize}, now {fileInfo.Length}).");
    }
    if (fileInfo.LastWriteTimeUtc != file.LastModifiedTime) {
      throw new InvalidOperationException("Source file modified since catalog build: " + file.FilePath + ".");
    }
    if (!FileHashUtility.TryCalculateSha256FromFileInChunks(text, out string hash, out string error, chunkSize)) {
      throw new InvalidOperationException("SHA-256 re-check failed for " + file.FilePath + ": " + error);
    }
    if (!string.Equals(hash, file.FileHash, StringComparison.OrdinalIgnoreCase)) {
      throw new InvalidOperationException("Source file content changed since catalog build: " + file.FilePath + ".");
    }
  }

  private static void WriteBlocks(ITapeDrive drive, byte[] data, int blockSize, int writeDelayMs) {
    int num = data.Length / blockSize;
    for (int i = 0; i < num; i++) {
      byte[] array = new byte[blockSize];
      Array.Copy(data, i * blockSize, array, 0, blockSize);
      drive.WriteData(array);
      if (writeDelayMs > 0) {
        Thread.Sleep(writeDelayMs);
      }
    }
  }

  private string GetProgressSidecarPath(BackupItem backup) {
    string text = string.Join("_", backup.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
    if (string.IsNullOrWhiteSpace(text)) {
      text = "backup";
    }
    return Path.Combine(_appPath, "backup-progress-" + text + ".json");
  }

  private void WriteProgressSidecar(BackupItem backup, string filePath, long startBlock, uint blocksWritten) {
    var value = new {
      Name = backup.Name,
      UpdatedUtc = DateTime.UtcNow,
      FilePath = filePath,
      StartBlock = startBlock,
      BlocksWritten = blocksWritten
    };
    File.WriteAllText(GetProgressSidecarPath(backup), JsonSerializer.Serialize(value));
  }

  private void ClearProgressSidecar(BackupItem backup) {
    string progressSidecarPath = GetProgressSidecarPath(backup);
    if (File.Exists(progressSidecarPath)) {
      File.Delete(progressSidecarPath);
    }
  }

  private static BotIndex.Index? TryReadBotIndex(ITapeDrive drive, uint blockSize) {
    drive.SetPosition(0u, 0u, 0L);
    byte[] array = new byte[blockSize];
    int num = drive.ReadData(array, 0, array.Length);
    if (num < 16) {
      return null;
    }
    BotIndex.Index index;
    string error;
    return BotIndex.TryParse(array, out index, out error) ? index : null;
  }

  private static void ValidateDescriptor(BackupDescriptor descriptor, int? expectedCiphertextLength = null, int? actualCiphertextLength = null) {
    if (!string.Equals(descriptor.FormatId, "MLTO", StringComparison.Ordinal)) {
      throw new InvalidOperationException("Unsupported descriptor FormatId '" + descriptor.FormatId + "'.");
    }
    if (descriptor.SchemaVersion < 1 || descriptor.SchemaVersion > 2) {
      throw new InvalidOperationException($"Unsupported descriptor SchemaVersion {descriptor.SchemaVersion}.");
    }
    if (expectedCiphertextLength.HasValue) {
      int valueOrDefault = expectedCiphertextLength.GetValueOrDefault();
      if (actualCiphertextLength.HasValue) {
        int valueOrDefault2 = actualCiphertextLength.GetValueOrDefault();
        if (valueOrDefault != valueOrDefault2) {
          throw new InvalidOperationException($"Descriptor ciphertext length mismatch (preamble {valueOrDefault}, framed {valueOrDefault2}).");
        }
      }
    }
    if (!string.IsNullOrWhiteSpace(descriptor.ContentHash)) {
      string a = FileHashUtility.ComputeContentHash(descriptor.Files.Select((FileDescriptor file) => file.FileHash));
      if (!string.Equals(a, descriptor.ContentHash, StringComparison.OrdinalIgnoreCase)) {
        throw new InvalidOperationException("Descriptor ContentHash verification failed.");
      }
    }
  }

  private BackupDescriptor DecryptDescriptor(byte[] framedData, int blockSize, int preambleCiphertextLength) {
    byte[] array = PaddingUtility.RemovePadding(framedData, blockSize);
    if (array.Length != preambleCiphertextLength) {
      throw new InvalidOperationException($"Descriptor ciphertext length mismatch (preamble {preambleCiphertextLength}, framed {array.Length}).");
    }
    byte[] decryptedData = null;
    string errorMessage = null;
    foreach (string item in GetSecretsForDecrypt()) {
      if (AESGCMUtility.TryDecryptData(array, item, out decryptedData, out errorMessage)) {
        break;
      }
    }
    if (decryptedData == null) {
      throw new InvalidOperationException(errorMessage ?? "Failed to decrypt descriptor with available secrets.");
    }
    BackupDescriptor backupDescriptor = Encoding.UTF8.GetString(decryptedData).ToObject<BackupDescriptor>() ?? throw new InvalidOperationException("Descriptor JSON deserialized to null.");
    ValidateDescriptor(backupDescriptor, preambleCiphertextLength, array.Length);
    return backupDescriptor;
  }

  public void Backup(BackupItem backup, string? expectedBarcode = null) {
    EnsureCartridgeLoaded();
    EnsureExpectedBarcode(expectedBarcode);
    bool append = BackupWriteModes.Matches(backup.WriteMode, BackupWriteModes.Append);
    uint blockSize = LTOBlockSizes.GetBlockSize(backup.LTOGen);
    string secret = GetSecretForEncrypt();
    try {
      PathAccessWrapper(backup.Source, delegate (string sourcePath) {
        using ITapeDrive tapeDrive = CreateDrive();
        tapeDrive.Prepare(0u);
        tapeDrive.SetMediaParams(blockSize);
        tapeDrive.Prepare(2u);
        tapeDrive.Prepare(3u);
        tapeDrive.WaitForTapeReady();
        List<BotIndex.SetEntry> list = new List<BotIndex.SetEntry>();
        uint num;
        if (append) {
          BotIndex.Index index = TryReadBotIndex(tapeDrive, blockSize) ?? throw new InvalidOperationException("Append requires an existing SchemaVersion 2 BOT index. Run an Overwrite backup first.");
          if (index.Sets.Count >= 16) {
            throw new InvalidOperationException($"Cartridge already has {16} backup sets.");
          }
          list.AddRange(index.Sets);
          tapeDrive.SetPosition(4u, 0u, 0L);
          TapePosition position = tapeDrive.GetPosition(0u);
          num = position.OffsetLow ?? throw new InvalidOperationException("Unable to read EOD position for append.");
        }
        else {
          num = 1u;
          tapeDrive.SetPosition(0u, 0u, 0L);
        }
        BackupDescriptor descriptor = CreateDescriptor(backup, blockSize, num);
        PreparedDescriptor preparedDescriptor = PrepareEncryptedDescriptor(descriptor, secret);
        ulong num2 = (ulong)preparedDescriptor.Descriptor.Files.Sum((FileDescriptor file) => file.NumberOfBlocks);
        ulong num3 = num2 + 1 + 1 + preparedDescriptor.DescriptorBlockCount + 2;
        ulong num4 = (append ? (num + num3) : CalculateRequiredBlocksForOverwrite(preparedDescriptor));
        if (num4 > LTOBlockSizes.GetMaxBlocks(backup.LTOGen)) {
          throw new InvalidOperationException("Backup set does not fit on target LTO generation.");
        }
        if (!append) {
          tapeDrive.SetPosition(0u, 0u, 0L);
          tapeDrive.WriteData(BotIndex.Create(new BotIndex.Index {
            SchemaVersion = 2,
            BlockSize = blockSize,
            Sets = new List<BotIndex.SetEntry>()
          }, (int)blockSize));
        }
        else {
          tapeDrive.SetPosition(1u, 0u, num);
        }
        foreach (FileDescriptor file in preparedDescriptor.Descriptor.Files) {
          EnsureSourceUnchanged(sourcePath, file, (int)blockSize);
          WriteProgressSidecar(backup, file.FilePath, file.StartBlock, 0u);
          string path = Path.Combine(sourcePath, file.FilePath);
          using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
          byte[] array = new byte[blockSize];
          for (int num5 = 0; num5 < file.NumberOfBlocks; num5++) {
            int num6 = fileStream.Read(array, 0, array.Length);
            if (num6 < array.Length) {
              Array.Clear(array, num6, array.Length - num6);
            }
            tapeDrive.WriteData(array);
            if (Configuration.WriteDelay > 0) {
              Thread.Sleep(Configuration.WriteDelay);
            }
          }
          WriteProgressSidecar(backup, file.FilePath, file.StartBlock, file.NumberOfBlocks);
        }
        tapeDrive.WriteMarks(1u, 1u);
        TapePosition position2 = tapeDrive.GetPosition(0u);
        uint descriptorAbsoluteBlock = position2.OffsetLow ?? throw new InvalidOperationException("Unable to read descriptor absolute position.");
        tapeDrive.WriteData(preparedDescriptor.PreambleBlock);
        WriteBlocks(tapeDrive, preparedDescriptor.FramedCiphertext, (int)blockSize, Configuration.WriteDelay);
        tapeDrive.WriteMarks(1u, 2u);
        list.Add(new BotIndex.SetEntry {
          PayloadBaseBlock = num,
          DescriptorAbsoluteBlock = descriptorAbsoluteBlock,
          DescriptorBlockCount = preparedDescriptor.DescriptorBlockCount,
          ContentHash = BotIndex.ContentHashFromHex(preparedDescriptor.Descriptor.ContentHash),
          BackupName = backup.Name
        });
        byte[] data = BotIndex.Create(new BotIndex.Index {
          SchemaVersion = 2,
          BlockSize = blockSize,
          Sets = list
        }, (int)blockSize);
        tapeDrive.SetPosition(1u, 0u, 0L);
        tapeDrive.WriteData(data);
        MamBackupSummary.Write(tapeDrive, backup.Name, preparedDescriptor.Descriptor.CreatedUtc, backup.LTOGen, preparedDescriptor.Descriptor.Files.Count, blockSize, preparedDescriptor.Descriptor.ContentHash);
        tapeDrive.Prepare(4u);
        tapeDrive.SetPosition(0u, 0u, 0L);
      });
      ClearProgressSidecar(backup);
      if (logger.IsEnabled(LogLevel.Information)) {
        logger.LogInformation("Backup completed for {BackupName}. Append={Append}.", backup.Name, append);
      }
      if (!string.IsNullOrWhiteSpace(expectedBarcode)) {
        WriteBarcode(expectedBarcode);
      }
      else if (!string.IsNullOrWhiteSpace(backup.Barcode)) {
        WriteBarcode(backup.Barcode);
      }
    }
    catch {
      throw;
    }
  }

  public BackupDescriptor? FindDescriptor(uint blockSize, string? preferredBackupName = null) {
    using ITapeDrive tapeDrive = CreateDrive();
    tapeDrive.Prepare(0u);
    tapeDrive.SetMediaParams(blockSize);
    BotIndex.Index index = TryReadBotIndex(tapeDrive, blockSize);
    if (index != null) {
      List<BotIndex.SetEntry> sets = index.Sets;
      if (sets != null && sets.Count > 0) {
        BotIndex.SetEntry setEntry = SelectSet(index, preferredBackupName);
        return ReadDescriptorFromAbsolute(tapeDrive, blockSize, setEntry.DescriptorAbsoluteBlock);
      }
    }
    tapeDrive.SetPosition(0u, 0u, 0L);
    tapeDrive.SetPosition(6u, 0u, 1L);
    return TryReadDescriptorFromCurrentPosition(tapeDrive, blockSize);
  }

  private static BotIndex.SetEntry SelectSet(BotIndex.Index index, string? preferredBackupName) {
    if (!string.IsNullOrWhiteSpace(preferredBackupName)) {
      BotIndex.SetEntry setEntry = index.Sets.LastOrDefault((BotIndex.SetEntry set) => string.Equals(set.BackupName, preferredBackupName, StringComparison.OrdinalIgnoreCase));
      if (setEntry != null) {
        return setEntry;
      }
    }
    List<BotIndex.SetEntry> sets = index.Sets;
    return sets[sets.Count - 1];
  }

  private BackupDescriptor ReadDescriptorFromAbsolute(ITapeDrive drive, uint blockSize, uint descriptorAbsoluteBlock) {
    drive.SetPosition(1u, 0u, descriptorAbsoluteBlock);
    return ReadDescriptorFromCurrentPosition(drive, blockSize);
  }

  private BackupDescriptor? TryReadDescriptorFromCurrentPosition(ITapeDrive drive, uint blockSize) {
    try {
      return ReadDescriptorFromCurrentPosition(drive, blockSize);
    }
    catch (InvalidOperationException) {
      return null;
    }
  }

  private BackupDescriptor ReadDescriptorFromCurrentPosition(ITapeDrive drive, uint blockSize) {
    byte[] array = new byte[blockSize];
    int num = drive.ReadData(array, 0, array.Length);
    if (num < 20) {
      throw new InvalidOperationException("Descriptor preamble not found.");
    }
    if (!DescriptorPreamble.TryParse(array, out uint descriptorBlockCount, out int ciphertextLength, out string error)) {
      throw new InvalidOperationException(error ?? "Failed to parse descriptor preamble.");
    }
    byte[] array2 = new byte[descriptorBlockCount * blockSize];
    byte[] array3 = new byte[blockSize];
    for (int i = 0; i < descriptorBlockCount; i++) {
      drive.ReadData(array3, 0, array3.Length);
      Array.Copy(array3, 0L, array2, i * blockSize, array3.Length);
    }
    return DecryptDescriptor(array2, (int)blockSize, ciphertextLength);
  }

  public void Restore(BackupItem backup) {
    EnsureCartridgeLoaded();
    uint blockSize = LTOBlockSizes.GetBlockSize(backup.LTOGen);
    BackupDescriptor descriptor = FindDescriptor(blockSize, backup.Name) ?? throw new InvalidOperationException("Descriptor not found on tape.");
    PathAccessWrapper(backup.Destination, delegate (string restorePath) {
      using ITapeDrive tapeDrive = CreateDrive();
      tapeDrive.Prepare(0u);
      tapeDrive.SetMediaParams(descriptor.BlockSize);
      tapeDrive.SetPosition(0u, 0u, 0L);
      foreach (FileDescriptor file in descriptor.Files) {
        tapeDrive.SetPosition(1u, 0u, file.StartBlock);
        string text = Path.Combine(restorePath, file.FilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(text));
        using (FileStream fileStream = new FileStream(text, FileMode.Create, FileAccess.Write)) {
          byte[] array = new byte[descriptor.BlockSize];
          for (int i = 0; i < file.NumberOfBlocks; i++) {
            int num = tapeDrive.ReadData(array, 0, array.Length);
            int num2 = (int)(file.FileSize % descriptor.BlockSize);
            int count = ((i == file.NumberOfBlocks - 1 && num2 != 0) ? num2 : num);
            fileStream.Write(array, 0, count);
          }
        }
        if (!VerifyRestoredFileHash(text, file, (int)descriptor.BlockSize)) {
          if (Configuration.FailFastOnChecksumMismatch) {
            throw new InvalidOperationException("Checksum mismatch for restored file: " + file.FilePath);
          }
          logger.LogWarning("Checksum mismatch for restored file: {FilePath}", file.FilePath);
        }
      }
      tapeDrive.SetPosition(0u, 0u, 0L);
    }, RemotePathAccessMode.Write);
    if (logger.IsEnabled(LogLevel.Information)) {
      logger.LogInformation("Restore completed for {BackupName}.", backup.Name);
    }
  }

  private static bool VerifyRestoredFileHash(string filePath, FileDescriptor file, int chunkSize) {
    if (FileHashAlgorithms.Matches(file.HashAlgorithm, FileHashAlgorithms.Crc32)) {
      return ChecksumUtility.VerifyCRC32ChecksumFromFileInChunks(filePath, file.FileHash, chunkSize);
    }
    return FileHashUtility.VerifySha256FromFileInChunks(filePath, file.FileHash, chunkSize);
  }

  public void LoadTape() {
    using ITapeDrive tapeDrive = CreateDrive();
    tapeDrive.Prepare(0u);
  }

  public void EjectTape() {
    using ITapeDrive tapeDrive = CreateDrive();
    tapeDrive.Prepare(1u);
  }

  public void TapeErase(string ltoGen) {
    EnsureCartridgeLoaded();
    using ITapeDrive tapeDrive = CreateDrive();
    tapeDrive.Prepare(0u);
    tapeDrive.SetMediaParams(LTOBlockSizes.GetBlockSize(ltoGen));
    tapeDrive.SetPosition(0u, 0u, 0L);
    tapeDrive.Prepare(2u);
    tapeDrive.Prepare(3u);
    tapeDrive.Erase(0u);
    tapeDrive.SetPosition(0u, 0u, 0L);
  }

  public int GetDeviceStatus() {
    using ITapeDrive tapeDrive = CreateDrive();
    return tapeDrive.GetStatus();
  }

  public DriveStatusSnapshot GetDriveStatusSnapshot() {
    LoadedCartridgeSnapshot loadedCartridge = GetLoadedCartridge();
    if (!loadedCartridge.IsLoaded) {
      return new DriveStatusSnapshot {
        DeviceMode = Configuration.DeviceMode,
        Topology = Configuration.Topology,
        TapePath = Configuration.TapePath,
        DevicePath = Configuration.TapePath,
        StatusCode = -1,
        State = "No media loaded",
        Error = loadedCartridge.Hint
      };
    }
    try {
      using ITapeDrive tapeDrive = CreateDrive();
      int status = tapeDrive.GetStatus();
      uint? absoluteBlock = null;
      try {
        TapePosition position = tapeDrive.GetPosition(0u);
        int? error = position.Error;
        if ((!error.HasValue || error.GetValueOrDefault() == 0) ? true : false) {
          absoluteBlock = position.OffsetLow;
        }
      }
      catch {
      }
      return new DriveStatusSnapshot {
        DeviceMode = Configuration.DeviceMode,
        Topology = Configuration.Topology,
        TapePath = Configuration.TapePath,
        DevicePath = tapeDrive.DevicePath,
        StatusCode = status,
        State = ((status == 0) ? "Ready" : $"Not ready (Win32 {status})"),
        AbsoluteBlock = absoluteBlock
      };
    }
    catch (Exception ex) {
      return new DriveStatusSnapshot {
        DeviceMode = Configuration.DeviceMode,
        Topology = Configuration.Topology,
        TapePath = Configuration.TapePath,
        DevicePath = Configuration.TapePath,
        StatusCode = -1,
        State = "Unavailable",
        Error = ex.Message
      };
    }
  }

  public IReadOnlyList<LTOCmAttribute> ReadCartridgeMemory() {
    EnsureCartridgeLoaded();
    using ITapeDrive tapeDrive = CreateDrive();
    return tapeDrive.ReadCartridgeAttributes();
  }

  public void WriteBarcode(string barcode) {
    ArgumentException.ThrowIfNullOrWhiteSpace(barcode, "barcode");
    EnsureCartridgeLoaded();
    using ITapeDrive drive = CreateDrive();
    EmulatedTapeLibrary.WriteMamBarcode(drive, barcode);
    if (IsEmulated && IsTapeLibrary) {
      EmulatedTapeLibrary emulatedTapeLibrary = CreateEmulatedLibrary();
      LibraryElementStatus libraryElementStatus = ResolveLibraryDrive(emulatedTapeLibrary);
      emulatedTapeLibrary.SetBarcode(libraryElementStatus.Slot, barcode);
    }
  }
}
