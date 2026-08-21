namespace MaksIT.LTO.Backup.Shared.Models;

public class BackupDescriptor {
  public const int CurrentSchemaVersion = 2;
  public const int MinReadableSchemaVersion = 1;
  public const string FormatMagic = "MLTO";

  public int SchemaVersion { get; set; } = CurrentSchemaVersion;
  public string FormatId { get; set; } = FormatMagic;
  public uint BlockSize { get; set; }

  public string? BackupName { get; set; }
  public DateTime CreatedUtc { get; set; }
  public string? LtoGen { get; set; }
  public string? MediaBarcode { get; set; }
  public string? SourceRoot { get; set; }
  public string? Host { get; set; }
  /// <summary>
  /// SHA-256 (hex) of the ordered file hash list; identifies catalog content.
  /// </summary>
  public string? ContentHash { get; set; }

  public List<FileDescriptor> Files { get; set; } = [];
}

public class FileDescriptor {
  public required string FilePath { get; set; }
  public long StartBlock { get; set; }
  public uint NumberOfBlocks { get; set; }
  public long FileSize { get; set; }
  public DateTime CreationTime { get; set; }
  public DateTime LastModifiedTime { get; set; }
  /// <summary>
  /// Hex-encoded digest; algorithm is <see cref="HashAlgorithm"/>.
  /// </summary>
  public required string FileHash { get; set; }
  public string HashAlgorithm { get; set; } = FileHashAlgorithms.Sha256.Name;
}
