import os
import json
import time
import ctypes
import threading

try:
    from pynput import keyboard as pynput_kb
    from pynput import mouse as pynput_mouse
    PYNPUT_OK = True
except Exception:
    PYNPUT_OK = False

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
MACROS_FILE = os.path.join(BASE_DIR, "macros.json")

DEFAULT_MACROS = [
    {
        "id": "macro_quickswitch",
        "name": "CS2 / Valorant Quick Switch (AWP/Op)",
        "trigger": "f6",
        "repeat_mode": "once",
        "repeat_count": 1,
        "enabled": False,
        "steps": [
            {"type": "key_press", "value": "3"},
            {"type": "delay", "value": 45},
            {"type": "key_press", "value": "1"}
        ]
    },
    {
        "id": "macro_autoclick",
        "name": "Turbo Sol Tık (Auto-Clicker 40 CPS)",
        "trigger": "f7",
        "repeat_mode": "toggle_loop",
        "repeat_count": 1,
        "enabled": False,
        "steps": [
            {"type": "mouse_click", "value": "left"},
            {"type": "delay", "value": 25}
        ]
    },
    {
        "id": "macro_bhop",
        "name": "BunnyHop Zıplama Döngüsü",
        "trigger": "f8",
        "repeat_mode": "toggle_loop",
        "repeat_count": 1,
        "enabled": False,
        "steps": [
            {"type": "key_press", "value": "space"},
            {"type": "delay", "value": 32}
        ]
    },
    {
        "id": "macro_ggwp",
        "name": "Hızlı Oyun Sonu Mesajı (GG WP)",
        "trigger": "f9",
        "repeat_mode": "once",
        "repeat_count": 1,
        "enabled": False,
        "steps": [
            {"type": "key_press", "value": "enter"},
            {"type": "delay", "value": 30},
            {"type": "text", "value": "GG WP!"},
            {"type": "delay", "value": 20},
            {"type": "key_press", "value": "enter"}
        ]
    }
]

VK_MAP = {
    "backspace": 0x08, "tab": 0x09, "enter": 0x0D, "shift": 0x10, "ctrl": 0x11, "alt": 0x12,
    "caps": 0x14, "esc": 0x1B, "space": 0x20, "left": 0x25, "up": 0x26, "right": 0x27, "down": 0x28,
    "0": 0x30, "1": 0x31, "2": 0x32, "3": 0x33, "4": 0x34, "5": 0x35, "6": 0x36, "7": 0x37, "8": 0x38, "9": 0x39,
    "a": 0x41, "b": 0x42, "c": 0x43, "d": 0x44, "e": 0x45, "f": 0x46, "g": 0x47, "h": 0x48, "i": 0x49,
    "j": 0x4A, "k": 0x4B, "l": 0x4C, "m": 0x4D, "n": 0x4E, "o": 0x4F, "p": 0x50, "q": 0x51, "r": 0x52,
    "s": 0x53, "t": 0x54, "u": 0x55, "v": 0x56, "w": 0x57, "x": 0x58, "y": 0x59, "z": 0x5A,
    "f1": 0x70, "f2": 0x71, "f3": 0x72, "f4": 0x73, "f5": 0x74, "f6": 0x75,
    "f7": 0x76, "f8": 0x77, "f9": 0x78, "f10": 0x79, "f11": 0x7A, "f12": 0x7B,
    "vol_mute": 0xAD, "vol_down": 0xAE, "vol_up": 0xAF, "media_next": 0xB0, "media_prev": 0xB1, "media_play": 0xB3
}


class MacroEngine:
    def __init__(self):
        self.lock = threading.Lock()
        self.macros = []
        self.global_enabled = True
        self.active_loops = {}  # {macro_id: stop_event}
        self.is_executing = False

        # Live Recorder state
        self.recording = False
        self.recorded_steps = []
        self.last_record_time = 0.0

        self.kb_controller = pynput_kb.Controller() if PYNPUT_OK else None
        self.mouse_controller = pynput_mouse.Controller() if PYNPUT_OK else None

        self.load()

    def load(self):
        if os.path.exists(MACROS_FILE):
            try:
                with open(MACROS_FILE, "r", encoding="utf-8") as f:
                    self.macros = json.load(f)
                return
            except Exception:
                pass
        self.macros = list(DEFAULT_MACROS)
        self.save()

    def save(self):
        try:
            with open(MACROS_FILE, "w", encoding="utf-8") as f:
                json.dump(self.macros, f, indent=2, ensure_ascii=False)
        except Exception:
            pass

    def get_macros(self):
        with self.lock:
            return list(self.macros)

    def add_or_update_macro(self, macro_obj: dict):
        with self.lock:
            mid = macro_obj.get("id")
            if not mid:
                mid = f"macro_{int(time.time() * 1000)}"
                macro_obj["id"] = mid
            found = False
            for idx, m in enumerate(self.macros):
                if m["id"] == mid:
                    self.macros[idx] = macro_obj
                    found = True
                    break
            if not found:
                self.macros.append(macro_obj)
            self.save()
        return macro_obj

    def delete_macro(self, macro_id: str):
        with self.lock:
            if macro_id in self.active_loops:
                self.active_loops[macro_id].set()
                del self.active_loops[macro_id]
            self.macros = [m for m in self.macros if m["id"] != macro_id]
            self.save()

    def toggle_macro(self, macro_id: str, enabled: bool):
        with self.lock:
            for m in self.macros:
                if m["id"] == macro_id:
                    m["enabled"] = bool(enabled)
                    if not enabled and macro_id in self.active_loops:
                        self.active_loops[macro_id].set()
                        del self.active_loops[macro_id]
                    break
            self.save()

    def start_recording(self):
        with self.lock:
            self.recording = True
            self.recorded_steps = []
            self.last_record_time = time.time()

    def stop_recording(self):
        with self.lock:
            self.recording = False
            return list(self.recorded_steps)

    def handle_key_event(self, key_name: str):
        """Called by the keyboard listener when any key is pressed."""
        if not key_name:
            return
        k_lower = key_name.lower()

        if self.recording:
            now = time.time()
            dt_ms = int((now - self.last_record_time) * 1000)
            self.last_record_time = now
            with self.lock:
                if len(self.recorded_steps) > 0 and dt_ms > 10:
                    self.recorded_steps.append({"type": "delay", "value": min(2000, dt_ms)})
                self.recorded_steps.append({"type": "key_press", "value": k_lower})
            return

        if not self.global_enabled or self.is_executing:
            return

        matched_macro = None
        with self.lock:
            for m in self.macros:
                if m.get("enabled") and m.get("trigger", "").lower() == k_lower:
                    matched_macro = dict(m)
                    break

        if matched_macro:
            self._trigger_macro(matched_macro)

    def _trigger_macro(self, macro: dict):
        mid = macro["id"]
        rmode = macro.get("repeat_mode", "once")

        if rmode == "toggle_loop":
            with self.lock:
                if mid in self.active_loops:
                    self.active_loops[mid].set()
                    del self.active_loops[mid]
                    return
                stop_ev = threading.Event()
                self.active_loops[mid] = stop_ev
            t = threading.Thread(target=self._run_macro_worker, args=(macro, stop_ev, True), daemon=True)
            t.start()
        else:
            stop_ev = threading.Event()
            t = threading.Thread(target=self._run_macro_worker, args=(macro, stop_ev, False), daemon=True)
            t.start()

    def _run_macro_worker(self, macro: dict, stop_ev: threading.Event, is_loop: bool):
        steps = macro.get("steps", [])
        repeat_count = max(1, int(macro.get("repeat_count", 1))) if macro.get("repeat_mode") == "repeat_n" else 1

        try:
            if is_loop:
                while not stop_ev.is_set() and self.global_enabled:
                    self._execute_steps(steps, stop_ev)
                    time.sleep(0.005)
            else:
                for _ in range(repeat_count):
                    if stop_ev.is_set() or not self.global_enabled:
                        break
                    self._execute_steps(steps, stop_ev)
        finally:
            if is_loop:
                with self.lock:
                    self.active_loops.pop(macro["id"], None)

    def _execute_steps(self, steps: list, stop_ev: threading.Event):
        user32 = ctypes.windll.user32
        for step in steps:
            if stop_ev.is_set():
                return
            stype = step.get("type")
            val = step.get("value")

            if stype == "delay":
                ms = max(1, int(val))
                time.sleep(ms / 1000.0)

            elif stype == "key_press":
                vk = VK_MAP.get(str(val).lower())
                if vk:
                    self.is_executing = True
                    user32.keybd_event(vk, 0, 0, 0)
                    time.sleep(0.01)
                    user32.keybd_event(vk, 0, 2, 0)
                    self.is_executing = False

            elif stype == "key_down":
                vk = VK_MAP.get(str(val).lower())
                if vk:
                    self.is_executing = True
                    user32.keybd_event(vk, 0, 0, 0)
                    self.is_executing = False

            elif stype == "key_up":
                vk = VK_MAP.get(str(val).lower())
                if vk:
                    self.is_executing = True
                    user32.keybd_event(vk, 0, 2, 0)
                    self.is_executing = False

            elif stype == "text":
                if self.kb_controller and val:
                    self.is_executing = True
                    self.kb_controller.type(str(val))
                    self.is_executing = False

            elif stype == "mouse_click":
                btn = str(val).lower()
                if btn == "right":
                    user32.mouse_event(0x0008, 0, 0, 0, 0)
                    time.sleep(0.01)
                    user32.mouse_event(0x0010, 0, 0, 0, 0)
                elif btn == "middle":
                    user32.mouse_event(0x0020, 0, 0, 0, 0)
                    time.sleep(0.01)
                    user32.mouse_event(0x0040, 0, 0, 0, 0)
                else:
                    user32.mouse_event(0x0002, 0, 0, 0, 0)
                    time.sleep(0.01)
                    user32.mouse_event(0x0004, 0, 0, 0, 0)
