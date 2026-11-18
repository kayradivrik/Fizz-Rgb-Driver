# Contributing to Fizz-Rgb-Driver

Thank you for your interest in contributing to **Fizz-Rgb-Driver**!

## Development & Building

1. Clone the repository:
   ```powershell
   git clone https://github.com/kayradivrik/Fizz-Rgb-Driver.git
   cd Fizz-Rgb-Driver
   ```
2. Build the native executables (`FizzCtl.exe` and `FizzCtl_Setup.exe`):
   ```powershell
   .\build_exe.bat
   ```
3. Or run the Python/WebView engine directly:
   ```powershell
   python app.py
   ```

## Adding New Lighting Presets

Per-key lighting presets are stored in `profiles/*.json` as a mapping of the 61 key IDs (`esc`, `1`..`0`, `q`..`p`, `a`..`l`, `z`..`m`, modifiers) to `[R, G, B]` arrays.

## Pull Requests

- Keep memory allocations outside the 35–60 FPS render loop (`src/EffectsAndMacros.cs` and `effects_engine.py`).
- Test HID packets on physical `VID_258A&PID_0049` hardware before submitting protocol changes.
