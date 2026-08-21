using System.Security.Cryptography;
using System.Text;


namespace MaksIT.LTO.Core.Utilities;

public static class FileHashUtility {
  public static bool TryCalculateSha256FromFileInChunks(string filePath, out string? hash, out string? error, int chunkSize = 65536) {
    hash = null;
    error = null;
    try {
      ArgumentOutOfRangeException.ThrowIfLessThan(chunkSize, 1, "chunkSize");
      using FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
      using IncrementalHash incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
      byte[] array = new byte[chunkSize];
      int count;
      while ((count = fileStream.Read(array, 0, array.Length)) > 0) {
        incrementalHash.AppendData(array, 0, count);
      }
      hash = Convert.ToHexString(incrementalHash.GetHashAndReset()).ToLowerInvariant();
      return true;
    }
    catch (Exception ex) {
      error = ex.Message;
      return false;
    }
  }

  public static bool VerifySha256FromFileInChunks(string filePath, string expectedHash, int chunkSize = 65536) {
    if (!TryCalculateSha256FromFileInChunks(filePath, out string hash, out string _, chunkSize)) {
      return false;
    }
    return string.Equals(hash, expectedHash, StringComparison.OrdinalIgnoreCase);
  }

  public static string ComputeContentHash(IEnumerable<string> orderedFileHashes) {
    using IncrementalHash incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    foreach (string orderedFileHash in orderedFileHashes) {
      incrementalHash.AppendData(Encoding.UTF8.GetBytes(orderedFileHash));
    }
    return Convert.ToHexString(incrementalHash.GetHashAndReset()).ToLowerInvariant();
  }
}
