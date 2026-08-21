using System.Net;
using Microsoft.Extensions.Logging;


namespace MaksIT.LTO.Core.Networking;

public static class RemotePathAccess {
  public static IRemotePathAccess Open(ILoggerFactory loggerFactory, string uncOrSmbPath, NetworkCredential credential, RemotePathAccessMode mode) {
    if (OperatingSystem.IsWindows()) {
      return WindowsSmbPathAccess.Open(loggerFactory, uncOrSmbPath, credential);
    }
    if (OperatingSystem.IsLinux()) {
      return LinuxSmbPathAccess.Open(loggerFactory, uncOrSmbPath, credential, mode);
    }
    throw new PlatformNotSupportedException("SMB remote paths are supported on Windows and Linux only.");
  }

  public static (string Host, string Share, string Relative) ParseSmbPath(string path) => SmbPathParser.Parse(path);
}
