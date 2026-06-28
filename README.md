# Robs Wind Sim

Windows tray application that drives two PWM fan channels on an Arduino Nano for a DIY sim racing wind simulator. Reads live speed from iRacing telemetry (or manual test mode) and maps it to fan PWM output.

## Hardware

- **Arduino Nano** (ATmega328) over USB serial
- **Left channel:** pin D9 → 2× Noctua NF-A14 iPPC-3000 (Y-splitter)
- **Right channel:** pin D10 → 2× Noctua NF-A14 iPPC-3000 (Y-splitter)
- **12V PSU** (Mean Well LRS-75-12) powers fans directly; Arduino supplies PWM signal only
- **Shared ground** between PSU −V and Arduino GND
- **D2 / D3** are wired to fan tach signals but reserved for future use — not used by this firmware

PWM is generated at ~31 kHz via Timer1 register configuration (not `analogWrite()`), which matches Noctua's 18–30 kHz PWM spec.

## Serial Protocol

Newline-terminated ASCII commands at **115200 baud**. No acknowledgment from the Arduino.

| Direction     | Format              | Example        |
|---------------|---------------------|----------------|
| App → Arduino | `PWM <left> <right>` | `PWM 128 256` |

- `<left>` and `<right>` are integer duty values **0–511** (9-bit Timer1 compare values).
- Malformed lines are ignored.
- **Fail-safe:** if no valid command is received for 2.5 seconds, both channels ramp to 0.

You can bench-test with the Arduino Serial Monitor: set line ending to Newline, send `PWM 128 128` at 115200.

## Arduino Firmware

1. Open [arduino/wind-sim-fans/wind-sim-fans.ino](arduino/wind-sim-fans/wind-sim-fans.ino) in the Arduino IDE.
2. Board: **Arduino Nano**.
3. Processor: **ATmega328P** — pick Old or New Bootloader to match your board.
4. Upload.

## iRacing Setup

No separate iRacing SDK install is required. The app uses [IRSDKSharper](https://github.com/mherbold/IRSDKSharper) (GPL-3.0) to read shared-memory telemetry while iRacing is running.

Enable memory-based telemetry in iRacing's `app.ini` (typically under `Documents\iRacing\app.ini`):

```ini
irsdkEnableMem=1
```

Restart iRacing after changing this setting. With Test Mode off (the default), the app reads live `Speed` telemetry at ~30 Hz and converts m/s to mph for fan mapping.

## Windows App

### Requirements

- .NET 10 SDK
- Windows x64
- iRacing with `irsdkEnableMem=1` (for live mode)

### Run from source

```bash
scripts\dev.cmd
```

Or directly:

```bash
dotnet run --project src/RobsWindSim/RobsWindSim.csproj -c Release
```

Day-to-day dev uses the installed .NET runtime — no hundreds of loose DLLs in `bin/`.

### Publish standalone .exe

```bash
scripts\publish.cmd
```

Or directly:

```bash
dotnet publish src/RobsWindSim/RobsWindSim.csproj -p:PublishProfile=win-x64
```

Output: `src/RobsWindSim/bin/Release/net10.0-windows/win-x64/publish/RobsWindSim.exe` (single self-contained file).

Settings are saved to `%LocalAppData%\RobsWindSim\settings.json`. Older settings under `%LocalAppData%\WindSim` or beside the exe are migrated automatically on first launch.

### Install (end users)

Download **RobsWindSim-Setup-x.y.z.exe** from [GitHub Releases](https://github.com/robneville73/robs-wind-sim/releases/latest), run the installer, and launch **Robs Wind Sim** from the Start Menu.

Windows may show a SmartScreen warning because the installer is not code-signed. Choose **More info** → **Run anyway** to proceed.

### Build installer (developers)

Prerequisites:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`)

```bash
scripts\build-installer.cmd
```

Output: `artifacts\RobsWindSim-Setup-{version}.exe` (version read from `src/RobsWindSim/RobsWindSim.csproj`).

### Releasing

1. Bump `<Version>` in [src/RobsWindSim/RobsWindSim.csproj](src/RobsWindSim/RobsWindSim.csproj).
2. Commit and push to `main`.
3. Tag and push (or use **Actions → Release → Run workflow**):

```bash
git tag v1.1.0
git push origin v1.1.0
```

GitHub Actions builds the installer and attaches it to a new [Release](https://github.com/robneville73/robs-wind-sim/releases). Check the **Actions** tab if a release does not appear within a few minutes.

### Tray behavior

- **Red** icon — Arduino disconnected
- **Gold** icon — connected, idle/low output
- **Green** icon — connected, active fan output
- Double-click tray icon or right-click → **Settings**
- Right-click → **Master On/Off**, **Exit**

Default global hotkeys: **F9** (master toggle), **F10** (idle up), **F11** (idle down). Rebind in Settings.

## Testing

### Live iRacing mode (default)

1. Confirm `irsdkEnableMem=1` in iRacing `app.ini`.
2. Flash the Arduino sketch and connect USB.
3. Launch `RobsWindSim.exe` (or `dotnet run`).
4. Open **Settings**, select the Arduino COM port, click **Refresh** if needed.
5. Confirm **Arduino: Connected** when the port is correct.
6. Start iRacing and enter a live session (test drive or practice).
7. Confirm **iRacing: Connected — X mph** updates in Settings.
8. Drive — fans should track speed via the configured mapping curve.
9. Stop in pits at 0 mph — idle fan speed applies (default 17%).
10. Exit iRacing — fans return to idle speed (same as speed 0 in pits).
11. Toggle **Test Mode** on — manual slider takes over immediately.

**Replay mode:** On the **Fans** tab, enable **Replay mode** to drive fans from replay telemetry. Off by default — replays hold idle speed only.

### Manual test mode

1. Open **Settings** → **Test Mode** tab.
2. Enable **Use manual test speed instead of iRacing**.
3. Move the speed slider — fans should track mapped output.
4. Click **Run test sweep** — speed ramps 0 → max → 0 over ~25 seconds.
5. Toggle **Master On/Off** (tray menu or F9) — fans stop regardless of slider.
6. Unplug USB — status shows disconnected; replug and it retries automatically.

**Note:** At speed 0 (live session, iRacing not running, or test mode), the configured **idle fan speed** (default 17%) is applied instead of full stop. Set idle to 0% if you want silence at zero speed.

## Architecture

Speed input is abstracted via `ISpeedSource`. Live mode uses `IracingSpeedSource` (IRSDKSharper); test mode uses `ManualTestSpeedSource`. A single `FanOutputPipeline` maps speed → fan percentage regardless of source.

## Out of Scope

- Fan tach/RPM reading (D2/D3 reserved)
- Left/right differential cornering effects
- SimHub dependency

## Third-Party Licenses

- [IRSDKSharper](https://github.com/mherbold/IRSDKSharper) — GPL-3.0
