# Contributing to MaksIT.LTO.Backup

Thank you for contributing. C# style: repo-root [`.editorconfig`](.editorconfig).

## Development setup

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Git
- PowerShell 7+ (RepoUtils scripts under `utils/`)

### Build

```powershell
cd src
dotnet build MaksIT.LTO.slnx
```

### Configuration

Edit the shared seed only: [`src/MaksIT.LTO.Backup.Shared/configuration.json`](src/MaksIT.LTO.Backup.Shared/configuration.json). Console, Avalonia UI, and Worker link it into their outputs. Runtime saves go to `%AppData%/MaksIT/LTO Backup/settings.json`. See [README.md](README.md).

### Tests

Prefer emulator-backed tests (no physical LTO required). Tests run under **Microsoft Testing Platform** (`src/global.json` `test.runner`) with **xunit.v3** and **coverlet.MTP**:

```powershell
utils\Invoke-TestEngine.bat
```

Or:

```powershell
cd src
dotnet test .\MaksIT.LTO.Tests
```

Coverage shields at the top of `README.md` are maintained by the **CoverageBadges** plugin (`utils/engines/test/scriptSettings.json`, `badgeFormat: shields`). Run `utils\Invoke-TestEngine.bat` after meaningful coverage changes and commit the updated README.

### Release

1. Update [CHANGELOG.md](CHANGELOG.md) and bump `<Version>` in [`Directory.Build.props`](Directory.Build.props) (`X.Y.Z` or SemVer prerelease such as `0.1.0-alpha.1`).
2. Commit on `main`, tag `v{version}` on HEAD (e.g. `v0.1.0-alpha.1`). GitHub marks hyphenated versions as prerelease.
3. Run `utils\Invoke-ReleasePackage.bat`. GitHub assets are the portable zip (win-x64), Windows setup exe (Avalonia UI), and Flatpak (Avalonia UI).

## Commit format

```text
(type): description
```

Types: `(feature):`, `(bugfix):`, `(refactor):`, `(perf):`, `(test):`, `(docs):`, `(build):`, `(ci):`, `(style):`, `(revert):`, `(chore):`.

Lowercase description; no trailing period.

## Code style

- File-scoped namespaces; two blank lines after the last `using`
- Usings: System → Microsoft → 3rd party → MaksIT (length-sorted within each group; no blank lines between groups)
- K&R braces; prefer `var`; omit `{}` on single-statement `if` / loops
- Prefer emulator tests when physical tape is unavailable

## Reporting issues

Include OS, .NET version, `DeviceMode` / `Topology`, and relevant logs. Physical tape/changer bugs should note Windows (`\\.\Tape0` / `\\.\Changer0`) or Linux (`/dev/nst*` / `/dev/sg*`) paths.

If the issue needs hardware you cannot test, funding or drive sponsorship helps — see [README.md](README.md) / Buy Me a Coffee links.

## Pull requests

1. Build and emulator tests pass (`utils\Invoke-TestEngine.bat` or `dotnet test`).
2. Update README / CHANGELOG when behavior or public config changes.
3. Keep the diff scoped; no unrelated refactors.

## Contact

- **Email**: [maksym.sadovnychyy@gmail.com](mailto:maksym.sadovnychyy@gmail.com)
- **Reddit**: [MaksIT.LTO.Backup thread](https://www.reddit.com/r/MaksIT/comments/1ghgbx5/maksitltobackup_a_simplified_cli_tool_for_windows/)
