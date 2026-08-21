using System.Net;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using SMBLibrary;
using SMBLibrary.Client;


namespace MaksIT.LTO.Core.Networking;

[SupportedOSPlatform("linux")]
internal sealed class LinuxSmbPathAccess : IRemotePathAccess, IDisposable {
  private readonly ILogger _logger;

  private readonly RemotePathAccessMode _mode;

  private readonly string _host;

  private readonly string _share;

  private readonly string _shareRelativePath;

  private readonly NetworkCredential _credential;

  private readonly string _stagingRoot;

  private bool _disposed;

  public string LocalPath { get; }

  private LinuxSmbPathAccess(ILogger logger, RemotePathAccessMode mode, string host, string share, string shareRelativePath, NetworkCredential credential, string stagingRoot) {
    _logger = logger;
    _mode = mode;
    _host = host;
    _share = share;
    _shareRelativePath = shareRelativePath;
    _credential = credential;
    _stagingRoot = stagingRoot;
    LocalPath = Path.Combine(stagingRoot, "data");
  }

  public static LinuxSmbPathAccess Open(ILoggerFactory loggerFactory, string smbPath, NetworkCredential credential, RemotePathAccessMode mode) {
    ILogger logger = loggerFactory.CreateLogger("LinuxSmbPathAccess");
    (string Host, string Share, string Relative) tuple = SmbPathParser.Parse(smbPath);
    string item = tuple.Host;
    string item2 = tuple.Share;
    string item3 = tuple.Relative;
    string text = Path.Combine(Path.GetTempPath(), "maksit-lto-smb-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(text, "data"));
    LinuxSmbPathAccess linuxSmbPathAccess = new LinuxSmbPathAccess(logger, mode, item, item2, item3, credential, text);
    if (mode == RemotePathAccessMode.Read) {
      linuxSmbPathAccess.DownloadTree();
    }
    return linuxSmbPathAccess;
  }

  public void Dispose() {
    if (_disposed) {
      return;
    }
    _disposed = true;
    try {
      if (_mode == RemotePathAccessMode.Write) {
        UploadTree();
      }
    }
    finally {
      try {
        if (Directory.Exists(_stagingRoot)) {
          Directory.Delete(_stagingRoot, recursive: true);
        }
      }
      catch (Exception exception) {
        _logger.LogWarning(exception, "Failed to clean SMB staging directory {Path}.", _stagingRoot);
      }
    }
  }

  private void DownloadTree() => WithFileStore(delegate (ISMBFileStore fileStore) {
    DownloadDirectory(fileStore, _shareRelativePath, LocalPath);
  });

  private void UploadTree() => WithFileStore(delegate (ISMBFileStore fileStore) {
    UploadDirectory(fileStore, LocalPath, _shareRelativePath);
  });

  private void WithFileStore(Action<ISMBFileStore> action) {
    SMB2Client sMB2Client = new SMB2Client();
    if (!IPAddress.TryParse(_host, out IPAddress address)) {
      IPAddress[] hostAddresses = Dns.GetHostAddresses(_host);
      if (hostAddresses.Length == 0) {
        throw new InvalidOperationException("Cannot resolve SMB host '" + _host + "'.");
      }
      address = hostAddresses[0];
    }
    if (!sMB2Client.Connect(address, SMBTransportType.DirectTCPTransport)) {
      throw new InvalidOperationException("SMB connect to " + _host + " failed.");
    }
    try {
      string domainName = _credential.Domain ?? string.Empty;
      NTStatus status = sMB2Client.Login(domainName, _credential.UserName, _credential.Password);
      if (status != NTStatus.STATUS_SUCCESS) {
        throw new InvalidOperationException($"SMB login failed: {status}");
      }
      ISMBFileStore iSMBFileStore = sMB2Client.TreeConnect(_share, out status);
      if (status != NTStatus.STATUS_SUCCESS || iSMBFileStore == null) {
        throw new InvalidOperationException($"SMB TreeConnect('{_share}') failed: {status}");
      }
      try {
        action(iSMBFileStore);
      }
      finally {
        iSMBFileStore.Disconnect();
      }
      sMB2Client.Logoff();
    }
    finally {
      sMB2Client.Disconnect();
    }
  }

  private void DownloadDirectory(ISMBFileStore fileStore, string remoteDir, string localDir) {
    Directory.CreateDirectory(localDir);
    string text = NormalizeRemote(remoteDir);
    NTStatus nTStatus = fileStore.CreateFile(out var handle, out var _, text, AccessMask.SYNCHRONIZE | AccessMask.GENERIC_READ, SMBLibrary.FileAttributes.Directory, ShareAccess.Read | ShareAccess.Write, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);
    if (nTStatus != NTStatus.STATUS_SUCCESS) {
      throw new InvalidOperationException($"SMB open directory '{text}' failed: {nTStatus}");
    }
    try {
      nTStatus = fileStore.QueryDirectory(out var result, handle, "*", FileInformationClass.FileDirectoryInformation);
      if (nTStatus != NTStatus.STATUS_SUCCESS && nTStatus != NTStatus.STATUS_NO_MORE_FILES) {
        throw new InvalidOperationException($"SMB QueryDirectory failed: {nTStatus}");
      }
      foreach (QueryDirectoryFileInformation item in result ?? new List<QueryDirectoryFileInformation>()) {
        if (item is FileDirectoryInformation { FileName: var fileName } fileDirectoryInformation && ((!(fileName == ".") && !(fileName == "..")) || 1 == 0)) {
          string text2 = (string.IsNullOrEmpty(text) ? fileDirectoryInformation.FileName : (text + "\\" + fileDirectoryInformation.FileName));
          string text3 = Path.Combine(localDir, fileDirectoryInformation.FileName);
          if ((fileDirectoryInformation.FileAttributes & SMBLibrary.FileAttributes.Directory) != 0) {
            DownloadDirectory(fileStore, text2, text3);
          }
          else {
            DownloadFile(fileStore, text2, text3);
          }
        }
      }
    }
    finally {
      fileStore.CloseFile(handle);
    }
  }

  private void DownloadFile(ISMBFileStore fileStore, string remoteFile, string localFile) {
    NTStatus nTStatus = fileStore.CreateFile(out var handle, out var _, NormalizeRemote(remoteFile), AccessMask.SYNCHRONIZE | AccessMask.GENERIC_READ, SMBLibrary.FileAttributes.Normal, ShareAccess.Read, CreateDisposition.FILE_OPEN, CreateOptions.FILE_SYNCHRONOUS_IO_ALERT | CreateOptions.FILE_NON_DIRECTORY_FILE, null);
    if (nTStatus != NTStatus.STATUS_SUCCESS) {
      throw new InvalidOperationException($"SMB open file '{remoteFile}' failed: {nTStatus}");
    }
    try {
      using FileStream fileStream = File.Create(localFile);
      long num = 0L;
      while (true) {
        nTStatus = fileStore.ReadFile(out var data, handle, num, 65536);
        if (nTStatus != NTStatus.STATUS_SUCCESS && nTStatus != NTStatus.STATUS_END_OF_FILE) {
          throw new InvalidOperationException($"SMB read '{remoteFile}' failed: {nTStatus}");
        }
        if (nTStatus == NTStatus.STATUS_END_OF_FILE || data == null || data.Length == 0) {
          break;
        }
        fileStream.Write(data, 0, data.Length);
        num += data.Length;
      }
    }
    finally {
      fileStore.CloseFile(handle);
    }
  }

  private void UploadDirectory(ISMBFileStore fileStore, string localDir, string remoteDir) {
    EnsureRemoteDirectory(fileStore, remoteDir);
    string[] directories = Directory.GetDirectories(localDir);
    foreach (string text in directories) {
      string fileName = Path.GetFileName(text);
      string remoteDir2 = (string.IsNullOrEmpty(remoteDir) ? fileName : (remoteDir + "\\" + fileName));
      UploadDirectory(fileStore, text, remoteDir2);
    }
    string[] files = Directory.GetFiles(localDir);
    foreach (string text2 in files) {
      string fileName2 = Path.GetFileName(text2);
      string remoteFile = (string.IsNullOrEmpty(remoteDir) ? fileName2 : (remoteDir + "\\" + fileName2));
      UploadFile(fileStore, text2, remoteFile);
    }
  }

  private void EnsureRemoteDirectory(ISMBFileStore fileStore, string remoteDir) {
    string text = NormalizeRemote(remoteDir);
    if (!string.IsNullOrEmpty(text)) {
      object handle;
      FileStatus fileStatus;
      NTStatus nTStatus = fileStore.CreateFile(out handle, out fileStatus, text, AccessMask.SYNCHRONIZE | AccessMask.GENERIC_WRITE | AccessMask.GENERIC_READ, SMBLibrary.FileAttributes.Directory, ShareAccess.Read | ShareAccess.Write, CreateDisposition.FILE_OPEN_IF, CreateOptions.FILE_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);
      if (nTStatus != NTStatus.STATUS_SUCCESS) {
        throw new InvalidOperationException($"SMB create directory '{text}' failed: {nTStatus}");
      }
      fileStore.CloseFile(handle);
    }
  }

  private void UploadFile(ISMBFileStore fileStore, string localFile, string remoteFile) {
    NTStatus nTStatus = fileStore.CreateFile(out var handle, out var _, NormalizeRemote(remoteFile), AccessMask.SYNCHRONIZE | AccessMask.GENERIC_WRITE, SMBLibrary.FileAttributes.Normal, ShareAccess.None, CreateDisposition.FILE_SUPERSEDE, CreateOptions.FILE_SYNCHRONOUS_IO_ALERT | CreateOptions.FILE_NON_DIRECTORY_FILE, null);
    if (nTStatus != NTStatus.STATUS_SUCCESS) {
      throw new InvalidOperationException($"SMB create file '{remoteFile}' failed: {nTStatus}");
    }
    try {
      using FileStream fileStream = File.OpenRead(localFile);
      byte[] array = new byte[65536];
      long num = 0L;
      int num2;
      while ((num2 = fileStream.Read(array, 0, array.Length)) > 0) {
        byte[] array2 = array;
        if (num2 < array.Length) {
          array2 = new byte[num2];
          Buffer.BlockCopy(array, 0, array2, 0, num2);
        }
        nTStatus = fileStore.WriteFile(out var _, handle, num, array2);
        if (nTStatus != NTStatus.STATUS_SUCCESS) {
          throw new InvalidOperationException($"SMB write '{remoteFile}' failed: {nTStatus}");
        }
        num += num2;
      }
    }
    finally {
      fileStore.CloseFile(handle);
    }
  }

  private static string NormalizeRemote(string path) => path.Replace('/', '\\').Trim('\\');
}
