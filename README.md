# MaksIT.LTO.Backup

![Line Coverage](https://img.shields.io/badge/Line%20Coverage-39.3%25-yellow)
![Branch Coverage](https://img.shields.io/badge/Branch%20Coverage-28.7%25-yellow)
![Method Coverage](https://img.shields.io/badge/Method%20Coverage-49.7%25-yellowgreen)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/License-GPLv2-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux-0078D6)

Cross-platform toolkit for LTO tape backup and restore. Console, Avalonia UI, and Worker share one service layer.

See [LICENSE.md](LICENSE.md) (GPLv2). Changes: [CHANGELOG.md](CHANGELOG.md). Contributing: [CONTRIBUTING.md](CONTRIBUTING.md).

| Host | Project | Role |
|------|---------|------|
| Console | `MaksIT.LTO.Backup` | Interactive menu / `backup --name` CLI |
| Avalonia | `MaksIT.LTO.Backup.UI` | MVVM operations UI (Windows + Linux; CommunityToolkit.Mvvm) |
| Worker | `MaksIT.LTO.Backup.Service` | Scheduled library backups (Windows service / systemd) |

Device access modes:

- **Physical** — real drive/library on **Windows** (`\\.\Tape0`, `\\.\Changer0`; admin) or **Linux** (`/dev/nst0`, `/dev/sg*`; `tape`/`disk` group)
- **Emulated** — file-backed drive + library (no hardware; primary CI / no-hardware path)

> Current line is **`0.1.0-alpha.2`** (prerelease, not production-ready). Emulator coverage is the primary validation path; physical tape/changer and Linux SMB are not hardware-proven yet. Use at your own risk.

---

If you find this project useful, please consider supporting its development:

[<img src="https://cdn.buymeacoffee.com/buttons/v2/default-blue.png" alt="Buy Me A Coffee" style="height: 60px; width: 217px;">](https://www.buymeacoffee.com/maksitcom)

---

## Features

- Backup and restore with AES-GCM encrypted on-tape JSON descriptors and SHA-256 file verification
- SchemaVersion 2 layout: BOT index (`MLTI`), payload, filemark, descriptor preamble (`MLTD`), ciphertext, double filemark
- Overwrite or Append write modes (multiple sets per cartridge via BOT set table)
- Fail-fast restore on checksum mismatch (`FailFastOnChecksumMismatch`)
- MAM summary attributes written after backup (app name/version, host, text label)
- Local paths on all platforms; SMB source/destination on **Windows** (WNet) and **Linux** (SMB2 staging via SMBLibrary)
- LTO generation block sizes (LTO1–LTO9) and media-capacity checks
- Drive ops: load, eject, short erase, status, cartridge memory (MAM) / barcode
- Library ops: inventory, move medium (emulated; physical Windows `IOCTL_CHANGER_*` and Linux SG_IO)
- Shared library used by console, Avalonia UI, and Worker
- Live Avalonia monitor for drive status and library inventory
- Avalonia Service tab: install / uninstall / start / stop Worker (Windows + Linux)
- Emulator-backed unit / E2E tests without a physical tape (`{tapeId}.drive.json` + `{tapeId}.blocks.bin`)

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- **Windows:** physical tape/changer IOCTLs; Avalonia UI; SMB via WNet
- **Linux:** physical tape (`/dev/nst*`) and changer (`/dev/sg*`); Avalonia UI; SMB via managed SMB2 client; or `DeviceMode=Emulated`
- Optional: PowerShell 7+ for RepoUtils test/release engines under `utils/`

## Solution layout

```text
utils/                          # RepoUtils release / test engines
src/
  MaksIT.LTO.slnx
  MaksIT.LTO.Core/              # tape/library APIs + emulators
  MaksIT.LTO.Backup.Shared/     # shared config, orchestrators, host helpers
  MaksIT.LTO.Backup/            # console host
  MaksIT.LTO.Backup.UI/         # Avalonia host (Windows + Linux)
  MaksIT.LTO.Backup.Service/    # scheduled Worker (Windows / Linux)
  MaksIT.LTO.Tests/             # emulator tests
```

## Scheduled backups (library only)

Automatic scheduling runs **only** when `Topology` is `TapeLibrary`. `StandaloneDrive` remains manual (console / UI).

**Backup jobs** define *what* (source, `Tapes` pool, LTO gen). Drive selection is automatic.

**Aggregations** define *when* and *in what order* jobs run as one wave (e.g. Night / Afternoon):

- `JobNames` — ordered list of backup job names
- `Schedule` — UScheduler-compatible calendar with overdue catch-up (`RunMonth`, `RunWeekday`, `RunTime` UTC `HH:mm`, `MinIntervalMinutes`)
- `Disabled` / `LastRunUtc` — wave-level

The Worker holds an exclusive lease for the whole aggregation (no mid-wave collisions). If another wave is busy, the due wave retries on the next poll without consuming `LastRunUtc`. After each job: unload cartridge so the next job can claim any empty drive.

Worker lifecycle (also from Avalonia **Service** tab):

```text
MaksIT.LTO.Backup.Service --install
MaksIT.LTO.Backup.Service --start
MaksIT.LTO.Backup.Service --stop
MaksIT.LTO.Backup.Service --uninstall
```

Console:

```text
MaksIT.LTO.Backup backup --name "Normal test"
MaksIT.LTO.Backup backup --aggregation "Night"
MaksIT.LTO.Backup backup --aggregation "Night" --scheduled
```

Install/uninstall typically require administrator (Windows) or root (Linux systemd).

## Configuration

One shared model for the whole solution. Seed JSON ships next to each host; runtime writes go to AppData so Program Files does not need elevation.

- Model: [`src/MaksIT.LTO.Backup.Shared/Models/Configuration.cs`](src/MaksIT.LTO.Backup.Shared/Models/Configuration.cs)
- Seed: [`src/MaksIT.LTO.Backup.Shared/configuration.json`](src/MaksIT.LTO.Backup.Shared/configuration.json) (copied next to the exe, never written)
- Runtime: `%AppData%/MaksIT/LTO Backup/settings.json` (same folder name as WiX: `Program Files\MaksIT\LTO Backup`)
- Loader / workflows: `ConfigurationFileService`, `BackupOrchestrator`, `LibraryOrchestrator` in `MaksIT.LTO.Backup.Shared`

Edit the Shared seed for factory defaults, or use the UI **Settings** tab and click **Save configuration**.

`DeviceMode` selects the backend (`Physical` / `Emulated`).  
`Topology` selects how hardware is used:

- `StandaloneDrive` — **Drive** tab only (load/eject/status/MAM). **Backup** tab runs against that drive.
- `TapeLibrary` — **Library** tab (inventory, move into drive bays, MAM on loaded cartridge). **Backup** tab runs only when a cartridge is in a drive bay.

```json
{
  "Configuration": {
    "DeviceMode": "Emulated",
    "Topology": "StandaloneDrive",
    "TapePath": "\\\\.\\Tape0",
    "LibraryPath": "\\\\.\\Changer0",
    "WriteDelay": 100,
    "FailFastOnChecksumMismatch": true,
    "Emulator": {
      "DataDirectory": ".\\emulator-data",
      "Library": { "SlotCount": 24, "DriveCount": 2, "IePortCount": 1 }
    },
    "Backups": [
      {
        "Name": "Normal test",
        "Barcode": "LTO001",
        "LTOGen": "LTO5",
        "WriteMode": "Overwrite",
        "Source": { "LocalPath": { "Path": "F:\\LTO\\Backup" } },
        "Destination": { "LocalPath": { "Path": "F:\\LTO\\Restore" } }
      }
    ]
  }
}
```

### On-tape format (SchemaVersion 2)

```text
[BOT index MLTI] → [file blocks…] → FM → [MLTD preamble] → [length-prefixed AES-GCM descriptor] → FM FM
```

Append adds another payload+descriptor set at EOD and rewrites the BOT set table (max 16 sets).

### Descriptor secret

On first run a `secret.txt` is created for descriptor encryption (first line = current key). Keep a safe copy.

To rotate: put the new key on line 1 of `secret.txt` and move the old key to line 2+ or into `secret.history.txt`. Decrypt tries the current key then prior keys.

To avoid leaving the file on disk, set a machine environment variable (used as the preferred encrypt/decrypt key):

```powershell
[System.Environment]::SetEnvironmentVariable(
  "LTO_BACKUP_SECRET",
  "<secret.txt content here>",
  [System.EnvironmentVariableTarget]::Machine
)
```

## Console menu

1. Load tape  
2. Backup  
3. Restore  
4. Eject tape  
5. Get device status  
6. Tape erase (short)  
7. Read cartridge memory  
8. Move medium (library)  
9. Library inventory  
10. Exit  

## Build and run

```powershell
cd src
dotnet build MaksIT.LTO.slnx
dotnet run --project .\MaksIT.LTO.Backup
```

Avalonia UI (Windows or Linux):

```powershell
dotnet run --project .\MaksIT.LTO.Backup.UI
```

Prefer RepoUtils for a GitHub release (`utils\Invoke-ReleasePackage.bat`). Assets are siblings: portable `maksit-lto-backup-{version}.zip` (win-x64 console, Avalonia UI, Worker), Windows setup `maksit-lto-backup-{version}.exe` (Avalonia UI), and `maksit-lto-backup-{version}.flatpak` (Avalonia UI). The installer and Flatpak are not inside the zip. Manual publish:

```powershell
cd src
dotnet publish .\MaksIT.LTO.Backup -c Release -o ..\releases\console
dotnet publish .\MaksIT.LTO.Backup.UI -c Release -o ..\releases\ui
dotnet publish .\MaksIT.LTO.Backup.Service -c Release -o ..\releases\service
```

Physical mode: Windows needs elevation; Linux needs access to `/dev/nst*` / `/dev/sg*` (often `tape`/`disk` group). Discover changer nodes with `lsscsi -g`.

## Tests and coverage

Emulator-backed tests (no tape required):

```powershell
cd src
dotnet test .\MaksIT.LTO.Tests
```

With Cobertura coverage:

```powershell
cd src
dotnet test .\MaksIT.LTO.Tests --collect:"XPlat Code Coverage" --results-directory .\TestResults
```

Coverage shields at the top of this README use [shields.io](https://shields.io) and are rewritten by the **CoverageBadges** plugin when `utils\Invoke-TestEngine.bat` runs.

## RepoUtils release pipeline

Vendored under `utils/` (from MaksIT RepoUtils community):

| Action | Entry |
|--------|--------|
| Test | `utils\Invoke-TestEngine.bat` |
| Release | `utils\Invoke-ReleasePackage.bat` |

GitHub assets are siblings: portable `maksit-lto-backup-{version}.zip` (win-x64), Windows setup exe (Avalonia UI), and Flatpak (Avalonia UI). The installer and Flatpak are not inside the zip. On Windows the Flatpak bundle is built via WSL Debian.

Settings:

- `utils/engines/test/scriptSettings.json`
- `utils/engines/release/scriptSettings.json`

## License

GPLv2 — see [LICENSE.md](./LICENSE.md).

© Maksym Sadovnychyy (MAKS-IT)
