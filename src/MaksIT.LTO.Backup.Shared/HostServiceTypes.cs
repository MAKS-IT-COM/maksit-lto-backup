namespace MaksIT.LTO.Backup.Shared;

public record HostServiceOperationResult(bool Success, string Message);

public enum HostServiceStatus {
  NotInstalled,
  Stopped,
  StartPending,
  StopPending,
  Running,
  ContinuePending,
  PausePending,
  Paused,
  Unknown
}
