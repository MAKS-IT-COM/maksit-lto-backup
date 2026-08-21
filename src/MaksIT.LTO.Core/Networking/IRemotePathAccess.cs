namespace MaksIT.LTO.Core.Networking;

public interface IRemotePathAccess : IDisposable {
  string LocalPath { get; }
}
