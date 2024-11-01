﻿using System.Net;
using System.Runtime.InteropServices;

namespace MaksIT.LTO.Core;

//public void RestoreFilesFromSmbShare(string smbPath, string username, string password, string domain, string restoreDirectory) {
//  var credentials = new NetworkCredential(username, password, domain);
//  using (new NetworkConnection(smbPath, credentials)) {
//    var files = Directory.GetFiles(smbPath, "*.*", SearchOption.AllDirectories);
//    foreach (var file in files) {
//      var relativePath = Path.GetRelativePath(smbPath, file);
//      var destinationPath = Path.Combine(restoreDirectory, relativePath);
//      Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
//      File.Copy(file, destinationPath, overwrite: true);
//      Console.WriteLine($"Restored file: {file} to {destinationPath}");
//    }
//  }
//}


public class NetworkConnection : IDisposable {
  private readonly string _networkName;

  public NetworkConnection(string networkName, NetworkCredential credentials) {
    _networkName = networkName;

    var netResource = new NetResource {
      Scope = ResourceScope.GlobalNetwork,
      ResourceType = ResourceType.Disk,
      DisplayType = ResourceDisplayType.Share,
      RemoteName = networkName
    };

    var result = WNetAddConnection2(netResource, credentials.Password, credentials.UserName, 0);

    if (result != 0) {
      throw new InvalidOperationException($"Error connecting to remote share: {result}");
    }
  }

  ~NetworkConnection() {
    Dispose(false);
  }

  public void Dispose() {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing) {
    WNetCancelConnection2(_networkName, 0, true);
  }

  [DllImport("mpr.dll")]
  private static extern int WNetAddConnection2(NetResource netResource, string password, string username, int flags);

  [DllImport("mpr.dll")]
  private static extern int WNetCancelConnection2(string name, int flags, bool force);

  [StructLayout(LayoutKind.Sequential)]
  public class NetResource {
    public ResourceScope Scope;
    public ResourceType ResourceType;
    public ResourceDisplayType DisplayType;
    public int Usage;
    public string LocalName;
    public string RemoteName;
    public string Comment;
    public string Provider;
  }

  public enum ResourceScope : int {
    Connected = 1,
    GlobalNetwork,
    Remembered,
    Recent,
    Context
  }

  public enum ResourceType : int {
    Any = 0,
    Disk = 1,
    Print = 2,
    Reserved = 8
  }

  public enum ResourceDisplayType : int {
    Generic = 0x0,
    Domain = 0x01,
    Server = 0x02,
    Share = 0x03,
    File = 0x04,
    Group = 0x05,
    Network = 0x06,
    Root = 0x07,
    Shareadmin = 0x08,
    Directory = 0x09,
    Tree = 0x0a,
    Ndscontainer = 0x0b
  }
}
