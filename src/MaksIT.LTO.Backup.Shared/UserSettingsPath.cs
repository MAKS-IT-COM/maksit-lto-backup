namespace MaksIT.LTO.Backup.Shared;


/// <summary>
/// User-writable settings under AppData. The product folder must match the WiX
/// <c>installFolderName</c> (or <c>Get-DesktopInstallFolderName</c> from
/// <c>appName</c> + <c>manufacturer</c>), e.g. <c>%AppData%/MaksIT/LTO Backup</c>.
/// Shipped JSON next to the exe is seed-only.
/// </summary>
public static class UserSettingsPath {
  public static string Get(string product, string fileName = "settings.json") =>
    Path.Combine(ProductDirectory(product), fileName);

  public static string LogsDirectory(string product) =>
    Path.Combine(ProductDirectory(product), "logs");

  public static string ProductDirectory(string product) =>
    Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "MaksIT",
      product);
}
