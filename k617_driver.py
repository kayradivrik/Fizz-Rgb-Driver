import os
import json
import time
import math
import threading
import hid

VID = 0x258A
PID = 0x0049

# 61 keys layout definitions with physical matrix coordinates and positions in the 96-slot buffer
KEYS = [
    # Row 0
    {"id": "esc", "label": "ESC", "row": 0, "col": 0.0, "w": 1.0, "pos": 0, "vk": 0x1B},
    {"id": "1", "label": "1", "row": 0, "col": 1.0, "w": 1.0, "pos": 7, "vk": 0x31},
    {"id": "2", "label": "2", "row": 0, "col": 2.0, "w": 1.0, "pos": 13, "vk": 0x32},
    {"id": "3", "label": "3", "row": 0, "col": 3.0, "w": 1.0, "pos": 19, "vk": 0x33},
    {"id": "4", "label": "4", "row": 0, "col": 4.0, "w": 1.0, "pos": 25, "vk": 0x34},
    {"id": "5", "label": "5", "row": 0, "col": 5.0, "w": 1.0, "pos": 31, "vk": 0x35},
    {"id": "6", "label": "6", "row": 0, "col": 6.0, "w": 1.0, "pos": 37, "vk": 0x36},
    {"id": "7", "label": "7", "row": 0, "col": 7.0, "w": 1.0, "pos": 43, "vk": 0x37},
    {"id": "8", "label": "8", "row": 0, "col": 8.0, "w": 1.0, "pos": 49, "vk": 0x38},
    {"id": "9", "label": "9", "row": 0, "col": 9.0, "w": 1.0, "pos": 55, "vk": 0x39},
    {"id": "0", "label": "0", "row": 0, "col": 10.0, "w": 1.0, "pos": 61, "vk": 0x30},
    {"id": "minus", "label": "-", "row": 0, "col": 11.0, "w": 1.0, "pos": 67, "vk": 0xBD},
    {"id": "equal", "label": "=", "row": 0, "col": 12.0, "w": 1.0, "pos": 73, "vk": 0xBB},
    {"id": "backspace", "label": "Backspace", "row": 0, "col": 13.0, "w": 2.0, "pos": 79, "vk": 0x08},

    # Row 1
    {"id": "tab", "label": "Tab", "row": 1, "col": 0.0, "w": 1.5, "pos": 2, "vk": 0x09},
    {"id": "q", "label": "Q", "row": 1, "col": 1.5, "w": 1.0, "pos": 8, "vk": 0x51},
    {"id": "w", "label": "W", "row": 1, "col": 2.5, "w": 1.0, "pos": 14, "vk": 0x57},
    {"id": "e", "label": "E", "row": 1, "col": 3.5, "w": 1.0, "pos": 20, "vk": 0x45},
    {"id": "r", "label": "R", "row": 1, "col": 4.5, "w": 1.0, "pos": 26, "vk": 0x52},
    {"id": "t", "label": "T", "row": 1, "col": 5.5, "w": 1.0, "pos": 32, "vk": 0x54},
    {"id": "y", "label": "Y", "row": 1, "col": 6.5, "w": 1.0, "pos": 38, "vk": 0x59},
    {"id": "u", "label": "U", "row": 1, "col": 7.5, "w": 1.0, "pos": 44, "vk": 0x55},
    {"id": "i", "label": "I", "row": 1, "col": 8.5, "w": 1.0, "pos": 50, "vk": 0x49},
    {"id": "o", "label": "O", "row": 1, "col": 9.5, "w": 1.0, "pos": 56, "vk": 0x4F},
    {"id": "p", "label": "P", "row": 1, "col": 10.5, "w": 1.0, "pos": 62, "vk": 0x50},
    {"id": "lbracket", "label": "[", "row": 1, "col": 11.5, "w": 1.0, "pos": 68, "vk": 0xDB},
    {"id": "rbracket", "label": "]", "row": 1, "col": 12.5, "w": 1.0, "pos": 74, "vk": 0xDD},
    {"id": "backslash", "label": "\\", "row": 1, "col": 13.5, "w": 1.5, "pos": 80, "vk": 0xDC},

    # Row 2
    {"id": "caps", "label": "Caps", "row": 2, "col": 0.0, "w": 1.75, "pos": 3, "vk": 0x14},
    {"id": "a", "label": "A", "row": 2, "col": 1.75, "w": 1.0, "pos": 9, "vk": 0x41},
    {"id": "s", "label": "S", "row": 2, "col": 2.75, "w": 1.0, "pos": 15, "vk": 0x53},
    {"id": "d", "label": "D", "row": 2, "col": 3.75, "w": 1.0, "pos": 21, "vk": 0x44},
    {"id": "f", "label": "F", "row": 2, "col": 4.75, "w": 1.0, "pos": 27, "vk": 0x46},
    {"id": "g", "label": "G", "row": 2, "col": 5.75, "w": 1.0, "pos": 33, "vk": 0x47},
    {"id": "h", "label": "H", "row": 2, "col": 6.75, "w": 1.0, "pos": 39, "vk": 0x48},
    {"id": "j", "label": "J", "row": 2, "col": 7.75, "w": 1.0, "pos": 45, "vk": 0x4A},
    {"id": "k", "label": "K", "row": 2, "col": 8.75, "w": 1.0, "pos": 51, "vk": 0x4B},
    {"id": "l", "label": "L", "row": 2, "col": 9.75, "w": 1.0, "pos": 57, "vk": 0x4C},
    {"id": "semicolon", "label": ";", "row": 2, "col": 10.75, "w": 1.0, "pos": 63, "vk": 0xBA},
    {"id": "quote", "label": "'", "row": 2, "col": 11.75, "w": 1.0, "pos": 69, "vk": 0xDE},
    {"id": "enter", "label": "Enter", "row": 2, "col": 12.75, "w": 2.25, "pos": 81, "vk": 0x0D},

    # Row 3
    {"id": "lshift", "label": "Shift", "row": 3, "col": 0.0, "w": 2.25, "pos": 4, "vk": 0xA0},
    {"id": "z", "label": "Z", "row": 3, "col": 2.25, "w": 1.0, "pos": 10, "vk": 0x5A},
    {"id": "x", "label": "X", "row": 3, "col": 3.25, "w": 1.0, "pos": 16, "vk": 0x58},
    {"id": "c", "label": "C", "row": 3, "col": 4.25, "w": 1.0, "pos": 22, "vk": 0x43},
    {"id": "v", "label": "V", "row": 3, "col": 5.25, "w": 1.0, "pos": 28, "vk": 0x56},
    {"id": "b", "label": "B", "row": 3, "col": 6.25, "w": 1.0, "pos": 34, "vk": 0x42},
    {"id": "n", "label": "N", "row": 3, "col": 7.25, "w": 1.0, "pos": 40, "vk": 0x4E},
    {"id": "m", "label": "M", "row": 3, "col": 8.25, "w": 1.0, "pos": 46, "vk": 0x4D},
    {"id": "comma", "label": ",", "row": 3, "col": 9.25, "w": 1.0, "pos": 52, "vk": 0xBC},
    {"id": "period", "label": ".", "row": 3, "col": 10.25, "w": 1.0, "pos": 58, "vk": 0xBE},
    {"id": "slash", "label": "/", "row": 3, "col": 11.25, "w": 1.0, "pos": 64, "vk": 0xBF},
    {"id": "rshift", "label": "Shift", "row": 3, "col": 12.25, "w": 2.75, "pos": 82, "vk": 0xA1},

    # Row 4
    {"id": "lctrl", "label": "Ctrl", "row": 4, "col": 0.0, "w": 1.25, "pos": 5, "vk": 0xA2},
    {"id": "win", "label": "Win", "row": 4, "col": 1.25, "w": 1.25, "pos": 11, "vk": 0x5B},
    {"id": "lalt", "label": "Alt", "row": 4, "col": 2.5, "w": 1.25, "pos": 17, "vk": 0xA4},
    {"id": "space", "label": "Space", "row": 4, "col": 3.75, "w": 6.25, "pos": 35, "vk": 0x20},
    {"id": "ralt", "label": "Alt", "row": 4, "col": 10.0, "w": 1.25, "pos": 53, "vk": 0xA5},
    {"id": "fn", "label": "Fn", "row": 4, "col": 11.25, "w": 1.25, "pos": 59, "vk": 0xFF},
    {"id": "menu", "label": "Menu", "row": 4, "col": 12.5, "w": 1.25, "pos": 65, "vk": 0x5D},
    {"id": "rctrl", "label": "Ctrl", "row": 4, "col": 13.75, "w": 1.25, "pos": 71, "vk": 0xA3},
]

# Pre-compute geometric and buffer offsets once at startup for zero-overhead rendering
for _k in KEYS:
    _cx = _k["col"] + _k["w"] * 0.5
    _cy = _k["row"] + 0.5
    _dx = _cx - 7.5
    _dy = (_cy - 2.5) * 2.0
    _k["cx"] = _cx
    _k["cy"] = _cy
    _k["col_idx"] = max(0, min(13, int(round(_k["col"]))))
    _k["byte_off"] = 4 + _k["pos"] * 3
    _k["angle"] = math.atan2(_dy, _dx)
    _k["dist"] = math.hypot(_dx, _dy)

KEY_MAP = {k["id"]: k for k in KEYS}
KEY_OFFSET_MAP = {k["id"]: k["byte_off"] for k in KEYS}


class K617Driver:
    """
    Ultra-low-overhead USB HID driver for Redragon K617 Fizz (VID 0x258A, PID 0x0049).
    Uses a pre-allocated 382-byte buffer to eliminate per-frame heap allocations.
    """
    __slots__ = (
        "lock", "dev_perkey", "path_perkey", "path_col05", "path_col06",
        "connected", "_packet_buf", "_template_cache"
    )

    def __init__(self):
        self.lock = threading.Lock()
        self.dev_perkey = None
        self.path_perkey = None
        self.path_col05 = None
        self.path_col06 = None
        self.connected = False
        self._template_cache = {}

        # Pre-allocated 382-byte HID feature report buffer
        self._packet_buf = bytearray(382)
        self._packet_buf[0] = 0x08
        self._packet_buf[1] = 0x0A
        self._packet_buf[2] = 0x7A
        self._packet_buf[3] = 0x01

        self._find_devices()

    def _find_devices(self):
        try:
            devices = hid.enumerate(VID, PID)
            for d in devices:
                path = d["path"]
                path_str = path.decode("utf-8", errors="ignore")
                if "Col08" in path_str:
                    self.path_perkey = path
                elif "Col05" in path_str:
                    self.path_col05 = path
                elif "Col06" in path_str:
                    self.path_col06 = path

            if self.path_perkey:
                self.connected = True
                return True
        except Exception:
            pass
        self.connected = False
        return False

    def _ensure_perkey_open(self):
        if self.dev_perkey is not None:
            return True
        if not self.path_perkey:
            if not self._find_devices() or not self.path_perkey:
                return False
        try:
            self.dev_perkey = hid.device()
            self.dev_perkey.open_path(self.path_perkey)
            self.connected = True
            return True
        except Exception:
            self.dev_perkey = None
            self.connected = False
            return False

    def _close_perkey(self):
        if self.dev_perkey:
            try:
                self.dev_perkey.close()
            except Exception:
                pass
            self.dev_perkey = None

    def send_per_key_frame(self, key_colors):
        """
        Mutates the pre-allocated 382-byte buffer in place and sends it via HID Feature Report.
        """
        with self.lock:
            if not self._ensure_perkey_open():
                return False

            buf = self._packet_buf
            offset_map = KEY_OFFSET_MAP

            if isinstance(key_colors, dict):
                for kid, (r, g, b) in key_colors.items():
                    off = offset_map.get(kid)
                    if off is not None:
                        buf[off] = 0 if r < 0 else (255 if r > 255 else int(r))
                        buf[off + 1] = 0 if g < 0 else (255 if g > 255 else int(g))
                        buf[off + 2] = 0 if b < 0 else (255 if b > 255 else int(b))
            elif isinstance(key_colors, list):
                for pos, rgb in enumerate(key_colors):
                    if pos < 96 and rgb:
                        off = 4 + pos * 3
                        r, g, b = rgb
                        buf[off] = 0 if r < 0 else (255 if r > 255 else int(r))
                        buf[off + 1] = 0 if g < 0 else (255 if g > 255 else int(g))
                        buf[off + 2] = 0 if b < 0 else (255 if b > 255 else int(b))

            try:
                return self.dev_perkey.send_feature_report(buf) == 382
            except Exception:
                self._close_perkey()
                return False

    def send_solid_color(self, r, g, b):
        r_b = max(0, min(255, int(r)))
        g_b = max(0, min(255, int(g)))
        b_b = max(0, min(255, int(b)))
        with self.lock:
            if not self._ensure_perkey_open():
                return False
            buf = self._packet_buf
            for k in KEYS:
                off = k["byte_off"]
                buf[off] = r_b
                buf[off + 1] = g_b
                buf[off + 2] = b_b
            try:
                return self.dev_perkey.send_feature_report(buf) == 382
            except Exception:
                self._close_perkey()
                return False

    def send_firmware_effect(self, effect_name, color=None, speed=3, brightness=4):
        if effect_name not in self._template_cache:
            template_file = os.path.join(os.path.dirname(__file__), "templates", f"{effect_name}.json")
            if not os.path.exists(template_file):
                return False
            with open(template_file, "r") as f:
                self._template_cache[effect_name] = json.load(f)

        # Clone cached frames
        frames = [list(frame) for frame in self._template_cache[effect_name]]

        if color is not None:
            frames[1][29] = max(0, min(255, int(color[0])))
            frames[1][30] = max(0, min(255, int(color[1])))
            frames[1][31] = max(0, min(255, int(color[2])))

        s_nibble = max(1, min(4, int(speed)))
        b_nibble = max(1, min(4, int(brightness)))
        packed_sb = (s_nibble << 4) | b_nibble
        frames[4][69] = packed_sb
        frames[4][71] = packed_sb

        with self.lock:
            self._close_perkey()
            if not self.path_col05 or not self.path_col06:
                if not self._find_devices():
                    return False

            h05 = None
            h06 = None
            try:
                h05 = hid.device()
                h05.open_path(self.path_col05)
                h06 = hid.device()
                h06.open_path(self.path_col06)

                h05.send_feature_report(frames[0])
                time.sleep(0.005)
                for idx in range(1, len(frames)):
                    h06.send_feature_report(frames[idx])
                    time.sleep(0.005)
                return True
            except Exception:
                return False
            finally:
                if h05:
                    try: h05.close()
                    except Exception: pass
                if h06:
                    try: h06.close()
                    except Exception: pass

    def close(self):
        with self.lock:
            self._close_perkey()
