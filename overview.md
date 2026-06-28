# Wind Sim Controller — Project Spec

## Overview

A lightweight Windows background application (C#, .NET) that reads live telemetry from iRacing and drives two PWM fan channels (via an Arduino Nano over USB serial) to simulate wind speed in a DIY sim racing wind simulator. Replaces SimHub's wind effect, which has a confirmed firmware/communication bug preventing live PWM updates from reaching the Arduino.

The app runs primarily in the system tray, with a minimal settings window and global hotkey support for Stream Deck integration.

## Hardware Context

- **Arduino Nano** (ATmega328, new bootloader), connected via USB serial
- **Two independent PWM fan channels**: pin D9 (left housing), pin D10 (right housing)
- Each channel drives 2x Noctua NF-A14 iPPC-3000 PWM fans (140mm) via a Y-splitter
- Fans are powered directly from a 12V Mean Well LRS-75-12 PSU (not through the Arduino) — the Arduino only supplies the PWM signal wire, with a shared ground between the PSU -V and Arduino GND
- Noctua NF-A14 iPPC-3000 fans expect a PWM control frequency of 18-30kHz per their datasheet. Standard Arduino `analogWrite()` on pins 9/10 defaults to ~490Hz, which is out of spec and can cause audible whining or poor low-end speed control on some PWM fans. This project deliberately avoids `analogWrite()` in favor of direct hardware timer register configuration (below), which has already been confirmed working on this exact hardware (Nano + NF-A14 iPPC-3000, pin D9) in a standalone test — fan speed responded correctly and smoothly across the full range with no audible whine.

### Arduino Sketch — Build From Scratch

Write a new Arduino sketch (.ino) with no dependency on SimHub or any SimHub-generated firmware. Requirements:

**PWM generation (both channels at ~25-31kHz, NOT analogWrite):**
- Pin 9 and pin 10 are both controlled by the ATmega328's **Timer1** (Channel A = pin 9, Channel B = pin 10), so a single timer configuration drives both channels independently via separate compare registers
- Configure Timer1 in Fast PWM mode with `ICR1` as the TOP value to set the PWM frequency:
  ```cpp
  TCCR1A = _BV(COM1A1) | _BV(COM1B1) | _BV(WGM11);
  TCCR1B = _BV(WGM13) | _BV(CS10);
  ICR1 = 511; // ~31.25kHz at 16MHz clock, gives 9-bit duty cycle resolution (0-511)
  ```
- Set duty cycle independently per channel:
  ```cpp
  OCR1A = leftDuty;   // pin 9 (left channel), 0-511
  OCR1B = rightDuty;  // pin 10 (right channel), 0-511
  ```
- `pinMode(9, OUTPUT)` and `pinMode(10, OUTPUT)` must be set in `setup()`

**Serial command handling (see protocol section below):**
- `Serial.begin()` at a fixed baud rate (suggest 115200)
- In `loop()`, continuously check for incoming serial data (non-blocking, e.g. `Serial.available()`) and parse complete newline-terminated commands
- On receipt of a valid command, immediately update `OCR1A` and/or `OCR1B` accordingly — no delay, no blocking waits anywhere in `loop()`
- Ignore the fans' tach/RPM signal wire entirely — it is not wired to the Arduino in this build

**Safety/fallback behavior:**
- If no valid serial command is received for some timeout period (suggest 2-3 seconds), the sketch should fail safe by ramping both channels down to 0 — this protects against the PC app crashing or the USB connection dropping while fans are at high speed
- On `setup()`, both channels should start at 0 (not an arbitrary boot-time value)

Propose the exact serial protocol (command format, parsing approach) as part of the implementation, following the constraints in the "Serial Communication Protocol" section below, and keep the C# app and this sketch in sync on whatever format is chosen.

**Reserved pins — do not use for anything else:**
- **D2** and **D3** are physically wired to the left and right fans' tach/RPM signal wires, respectively, but are **not used by this version of the sketch or app**. They are reserved for a possible future feature (RPM readout). Do not assign D2 or D3 to any other purpose. No code for reading them is needed in this version — leave them unconfigured/unused in the sketch for now.

## Core Architecture

The app should be built so that **speed input is abstracted from speed-consuming logic**. There must be exactly one code path that takes "current speed" and converts it to PWM duty cycle output — whether that speed comes from live iRacing telemetry or from a manual test input should be invisible to that logic.

### Speed Source (pluggable)
1. **iRacing SDK** — live telemetry, poll `Speed` (m/s, convert to mph or kph for UI display) at ~20-30Hz. Use an existing maintained C# iRacing SDK wrapper (e.g. irsdkSharp or similar — please identify the best current option and note any setup/installation steps in the README).
2. **Manual test mode** — a slider in the settings UI (0 to max configured speed) that feeds the exact same downstream pipeline. Also include a "Run test sweep" button that automatically ramps the test value from 0 → max → 0 over ~20-30 seconds, so the user can sit in the seat and feel the full range without touching the keyboard.

Only one speed source is active at a time, toggled via a UI switch ("Test Mode" / "Live iRacing Mode").

### Speed-to-PWM Mapping
- Input: absolute vehicle speed (mph), NOT scaled to each car's top speed (intentional design decision — wind force is a function of real airspeed, not a car's theoretical max)
- A single global "max speed" reference (default 200 mph, user configurable) maps to 100% fan output
- Configurable curve type: **Linear** and **Exponential** (ease-in, so low speeds stay quiet and intensity ramps up more aggressively near max — implement as a simple power curve with an adjustable exponent)
- Output is a 0-100% duty cycle value, separately for left and right channel

### Per-Channel Configuration
- Independent enable/disable toggle for left and right channel
- Independent max-power scaling per channel (0-100%, lets user balance two physically different housings/fans)
- A "Sync channels" toggle — when on, right channel always mirrors left channel's output (ignoring its own scaling), reducing this to effectively one unified control. When off, channels are fully independent.

### Idle Behavior
- When not actively driving in a live session (or in test mode with speed = 0), apply a separate **idle fan speed** (0-100%, user configurable, default low e.g. 15-20%) instead of dropping to zero
- This idle value is adjustable live via Stream Deck hotkeys (see below) without opening the settings window

## Stream Deck / Global Hotkey Integration

Three global hotkeys (work system-wide, app doesn't need focus), each user-remappable in settings:

1. **Master On/Off** — toggles the entire fan output on/off (overrides everything else; when off, both channels are forced to 0 regardless of speed or idle setting)
2. **Idle Speed Up** — increases idle fan speed by a configurable step (default 5%), capped at 100%
3. **Idle Speed Down** — decreases idle fan speed by the same step, floored at 0%

Use a standard .NET global hotkey library/approach (e.g. low-level keyboard hook or `RegisterHotKey` Win32 API) — these should work regardless of which application currently has focus, since they'll be triggered by physical Stream Deck button presses sending keystrokes.

## Settings UI (WinForms)

Minimal single-window settings panel, accessible via tray icon (double-click or right-click menu → Settings). Should include:

- COM port selection (dropdown, populated from available serial ports, with refresh button)
- Max speed reference (numeric input, mph)
- Curve type selector (Linear / Exponential) + exponent value if exponential selected
- Per-channel: enable toggle, max power scaling slider
- Sync channels toggle
- Idle speed setting (slider, also reflects live changes made via hotkeys)
- Hotkey rebinding for the 3 Stream Deck actions
- Test Mode toggle + manual speed slider + "Run test sweep" button
- Connection status indicators (iRacing SDK connected? Arduino serial connected?)

Window can be closed to tray (not exited) via the standard minimize-to-tray pattern. Provide a genuine "Exit" option in the tray right-click menu.

## System Tray

- Tray icon reflects basic status (e.g. icon color/overlay for: disconnected, connected-idle, connected-active)
- Right-click menu: Settings, Master On/Off toggle, Exit
- Tooltip on hover shows current left/right fan percentage

## Serial Communication Protocol (App ↔ Arduino)

Propose a simple, robust, human-readable line-based protocol (e.g. newline-terminated ASCII commands). Should be resilient to:
- Arduino not yet connected when app starts (app should poll/retry, not crash)
- App sending updates at a reasonable rate (~10-20Hz is plenty; no need to spam faster than the fan can physically respond)
- Brief serial disconnects (USB unplug/replug) — app should detect and attempt reconnection, surfaced in the tray icon/status

Document the exact protocol in the README so the Arduino sketch and the C# app stay in sync if either is modified later.

## Deliverables

1. **C# .NET WinForms application** (target .NET 10 LTS, self-contained publish so it runs as a standalone .exe without requiring a separately installed runtime)
2. **Arduino sketch (.ino)** implementing the serial protocol and direct-register PWM output on D9/D10
3. **README.md** covering: setup steps (installing iRacing SDK dependency, wiring reference, COM port setup), the serial protocol spec, and how to build/run from Cursor

## Explicitly Out of Scope (for now)

- Stream Deck plugin SDK integration (using global hotkeys instead — far simpler, no Stream Deck-specific code needed)
- Per-car-class speed scaling (may revisit later if absolute-speed mapping feels underwhelming in slower cars)
- Left/right differential cornering effects (SimHub's "curving" effect) — channels are either synced or independently/manually scaled, not dynamically driven by steering input
- Tach/RPM reading and display (green wires are physically wired to D2/D3 for possible future use, but no software support for reading them is part of this build — see "Reserved pins" note above)
- Any dependency on SimHub being installed or running
