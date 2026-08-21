using MaksIT.LTO.Backup.Shared;


namespace MaksIT.LTO.Tests;

public class HostServiceRegistrationTests {
  [Fact]
  public void FormatWindowsCreateArguments_RequiresSpaceAfterEquals() {
    var arguments = HostServiceRegistration.FormatWindowsCreateArguments(
      "MaksIT.LTO.Backup",
      Path.Combine(Path.GetTempPath(), "MaksIT.LTO.Backup.Service.exe"));

    Assert.Contains("binPath= ", arguments, StringComparison.Ordinal);
    Assert.Contains("start= auto", arguments, StringComparison.Ordinal);
    Assert.DoesNotContain("start=auto", arguments, StringComparison.Ordinal);
    Assert.Contains("create \"MaksIT.LTO.Backup\"", arguments, StringComparison.Ordinal);
  }

  [Fact]
  public void FormatSystemdUnit_UsesNotifyLifetimeAndQuotedPaths() {
    var unit = HostServiceRegistration.FormatSystemdUnit(
      "MaksIT.LTO.Backup",
      Path.Combine(Path.GetTempPath(), "MaksIT.LTO.Backup.Service"),
      "Schedules LTO library backups.");

    Assert.Contains("Type=notify", unit, StringComparison.Ordinal);
    Assert.Contains("ExecStart=\"", unit, StringComparison.Ordinal);
    Assert.Contains("WorkingDirectory=\"", unit, StringComparison.Ordinal);
    Assert.Contains("WantedBy=multi-user.target", unit, StringComparison.Ordinal);
    Assert.Contains("SyslogIdentifier=MaksIT.LTO.Backup", unit, StringComparison.Ordinal);
    Assert.Contains("KillSignal=SIGTERM", unit, StringComparison.Ordinal);
  }

  [Fact]
  public void IsValidServiceName_AcceptsDefaultAndRejectsUnsafe() {
    Assert.True(HostServiceRegistration.IsValidServiceName("MaksIT.LTO.Backup"));
    Assert.False(HostServiceRegistration.IsValidServiceName("MaksIT LTO"));
    Assert.False(HostServiceRegistration.IsValidServiceName("../evil"));
    Assert.False(HostServiceRegistration.IsValidServiceName(""));
  }

  [Fact]
  public void GetSystemdUnitPath_IsUnderSystemdSystem() =>
    Assert.Equal(
      "/etc/systemd/system/MaksIT.LTO.Backup.service",
      HostServiceRegistration.GetSystemdUnitPath("MaksIT.LTO.Backup"));
}
