namespace MaksIT.LTO.Core.Networking;


internal static class SmbPathParser {
  public static (string Host, string Share, string Relative) Parse(string path) {
    var trimmed = path.Trim();
    if (trimmed.StartsWith("smb://", StringComparison.OrdinalIgnoreCase)) {
      var without = trimmed["smb://".Length..];
      var parts = without.Split('/', StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length < 2)
        throw new ArgumentException($"Invalid smb URL '{path}'.");

      return (parts[0], parts[1], string.Join('\\', parts.Skip(2)));
    }

    var unc = trimmed.TrimStart('\\');
    var uncParts = unc.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
    if (uncParts.Length < 2)
      throw new ArgumentException($"Invalid UNC/SMB path '{path}'.");

    return (uncParts[0], uncParts[1], string.Join('\\', uncParts.Skip(2)));
  }
}
