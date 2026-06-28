# Wind Sim Controller

Windows tray application that drives two PWM fan channels on an Arduino Nano for a DIY sim racing wind simulator. Phase 1 provides manual test mode and serial control; iRacing telemetry integration is planned for phase 2.

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

## Windows App

### Requirements

- .NET 10 SDK
- Windows x64

### Run from source

```bash
scripts\dev.cmd
```

Or directly:

```bash
dotnet run --project src/WindSim/WindSim.csproj -c Release
```

Day-to-day dev uses the installed .NET runtime — no hundreds of loose DLLs in `bin/`.

### Publish standalone .exe

```bash
scripts\publish.cmd
```

Or directly:

```bash
dotnet publish src/WindSim/WindSim.csproj -p:PublishProfile=win-x64
```

Output: `src/WindSim/bin/Release/net10.0-windows/win-x64/publish/WindSim.exe` (single self-contained file).

Settings are saved to `%LocalAppData%\WindSim\settings.json`. If you previously ran a portable copy with `settings.json` beside the exe, settings are migrated automatically on first launch.

### Install (end users)

Download **WindSim-Setup-x.y.z.exe** from [GitHub Releases](https://github.com/robneville73/robs-wind-sim/releases), run the installer, and launch **Wind Sim Controller** from the Start Menu.

Windows may show a SmartScreen warning because the installer is not code-signed. Choose **More info** → **Run anyway** to proceed.

### Build installer (developers)

Prerequisites:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`)

```bash
scripts\build-installer.cmd
```

Output: `artifacts\WindSim-Setup-{version}.exe` (version read from `src/WindSim/WindSim.csproj`).

### Releasing

1. Bump `<Version>` in [src/WindSim/WindSim.csproj](src/WindSim/WindSim.csproj).
2. Commit and push to `main`.
3. Tag and push:

```bash
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions builds the installer and attaches it to a new [Release](https://github.com/robneville73/robs-wind-sim/releases).

### Tray behavior

- **Red** icon — Arduino disconnected
- **Gold** icon — connected, idle/low output
- **Green** icon — connected, active fan output
- Double-click tray icon or right-click → **Settings**
- Right-click → **Master On/Off**, **Exit**

Default global hotkeys: **F9** (master toggle), **F10** (idle up), **F11** (idle down). Rebind in Settings.

## Phase 1 Testing

1. Flash the Arduino sketch.
2. Launch `WindSim.exe` (or `dotnet run`).
3. Open **Settings**, select the Arduino COM port, click **Refresh** if needed.
4. Confirm **Arduino: Connected** when the port is correct.
5. Enable **Test Mode**, move the speed slider — fans should track mapped output.
6. Click **Run test sweep** — speed ramps 0 → max → 0 over ~25 seconds.
7. Toggle **Master On/Off** (tray menu or F9) — fans stop regardless of slider.
8. Unplug USB — status shows disconnected; replug and it retries automatically.
9. Adjust per-channel enable, max power, and sync toggles to verify independent control.

**Note:** At speed 0 (live or test mode), the configured **idle fan speed** (default 17%) is applied instead of full stop. Set idle to 0% if you want silence at zero speed during testing.

## Architecture (phase 2 ready)

Speed input is abstracted via `ISpeedSource`. Phase 1 uses `ManualTestSpeedSource`; `IracingSpeedSource` is a stub. A single `FanOutputPipeline` maps speed → fan percentage regardless of source, so iRacing can be added without changing the PWM path.

## Out of Scope (phase 1)

- iRacing SDK / live telemetry
- Fan tach/RPM reading (D2/D3 reserved)
- SimHub dependency
