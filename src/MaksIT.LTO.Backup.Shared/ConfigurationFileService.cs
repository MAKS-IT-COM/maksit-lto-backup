using System.Text.Json;
using System.Text.Json.Nodes;
using MaksIT.LTO.Backup.Shared.Models;


namespace MaksIT.LTO.Backup.Shared;

public sealed class ConfigurationFileService {
  public const string ProductFolder = "LTO Backup";
  public const string SeedFileName = "configuration.json";

  private static readonly JsonSerializerOptions SerializerOptions = new() {
    WriteIndented = true,
    PropertyNamingPolicy = null
  };

  private readonly string? _seedPath;
  private Configuration _current;

  public string FilePath { get; }

  public Configuration Current => _current;

  public ConfigurationFileService(string? configurationPath = null) {
    if (!string.IsNullOrWhiteSpace(configurationPath)) {
      FilePath = configurationPath;
      _seedPath = null;
    }
    else {
      FilePath = UserSettingsPath.Get(ProductFolder);
      _seedPath = Path.Combine(AppContext.BaseDirectory, SeedFileName);
    }

    _current = LoadFromDisk();
  }

  public Configuration Reload() {
    _current = LoadFromDisk();
    return _current;
  }

  public void Save(Configuration configuration) {
    ArgumentNullException.ThrowIfNull(configuration);

    var dir = Path.GetDirectoryName(FilePath);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);

    var root = ReadRoot(File.Exists(FilePath) ? FilePath : ResolveReadPath()) ?? [];
    root["Configuration"] = JsonSerializer.SerializeToNode(configuration, SerializerOptions);
    File.WriteAllText(FilePath, root.ToJsonString(SerializerOptions));
    _current = configuration;
  }

  private Configuration LoadFromDisk() {
    var path = ResolveReadPath();
    if (path is null)
      return new Configuration();

    using var document = JsonDocument.Parse(File.ReadAllText(path));
    if (!document.RootElement.TryGetProperty("Configuration", out var value))
      return new Configuration();

    return JsonSerializer.Deserialize<Configuration>(value.GetRawText(), SerializerOptions) ?? new Configuration();
  }

  private static JsonObject? ReadRoot(string? path) {
    if (path is null || !File.Exists(path))
      return null;

    return JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
  }

  private string? ResolveReadPath() {
    if (File.Exists(FilePath))
      return FilePath;
    if (_seedPath is not null && File.Exists(_seedPath))
      return _seedPath;
    return null;
  }
}
