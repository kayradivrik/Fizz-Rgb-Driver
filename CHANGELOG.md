# Changelog

All notable changes to **Fizz-Rgb-Driver** are documented in this file.

## [3.0.0] - 2026-11-18
### Added
- Native C# DirectX WPF desktop application (`FizzCtl.exe` / `RedragonK617_Studio.exe`) with custom dark XAML control templates and 3D sculpted double-shot PBT keycap stage.
- Self-contained graphical setup installer (`FizzCtl_Setup.exe` / `RedragonK617_Setup.exe`) with desktop/start menu shortcut creation and Windows startup registration.
- Low-level `WH_KEYBOARD_LL` Macro Studio supporting live keystroke recording, `once`, `repeat_n`, and `toggle_loop` modes.
- Win32 `EmptyWorkingSet` + `SetProcessWorkingSetSize` RAM optimizer reducing Working Set memory usage to ~0.9–4.5 MB.
- 23 real-time software lighting modes and 8 esports/theme per-key presets.

## [2.0.0] - 2026-11-15
### Added
- Zero-NumPy audio visualizer and dirty-flag USB HID report optimization.
- 8 reverse-engineered 1032-byte firmware templates (`Col06`).

## [1.0.0] - 2026-11-14
### Added
- Initial 382-byte `Col08` USB HID per-key RGB driver for Redragon K617 Fizz (`VID_258A&PID_0049`).
