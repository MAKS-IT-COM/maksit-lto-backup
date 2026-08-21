using System.Text.Json;
using Microsoft.Extensions.Logging;


namespace MaksIT.LTO.Core.MassStorage;

public sealed class EmulatedTapeDrive : ITapeDrive, IDisposable {
  private sealed class TapeEntry {
    public bool IsFileMark { get; set; }

    public long Offset { get; set; }

    public int Length { get; set; }
  }

  private sealed class DriveState {
    public int Position { get; set; }

    public List<DriveEntry> Entries { get; set; } = new List<DriveEntry>();

    public Dictionary<ushort, string> Attributes { get; set; } = new Dictionary<ushort, string>();
  }

  private sealed class DriveEntry {
    public bool IsFileMark { get; set; }

    public long Offset { get; set; }

    public int Length { get; set; }

    public string? DataBase64 { get; set; }
  }

  private const int PersistEveryNWrites = 256;

  private readonly ILogger<EmulatedTapeDrive> _logger;

  private readonly string _statePath;

  private readonly string _blocksPath;

  private readonly object _gate = new object();

  private readonly List<TapeEntry> _entries = new List<TapeEntry>();

  private readonly Dictionary<ushort, byte[]> _attributes = new Dictionary<ushort, byte[]>();

  private FileStream? _blocksStream;

  private int _position;

  private bool _dirty;

  private int _writesSincePersist;

  private bool _disposed;

  public string DevicePath { get; }

  public EmulatedTapeDrive(ILogger<EmulatedTapeDrive> logger, string dataDirectory, string tapeId = "Tape0") {
    _logger = logger;
    Directory.CreateDirectory(dataDirectory);
    _statePath = Path.Combine(dataDirectory, tapeId + ".drive.json");
    _blocksPath = Path.Combine(dataDirectory, tapeId + ".blocks.bin");
    DevicePath = "emu://" + tapeId;
    LoadState();
  }

  public int WriteData(byte[] data) {
    ArgumentNullException.ThrowIfNull(data, "data");
    lock (_gate) {
      ThrowIfDisposed();
      FileStream fileStream = EnsureBlocksStream();
      long offset;
      if (_position < _entries.Count && !_entries[_position].IsFileMark && _entries[_position].Length == data.Length) {
        offset = _entries[_position].Offset;
        fileStream.Seek(offset, SeekOrigin.Begin);
      }
      else {
        offset = fileStream.Seek(0L, SeekOrigin.End);
      }
      fileStream.Write(data, 0, data.Length);
      fileStream.Flush(flushToDisk: false);
      TapeEntry tapeEntry = new TapeEntry {
        IsFileMark = false,
        Offset = offset,
        Length = data.Length
      };
      if (_position < _entries.Count) {
        _entries[_position] = tapeEntry;
      }
      else {
        _entries.Add(tapeEntry);
      }
      _position++;
      MarkDirty();
      return 0;
    }
  }

  public int ReadData(byte[] buffer, int offset, int length) {
    lock (_gate) {
      ThrowIfDisposed();
      if (_position >= _entries.Count || _entries[_position].IsFileMark) {
        return 0;
      }
      TapeEntry tapeEntry = _entries[_position];
      int num = Math.Min(length, tapeEntry.Length);
      if (num > 0) {
        FileStream fileStream = EnsureBlocksStream();
        fileStream.Seek(tapeEntry.Offset, SeekOrigin.Begin);
        int num2 = fileStream.Read(buffer, offset, num);
        if (num2 != num) {
          throw new IOException($"Emulated tape block read truncated at position {_position}.");
        }
      }
      _position++;
      return num;
    }
  }

  public byte[] ReadData(uint length) {
    byte[] array = new byte[length];
    ReadData(array, 0, array.Length);
    return array;
  }

  public void WaitForTapeReady() {
  }

  public int Erase(uint type) {
    lock (_gate) {
      ThrowIfDisposed();
      _entries.Clear();
      _position = 0;
      CloseBlocksStream();
      try {
        if (File.Exists(_blocksPath)) {
          File.Delete(_blocksPath);
        }
      }
      catch (IOException exception) {
        _logger.LogWarning(exception, "Failed to delete emulated blocks file {BlocksPath} during erase.", _blocksPath);
      }
      MarkDirty(forcePersist: true);
      return 0;
    }
  }

  public void Prepare(uint operation) {
  }

  public int WriteMarks(uint type, uint count) {
    lock (_gate) {
      ThrowIfDisposed();
      if (type != 1) {
        return 0;
      }
      for (int i = 0; i < count; i++) {
        TapeEntry tapeEntry = new TapeEntry {
          IsFileMark = true,
          Offset = 0L,
          Length = 0
        };
        if (_position < _entries.Count) {
          _entries[_position] = tapeEntry;
        }
        else {
          _entries.Add(tapeEntry);
        }
        _position++;
      }
      MarkDirty(forcePersist: true);
      return 0;
    }
  }

  public TapePosition GetPosition(uint type, uint partition = 0u, uint offsetLow = 0u, uint offsetHigh = 0u) {
    lock (_gate) {
      return new TapePosition {
        MethodType = type,
        Partition = partition,
        OffsetLow = (uint)_position,
        OffsetHigh = 0u,
        Error = 0
      };
    }
  }

  public void SetPosition(uint method, uint partition = 0u, long offset = 0L) {
    lock (_gate) {
      ThrowIfDisposed();
      switch (method) {
        case 0u:
          _position = 0;
          break;
        case 1u:
          _position = (int)Math.Max(0L, offset);
          break;
        case 4u:
          _position = _entries.Count;
          break;
        case 6u: {
            int num = (int)offset;
            while (_position < _entries.Count && num > 0) {
              if (_entries[_position].IsFileMark) {
                num--;
              }
              _position++;
            }
            break;
          }
      }
    }
  }

  public void SetMediaParams(uint blockSize) {
  }

  public int GetStatus() => 0;

  public IReadOnlyList<LTOCmAttribute> ReadCartridgeAttributes() {
    lock (_gate) {
      ThrowIfDisposed();
      List<LTOCmAttribute> list = new List<LTOCmAttribute>();
      foreach (KeyValuePair<ushort, byte[]> attribute in _attributes) {
        LTOCmAttribute lTOCmAttribute = LTOCmKnownAttributes.GetAttributeByAddress(attribute.Key) ?? new LTOCmAttribute {
          Address = attribute.Key,
          Name = $"Unknown (0x{attribute.Key:X4})",
          Format = LTOCmAttributeFormat.Unknown,
          Length = attribute.Value.Length,
          Writable = true
        };
        lTOCmAttribute.Value = attribute.Value.ToArray();
        list.Add(lTOCmAttribute);
      }
      list.Sort((LTOCmAttribute a, LTOCmAttribute b) => a.Address.CompareTo(b.Address));
      return list;
    }
  }

  public void WriteCartridgeAttribute(LTOCmAttribute attribute) {
    if (attribute.Value == null) {
      throw new ArgumentException("Attribute value is required.", "attribute");
    }
    lock (_gate) {
      ThrowIfDisposed();
      _attributes[attribute.Address] = attribute.Value.ToArray();
      MarkDirty(forcePersist: true);
    }
  }

  public void Dispose() {
    lock (_gate) {
      if (!_disposed) {
        Persist();
        CloseBlocksStream();
        _disposed = true;
      }
    }
  }

  private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

  private FileStream EnsureBlocksStream() {
    if (_blocksStream != null) {
      return _blocksStream;
    }
    _blocksStream = new FileStream(_blocksPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 1048576, FileOptions.SequentialScan);
    return _blocksStream;
  }

  private void CloseBlocksStream() {
    if (_blocksStream != null) {
      _blocksStream.Dispose();
      _blocksStream = null;
    }
  }

  private void MarkDirty(bool forcePersist = false) {
    _dirty = true;
    _writesSincePersist++;
    if (forcePersist || _writesSincePersist >= 256) {
      Persist();
    }
  }

  private void LoadState() {
    if (!File.Exists(_statePath)) {
      return;
    }
    try {
      string json = File.ReadAllText(_statePath);
      DriveState driveState = JsonSerializer.Deserialize<DriveState>(json);
      if (driveState == null) {
        return;
      }
      _entries.Clear();
      bool flag = false;
      FileStream fileStream = null;
      try {
        foreach (DriveEntry entry in driveState.Entries) {
          if (!string.IsNullOrEmpty(entry.DataBase64)) {
            if (fileStream == null) {
              fileStream = new FileStream(_blocksPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            }
            byte[] array = Convert.FromBase64String(entry.DataBase64);
            long position = fileStream.Position;
            fileStream.Write(array, 0, array.Length);
            _entries.Add(new TapeEntry {
              IsFileMark = entry.IsFileMark,
              Offset = position,
              Length = array.Length
            });
            flag = true;
          }
          else {
            _entries.Add(new TapeEntry {
              IsFileMark = entry.IsFileMark,
              Offset = entry.Offset,
              Length = entry.Length
            });
          }
        }
      }
      finally {
        fileStream?.Dispose();
      }
      _attributes.Clear();
      foreach (KeyValuePair<ushort, string> attribute in driveState.Attributes) {
        _attributes[attribute.Key] = Convert.FromBase64String(attribute.Value);
      }
      _position = Math.Max(0, driveState.Position);
      if (flag) {
        _dirty = true;
        Persist();
        _logger.LogInformation("Migrated legacy emulated tape state at {StatePath} to binary blocks file {BlocksPath}.", _statePath, _blocksPath);
      }
    }
    catch (Exception ex) when (((ex is JsonException || ex is IOException || ex is FormatException || ex is ArgumentException || ex is OutOfMemoryException) ? 1 : 0) != 0) {
      QuarantineCorruptState(ex);
      _entries.Clear();
      _attributes.Clear();
      _position = 0;
      _dirty = false;
      _writesSincePersist = 0;
      CloseBlocksStream();
      try {
        if (File.Exists(_blocksPath)) {
          File.Delete(_blocksPath);
        }
      }
      catch {
      }
    }
  }

  private void QuarantineCorruptState(Exception ex) {
    string text = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
    string text2 = _statePath + ".corrupt-" + text;
    string destFileName = _blocksPath + ".corrupt-" + text;
    try {
      if (File.Exists(_statePath)) {
        File.Move(_statePath, text2, overwrite: true);
      }
      if (File.Exists(_blocksPath)) {
        File.Move(_blocksPath, destFileName, overwrite: true);
      }
      _logger.LogWarning(ex, "Corrupt emulated tape state at {StatePath} was quarantined to {QuarantinePath}. Starting with an empty cartridge.", _statePath, text2);
    }
    catch (Exception exception) {
      _logger.LogWarning(exception, "Failed to quarantine corrupt emulated tape state at {StatePath}. Starting with an empty cartridge.", _statePath);
      try {
        File.Delete(_statePath);
      }
      catch {
      }
    }
  }

  private void Persist() {
    if (_dirty || !File.Exists(_statePath)) {
      _blocksStream?.Flush(flushToDisk: true);
      DriveState value = new DriveState {
        Position = _position,
        Entries = _entries.Select((TapeEntry entry) => new DriveEntry {
          IsFileMark = entry.IsFileMark,
          Offset = entry.Offset,
          Length = entry.Length
        }).ToList(),
        Attributes = _attributes.ToDictionary<KeyValuePair<ushort, byte[]>, ushort, string>((KeyValuePair<ushort, byte[]> pair) => pair.Key, (KeyValuePair<ushort, byte[]> pair) => Convert.ToBase64String(pair.Value))
      };
      string contents = JsonSerializer.Serialize(value);
      string text = _statePath + ".tmp";
      File.WriteAllText(text, contents);
      File.Move(text, _statePath, overwrite: true);
      _dirty = false;
      _writesSincePersist = 0;
    }
  }
}
