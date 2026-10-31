# FaceOff

Turn FACEIT's kernel anti-cheat **off** when you're not playing, and back **on** with one click.

## The problem

FACEIT's anti-cheat installs a kernel driver (`FACEIT_AC.sys`, service name `faceit`) and a service (`FACEITService`). Close the client and they keep running. The driver still loads every time Windows boots, even if you haven't played in weeks. Code running at kernel level has full access to your machine. If you care about privacy and security, you only want that running while you play.

FACEIT's tray icon can unload the driver, but it [loads again on the next boot](https://support.faceit.com/hc/en-us/articles/9394666828188-What-is-FACEIT-Anti-cheat-and-how-does-it-work). FaceOff keeps it off until you turn it back on.

## What FaceOff does

- **Turn off:** closes the anti-cheat tray client (same as **Exit** in its tray menu) and the FACEIT app, then sets the driver and service to `Disabled` and stops them. Windows usually won't unload the driver while it's running, so FaceOff tells you when a restart is needed. Tick **Keep the FACEIT app open** if you only want the anti-cheat off.
- **Turn on:** restores the **exact** start types FACEIT had before (saved the first time you turn it off) and starts it again.
- **Status:** shows whether the driver is loaded and how each one starts.

What it doesn't do: it doesn't delete or modify any FACEIT file, it makes no network connections, and it adds nothing that runs at startup. All it changes is two service start types.

## Download

1. Get `FaceOff.exe` from [Releases](../../releases). It's a single portable file of about 110 KB with nothing to install. It needs .NET Framework 4.8, which ships with Windows 10 and 11.
2. Optional: check the file hash against `SHA256SUMS.txt`:
   ```powershell
   Get-FileHash .\FaceOff.exe -Algorithm SHA256
   ```
3. FaceOff asks for administrator rights because changing services requires them.

**"Windows protected your PC"?** The exe isn't code-signed, because certificates cost money. Click **More info → Run anyway**. The source code is all here, and every release is built by [GitHub Actions](.github/workflows/build.yml) straight from this repo.

## Usage

Double-click `FaceOff.exe` and press **TURN OFF** / **TURN ON**.

Command line (run from an elevated terminal):

```
FaceOff.exe --status
FaceOff.exe --disable [--keep-app] [--reboot]
FaceOff.exe --enable  [--reboot]
```

`--keep-app` leaves the FACEIT app running. Exit codes: `0` ok, `1` error, `2` restart required.

Typical routine: **Turn on → play → Turn off → restart.**

FaceOff saves FACEIT's original start types and your settings in `FaceOff.state` next to the exe. Keep the file with the exe.

## Prefer no binary?

[`scripts/faceit-off.bat`](scripts/faceit-off.bat) and [`scripts/faceit-on.bat`](scripts/faceit-on.bat) do the same thing. Run them as administrator. The one difference: `faceit-on.bat` always sets manual start instead of restoring your original setting.

## How it works

| Step | Turn off | Turn on |
|---|---|---|
| 1 | Save the current start types | Restore the saved start type for `faceit` and start it |
| 2 | Close `faceitclient.exe` and `FACEIT.exe` (unless you keep the app) | Restore the saved start type for `FACEITService` and start it |
| 3 | `FACEITService` → Disabled, stop | |
| 4 | `faceit` → Disabled, try to stop (restart if it won't) | |

All logic lives in [`Toggler.cs`](src/FaceOff/Toggler.cs) and [`ServiceControl.cs`](src/FaceOff/ServiceControl.cs).

## Build

```
dotnet build src/FaceOff/FaceOff.csproj -c Release
```

## Disclaimer

FaceOff is not affiliated with or endorsed by FACEIT. It only changes Windows service settings that you could also change with `sc config`. Use at your own risk. To get rid of the anti-cheat for good, uninstall FACEIT instead.

## License

[GPL-3.0](LICENSE)
