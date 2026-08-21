using System.Net;
using System.Runtime.Versioning;
using MaksIT.Core.Networking.Windows;
using Microsoft.Extensions.Logging;


namespace MaksIT.LTO.Core.Networking;

[SupportedOSPlatform("windows")]
internal sealed class WindowsSmbPathAccess : IRemotePathAccess, IDisposable {
  private readonly NetworkConnection _connection;

  public string LocalPath { get; }

  private WindowsSmbPathAccess(NetworkConnection connection, string localPath) {
    _connection = connection;
    LocalPath = localPath;
  }

  public static WindowsSmbPathAccess Open(ILoggerFactory loggerFactory, string uncPath, NetworkCredential credential) {
    if (!NetworkConnection.TryCreate(loggerFactory.CreateLogger<NetworkConnection>(), uncPath, credential, out NetworkConnection networkConnection, out string errorMessage) || networkConnection == null) {
      throw new InvalidOperationException(errorMessage ?? "SMB connect failed.");
    }
    return new WindowsSmbPathAccess(networkConnection, uncPath);
  }

  public void Dispose() => _connection.Dispose();
}
