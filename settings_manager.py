import os
import sys
import gc
import json
import ctypes
from ctypes import wintypes

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
SETTINGS_FILE = os.path.join(BASE_DIR, "settings.json")

DEFAULT_SETTINGS = {
    "target_fps": 35,
    "eco_mode": True,
    "ui_live_preview": True,
    "idle_timeout_minutes": 15,
    "autostart": False,
    "minimize_to_tray": True,
    "macros_enabled": True,
    "last_mode": "rainbow_wave",
    "brightness": 1.0,
    "speed": 1.0,
    "primary_color": [0, 240, 255],
    "secondary_color": [255, 0, 128],
    "tertiary_color": [255, 184, 0],
    "direction": "right",
    "audio_sensitivity": 1.5
}


class PROCESS_MEMORY_COUNTERS(ctypes.Structure):
    _fields_ = [
        ("cb", wintypes.DWORD),
        ("PageFaultCount", wintypes.DWORD),
        ("PeakWorkingSetSize", ctypes.c_size_t),
        ("WorkingSetSize", ctypes.c_size_t),
        ("QuotaPeakPagedPoolUsage", ctypes.c_size_t),
        ("QuotaPagedPoolUsage", ctypes.c_size_t),
        ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t),
        ("QuotaNonPagedPoolUsage", ctypes.c_size_t),
        ("PagefileUsage", ctypes.c_size_t),
        ("PeakPagefileUsage", ctypes.c_size_t),
    ]


_kernel32 = ctypes.windll.kernel32
_psapi = ctypes.windll.psapi

_kernel32.GetCurrentProcess.restype = ctypes.c_void_p
_psapi.EmptyWorkingSet.argtypes = [ctypes.c_void_p]
_psapi.EmptyWorkingSet.restype = wintypes.BOOL
_psapi.GetProcessMemoryInfo.argtypes = [ctypes.c_void_p, ctypes.POINTER(PROCESS_MEMORY_COUNTERS), wintypes.DWORD]
_psapi.GetProcessMemoryInfo.restype = wintypes.BOOL
_kernel32.SetProcessWorkingSetSize.argtypes = [ctypes.c_void_p, ctypes.c_size_t, ctypes.c_size_t]
_kernel32.SetProcessWorkingSetSize.restype = wintypes.BOOL


def trim_process_memory():
    """
    Triggers Python GC and instructs Windows kernel to trim the working set
    of the current process for ultra-low RAM usage.
    """
    try:
        gc.collect()
        handle = _kernel32.GetCurrentProcess()
        _psapi.EmptyWorkingSet(handle)
        _kernel32.SetProcessWorkingSetSize(handle, ctypes.c_size_t(-1), ctypes.c_size_t(-1))
    except Exception:
        pass


def get_process_memory_mb():
    """Returns exact current process working set RAM in MB."""
    try:
        pmc = PROCESS_MEMORY_COUNTERS()
        pmc.cb = ctypes.sizeof(PROCESS_MEMORY_COUNTERS)
        handle = _kernel32.GetCurrentProcess()
        if _psapi.GetProcessMemoryInfo(handle, ctypes.byref(pmc), pmc.cb):
            return round(pmc.WorkingSetSize / (1024.0 * 1024.0), 2)
    except Exception:
        pass
    return 8.5


class SettingsManager:
    def __init__(self):
        self.data = dict(DEFAULT_SETTINGS)
        self.load()

    def load(self):
        if os.path.exists(SETTINGS_FILE):
            try:
                with open(SETTINGS_FILE, "r", encoding="utf-8") as f:
                    loaded = json.load(f)
                    if isinstance(loaded, dict):
                        self.data.update(loaded)
            except Exception:
                pass

    def save(self):
        try:
            with open(SETTINGS_FILE, "w", encoding="utf-8") as f:
                json.dump(self.data, f, indent=2)
        except Exception:
            pass

    def update(self, new_values: dict):
        self.data.update(new_values)
        if "autostart" in new_values:
            self._apply_autostart(bool(new_values["autostart"]))
        self.save()

    def _apply_autostart(self, enable: bool):
        try:
            import winreg
            key_path = r"Software\Microsoft\Windows\CurrentVersion\Run"
            app_name = "RedragonK617RGBStudio"
            exe_path = os.path.join(BASE_DIR, "RedragonK617_Studio.exe")
            if not os.path.exists(exe_path):
                exe_path = f'"{sys.executable}" "{os.path.join(BASE_DIR, "app.py")}"'
            else:
                exe_path = f'"{exe_path}" --minimized'

            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, key_path, 0, winreg.KEY_SET_VALUE) as reg_key:
                if enable:
                    winreg.SetValueEx(reg_key, app_name, 0, winreg.REG_SZ, exe_path)
                else:
                    try:
                        winreg.DeleteValue(reg_key, app_name)
                    except FileNotFoundError:
                        pass
        except Exception:
            pass
