# MaksIT.LTO.Backup

**⚠️ Warning: This program is currently under fine-tuning and some features are still being added. Extensive real-world testing is still in progress. Use it at your own risk.**

A C# application designed to facilitate backup and restore operations to an LTO tape drive. This application enables seamless management of backups by handling file organization, descriptor creation, and efficient tape handling processes for loading, writing, and restoring data.

**🚀 Support MaksIT.LTO.Backup**
Help me bring reliable and affordable data backup solutions to everyone! With your support, I can test across multiple LTO generations, enhance this software, and ensure compatibility and performance for all users.

[Contribute to MaksIT.LTO.Backup’s development here!](https://gofund.me/6ef96254)

Every donation brings us closer to providing a robust, free backup tool that prioritizes data security and accessibility. Thank you for being part of this journey!

## Version History

**v0.0.1 - Initial Release (01/11/2024)**
- Initial implementation of backup and restore operations.
- Support for loading and ejecting tape.
- Basic file descriptor management.
- Customizable block sizes for different LTO generations.
- File checksum verification during restore

**v0.0.2 - (03/11/2024)**
- Use of Crc32 for restored data checksums
- Improved descriptor handling with AESGCM signature and integrity check
- Code review, DI, File logger

## Requirements

- .NET8 or higher
- JSON configuration files: `configuration.json` and `descriptor.json` (auto-generated)
- LTO Tape Drive and compatible drivers

## Setup

1. Clone the repository:
   ```bash
   git clone https://github.com/MAKS-IT-COM/maksit-lto-backup
   ```
2. Ensure `.NET8 SDK` is installed on your system.
3. Prepare a `configuration.json` file in the application directory with the following structure:

```json
{
  "TapePath": "\\\\.\\Tape0",
  "WriteDelay": 100,
  "Backups": [
    {
      "Name": "Normal test",
      "Barcode": "",
      "LTOGen": "LTO5",
      "Source": {
        "LocalPath": {
          "Path": "F:\\LTO\\Backup"
        }
      },
      "Destination": {
        "LocalPath": {
          "Path": "F:\\LTO\\Restore"
        }
      }
    },
    {
      "Name": "Network test",
      "Barcode": "",
      "LTOGen": "LTO5",
      "Source": {
        "RemotePath": {
          "Path": "\\\\nassrv0001.corp.maks-it.com\\data-1\\Users",
          "PasswordCredentials": {
            "Username": "",
            "Password": ""
          },
          "Protocol": "SMB"
        }
      },
      "Destination": {
        "LocalPath": {
          "Path": "F:\\LTO\\Restore"
        }
      }
    }
  ]
}
```

## Usage

### Running the Application

Execute the application by navigating to the project directory `src\MaksIT.LTO.Backup` and running:

```powershell
dotnet build && dotnet run
```

or use `dotnet_build_script.bat` to generate executables in `src\build_outputs`. From version `v0.0.2` I started to provide already compiled binaries in [Releases](https://github.com/MAKS-IT-COM/maksit-lto-backup/releases). You have to execute `MaksIT.LTO.Backup.exe` with administrative privileges.

> ⚠️ **Warning**:  When the application is started for the first time, a special secret.txt will be generated in the program root folder. This secret is used to sign descriptor and used during restore to decrypt it and perform integrity check. Please keep its copy safe somwhere.
> In case if in your scenario `secret.txt` is a seurity risk, then create system environment variable `LTO_BACKUP_SECRET` and use content from `secret.txt` as a value, after you can delete `secret.txt` from the application folder.

Here is a powershell snippet to create system environment variable:

```powershel
[System.Environment]::SetEnvironmentVariable("LTO_BACKUP_SECRET", "<secret.txt content here>", [System.EnvironmentVariableTarget]::Machine)
```


### Application Menu

Upon running, the following options will be presented:

1. **Load Tape**: Loads the tape into the drive.
2. **Backup**: Prompts the user to select a backup task from the configured list and initiates the backup process.
3. **Restore**: Restores a previously backed-up directory from the tape.
4. **Eject Tape**: Ejects the tape from the drive.
5. **Get device status**: Mostly used for debugging to understand if device is able to write
6. **Tape Erase (Short)**: Short tape erase
7. **Exit**: Exits the application.

### Sample Configuration

Below is an example configuration setup for an LTO-6 tape generation backup operation:

```json
{
  "TapePath": "\\\\.\\Tape0",
  "WriteDelay": 100,
  "Backups": [
    {
      "Name": "Normal test",
      "Barcode": "",
      "LTOGen": "LTO5",
      "Source": {
        "LocalPath": {
          "Path": "F:\\LTO\\Backup"
        }
      },
      "Destination": {
        "LocalPath": {
          "Path": "F:\\LTO\\Restore"
        }
      }
    },
    {
      "Name": "Network test",
      "Barcode": "",
      "LTOGen": "LTO5",
      "Source": {
        "RemotePath": {
          "Path": "\\\\nassrv0001.corp.maks-it.com\\data-1\\Users",
          "PasswordCredentials": {
            "Username": "",
            "Password": ""
          },
          "Protocol": "SMB"
        }
      },
      "Destination": {
        "LocalPath": {
          "Path": "F:\\LTO\\Restore\\Users"
        }
      }
    }
  ]
}
```

### Error Handling

Errors during backup or restore are caught and logged to the console and to `log.txt` in the program root folder.

## Contact

If you have any questions or need further assistance, feel free to reach out:

- **Email**: [maksym.sadovnychyy@gmail.com](mailto:maksym.sadovnychyy@gmail.com)
- **Reddit**: [MaksIT.LTO.Backup: A Simplified CLI Tool for Windows LTO Tape Backups](https://www.reddit.com/r/MaksIT/comments/1ghgbx5/maksitltobackup_a_simplified_cli_tool_for_windows/?utm_source=share&utm_medium=web3x&utm_name=web3xcss&utm_term=1&utm_content=share_button)

## License

This project is licensed under the terms of the GPLv2. See the [LICENSE](./LICENSE) file for full details.

© Maksym Sadovnychyy (MAKS-IT) 2024