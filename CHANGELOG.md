# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0-alpha.3] - 2026-09-30

Still alpha versus **0.1.0-alpha.2**. Emulator and build validated; physical Windows/Linux tape, changer, and Linux SMB staging are not hardware-proven yet. Not a production-ready line.

### Added

- Avalonia UI crash dialog for unhandled dispatcher, AppDomain, and task exceptions. The report is copyable and written as `crash-*.txt` under the product logs folder (`%AppData%/MaksIT/LTO Backup/logs`, and the Linux application-data equivalent).
- Flatpak desktop file and AppStream metainfo in `data/` (`com.maks_it.ltobackup`). README documents user and system `flatpak install`.
- Release publish is self-contained for `win-x64` and `linux-x64`. The portable zip stays win-x64. `WindowsInstaller` and `FlatpakPack` run in the release engine. GitHub release requires the zip, setup exe, and Flatpak; cleanup keeps those three.

### Changed

- Avalonia **12.1.2** (was 12.1.0).
- Linux UI stays on X11/XWayland (`UsePlatformDetect` only). Avalonia 12.1.2 native Wayland still hangs on GNOME `xdg_toplevel.configure(0, 0)` when launched from the GNOME app icon.
- Flatpak app id is lowercase `com.maks_it.ltobackup` (replaces `com.maks_it.LtoBackup`). Uninstall the old id before installing a new bundle. Finish args include `--device=all` (tape) and `--filesystem=home`. AppStream `<release version>` is stamped from the same `Directory.Build.props` version as the zip and setup exe, so `flatpak info` matches the bundle file name.
- Windows installer build also links `WixToolset.Util.wixext`.
- Vendored RepoUtils platform plugins: `CollectCoverage`, `ContainerEngineProbe`, `DiscoverPackageArtifacts`.

### Removed

- Unused npm pack plugins (`NpmBuild`, `NpmJestTest`, `NpmPack`, `NpmPackageSupport`, `NpmPublish`, `NpmReleaseVersion`). `NpmPublish` stays disabled in release settings.

## [0.1.0-alpha.2] - 2026-09-06

Still alpha versus **0.1.0-alpha.1**. Emulator and build validated; physical Windows/Linux tape, changer, and Linux SMB staging are not hardware-proven yet. Not a production-ready line.

### Added

- Desktop packaging as sibling GitHub assets: Windows setup `maksit-lto-backup-{version}.exe` (Avalonia UI) and `maksit-lto-backup-{version}.flatpak` (Avalonia UI), alongside the portable `maksit-lto-backup-{version}.zip` (win-x64 console, Avalonia UI, and Worker). The installer and Flatpak are not packed inside the zip. On Windows the Flatpak bundle is built via WSL Debian.
- Faceted LTO cartridge app icon (not the brand M).

### Changed

- Avalonia UI is **MVVM** (`CommunityToolkit.Mvvm`): operations, settings, monitor, and service controls live in `MainViewModel`; `MainWindow` is a thin view.
- Configuration (console, UI, Worker) is written to `%AppData%/MaksIT/LTO Backup/settings.json` (WiX `installFolderName`). The shipped `configuration.json` next to the exe is seed-only.
- Dark theme uses the MAKS.IT origami blues (`#33A5CF` / `#006199`).
- Synced RepoUtils utils: ContainerRegistry JSON catalog (PascalCase Harbor / InCluster keys).

## [0.1.0-alpha.1] - 2026-08-21

Alpha preview versus last published **0.0.2** (HEAD was console-only Windows on **net8.0**). Emulator and build validated; physical Windows/Linux tape, changer, and Linux SMB staging are not hardware-proven yet. Not a production-ready line.

### Added

- **Avalonia UI (`MaksIT.LTO.Backup.UI`):** cross-platform operations host (Monitor / Drive / Library / Backup / Settings / Service) on Windows and Linux — SimpleTheme **Dark** styles aligned with Cluster Console.
- **Shared library (`MaksIT.LTO.Backup.Shared`):** configuration model, `ConfigurationFileService`, `BackupOrchestrator`, `LibraryOrchestrator`, scheduling helpers used by console, UI, and Worker.
- **Worker (`MaksIT.LTO.Backup.Service`):** scheduled **aggregations** (ordered job waves) with UScheduler-shaped calendars and overdue catch-up; exclusive lease; automatic drive claim/unload; console `backup --name` / `backup --aggregation` [`--scheduled`].
- **Service registration:** Windows SCM (`sc.exe` `binPath=` / `start=` spacing) and systemd (`Type=notify` + `AddSystemd()`, quoted `ExecStart`/`WorkingDirectory`, execute bit, unit removal on uninstall). Worker content root is the application base directory so SCM/`systemd` cwd does not hide `configuration.json`.
- **Device abstraction:** `ITapeDrive` / `ITapeLibrary` with file-backed emulators (`EmulatedTapeDrive`, `EmulatedTapeLibrary`), `WindowsTapeDrive` adapter, and `DeviceMode` / `Topology` (`StandaloneDrive` | `TapeLibrary`).
- **Linux physical backends:** `LinuxTapeDrive` (`/dev/nst*` + MTIOCTOP/SG_IO MAM) and `LinuxTapeLibrary` (`/dev/sg*` + SCSI medium changer).
- **Windows physical changer:** `WindowsTapeLibrary` via `IOCTL_CHANGER_*` (inventory, initialize, move) — 0.0.2 had drive IOCTLs only.
- **Shared SCSI/MAM helpers:** `MamAttributeCodec`, `ScsiMediumChanger`; neutral `TapePosition`.
- **Cross-platform SMB:** `RemotePathAccess` — WNet on Windows (replaces in-repo `NetworkConnection`), SMBLibrary staging on Linux (new).
- **SchemaVersion 2 on-tape layout:** BOT index (`MLTI`), payload, filemark, descriptor preamble (`MLTD`), length-prefixed AES-GCM descriptor, double filemark; Overwrite or Append; SHA-256 file hashes (0.0.2 used a simpler CRC32 descriptor layout).
- **Emulator tests (`MaksIT.LTO.Tests`):** unit / E2E / scheduling coverage without physical tape; **xunit.v3** **4.0** + **Microsoft Testing Platform** (`global.json` `test.runner`, `TestingPlatformDotnetTestSupport`) and **coverlet.MTP**.
- **RepoUtils** under `utils/` (community test / release engines). README coverage badges are rewritten by the **CoverageBadges** plugin (`badgeFormat: shields`) when `utils\Invoke-TestEngine.bat` runs.
- Maintainer-local agent wiring (, ) and this changelog.

### Changed

- Target **.NET 10** (`net10.0`); solution file `src/MaksIT.LTO.slnx` (was `MaksIT.LTO.Backup.sln` + **net8.0**).
- Console host thinned to Shared orchestrators; configuration lives in Shared (`configuration.json`). Console, UI, and Worker load it from the application base directory.
- Crypto / hashing / logging / Windows SMB move onto **MaksIT.Core** (+ local `FileHashUtility`); in-repo `AESGCMUtility`, `ChecksumUtility`/`Crc32`, `FileLogger*`, and `NetworkConnection` removed.
- Publish path: RepoUtils release packaging instead of `dotnet_build_script.*`. Host version comes from root `Directory.Build.props` only.

### Removed

- Legacy `MaksIT.LTO.Backup.sln`, console-local `Configuration` / `Entities`, and `src/dotnet_build_script.*`.
- WPF-classic Avalonia light theme (`WpfClassic.axaml`); unused `WindowsVersions` constants; console `secret.txt` copy that was never in-repo; Community-only `utils/templates/` and obsolete `Update-RepoUtils` docs.

## [0.0.2] - 2024-11-03

### Added

- Console LTO backup/restore toolkit (local and SMB paths).
- AES-GCM encrypted descriptors, checksum verification, cartridge EEPROM (MAM) read/write.
- Dependency injection, console and file logging.
- Build scripts and contributor docs (`README.md`, `CONTRIBUTING.md`, `LICENSE.md`).

