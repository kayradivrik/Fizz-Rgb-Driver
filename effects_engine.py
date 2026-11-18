import time
import math
import array
import random
import colorsys
import threading
from k617_driver import KEYS, KEY_MAP, K617Driver

try:
    import sounddevice as sd
    AUDIO_AVAILABLE = True
except Exception:
    AUDIO_AVAILABLE = False

try:
    from pynput import keyboard as pynput_keyboard
    PYNPUT_AVAILABLE = True
except Exception:
    PYNPUT_AVAILABLE = False

PYNPUT_KEY_MAP = {}
if PYNPUT_AVAILABLE:
    PYNPUT_KEY_MAP = {
        pynput_keyboard.Key.esc: "esc",
        pynput_keyboard.Key.tab: "tab",
        pynput_keyboard.Key.caps_lock: "caps",
        pynput_keyboard.Key.shift: "lshift",
        pynput_keyboard.Key.shift_r: "rshift",
        pynput_keyboard.Key.ctrl_l: "lctrl",
        pynput_keyboard.Key.ctrl_r: "rctrl",
        pynput_keyboard.Key.alt_l: "lalt",
        pynput_keyboard.Key.alt_gr: "ralt",
        pynput_keyboard.Key.cmd: "win",
        pynput_keyboard.Key.space: "space",
        pynput_keyboard.Key.enter: "enter",
        pynput_keyboard.Key.backspace: "backspace",
        pynput_keyboard.Key.menu: "menu",
        pynput_keyboard.Key.f1: "f1",
        pynput_keyboard.Key.f2: "f2",
        pynput_keyboard.Key.f3: "f3",
        pynput_keyboard.Key.f4: "f4",
        pynput_keyboard.Key.f5: "f5",
        pynput_keyboard.Key.f6: "f6",
        pynput_keyboard.Key.f7: "f7",
        pynput_keyboard.Key.f8: "f8",
        pynput_keyboard.Key.f9: "f9",
        pynput_keyboard.Key.f10: "f10",
        pynput_keyboard.Key.f11: "f11",
        pynput_keyboard.Key.f12: "f12",
    }


class EffectsEngine:
    """
    Zero-NumPy, low-RAM, pre-computed geometry RGB Effects Engine.
    """
    def __init__(self, driver: K617Driver, macro_engine=None, settings_mgr=None):
        self.driver = driver
        self.macro_engine = macro_engine
        self.settings_mgr = settings_mgr
        self.running = False
        self.thread = None
        self.lock = threading.Lock()

        # Engine state
        self.current_mode = "rainbow_wave"
        self.brightness = 1.0
        self.speed = 1.0
        self.primary_color = (0, 240, 255)
        self.secondary_color = (255, 0, 128)
        self.tertiary_color = (255, 184, 0)
        self.per_key_colors = {k["id"]: (0, 240, 255) for k in KEYS}

        self.direction = "right"
        self.audio_sensitivity = 1.5
        self.target_fps = 35
        self.eco_mode = True
        self.idle_timeout_minutes = 15

        # Dirty flag for static/per-key optimization (avoids redundant USB packets)
        self._static_dirty = True
        self._last_static_send = 0.0

        # Idle tracking
        self.last_activity_time = time.time()
        self.is_sleeping = False

        # Reactive typing state
        self.reactive_ripples = []
        self.reactive_singles = {}
        self.reactive_lines = []
        self.reactive_explosions = []

        self.pynput_listener = None
        if PYNPUT_AVAILABLE:
            try:
                self.pynput_listener = pynput_keyboard.Listener(on_press=self._on_key_press)
                self.pynput_listener.daemon = True
                self.pynput_listener.start()
            except Exception:
                pass

        # Audio visualizer state (Zero-NumPy using RawInputStream int16)
        self.audio_stream = None
        self.audio_levels = [0.0] * 14
        self.audio_peak = 0.0

        # Effect internal states
        self.matrix_drops = [{"row": random.uniform(-6, 0), "speed": random.uniform(2.5, 5.5)} for _ in range(14)]
        self.fire_grid = [[0.0 for _ in range(14)] for _ in range(5)]
        self.stars = {k["id"]: 0.0 for k in KEYS}
        self.glitch_timer = 0.0
        self.glitch_colors = {}
        self.police_timer = 0.0
        self.heartbeat_x = 0.0
        self.radar_angle = 0.0
        self.lava_blobs = [
            {"x": 3.5, "y": 1.5, "vx": 0.45, "vy": 0.32, "rad": 2.3},
            {"x": 7.5, "y": 2.5, "vx": -0.35, "vy": 0.42, "rad": 2.6},
            {"x": 11.5, "y": 2.0, "vx": 0.50, "vy": -0.25, "rad": 2.0},
        ]

        self.last_frame_colors = {k["id"]: (0, 240, 255) for k in KEYS}
        self.actual_fps = 35.0

        self.start()

    def start(self):
        if not self.running:
            self.running = True
            self.thread = threading.Thread(target=self._render_loop, daemon=True)
            self.thread.start()

    def stop(self):
        self.running = False
        if self.thread and self.thread.is_alive():
            self.thread.join(timeout=1.0)
        self._stop_audio()

    def _on_key_press(self, key):
        self.last_activity_time = time.time()
        if self.is_sleeping:
            self.is_sleeping = False
            self._static_dirty = True

        kid = None
        if isinstance(key, pynput_keyboard.Key):
            kid = PYNPUT_KEY_MAP.get(key)
        elif hasattr(key, "char") and key.char:
            char = key.char.lower()
            if char in KEY_MAP or char in "1234567890":
                kid = char
            elif char == "-": kid = "minus"
            elif char == "=": kid = "equal"
            elif char == "[": kid = "lbracket"
            elif char == "]": kid = "rbracket"
            elif char == "\\": kid = "backslash"
            elif char == ";": kid = "semicolon"
            elif char == "'": kid = "quote"
            elif char == ",": kid = "comma"
            elif char == ".": kid = "period"
            elif char == "/": kid = "slash"

        # Forward to Macro Engine
        if kid and self.macro_engine:
            self.macro_engine.handle_key_event(kid)

        if kid and kid in KEY_MAP:
            k = KEY_MAP[kid]
            cx = k["cx"]
            cy = k["cy"]
            with self.lock:
                mode = self.current_mode
                if mode == "reactive_ripple":
                    self.reactive_ripples.append({"col": cx, "row": cy, "radius": 0.0, "life": 1.0})
                    if len(self.reactive_ripples) > 12:
                        self.reactive_ripples.pop(0)
                elif mode == "reactive_single":
                    self.reactive_singles[kid] = 1.0
                elif mode == "reactive_line":
                    self.reactive_lines.append({"row": k["row"], "col": cx, "life": 1.0})
                    if len(self.reactive_lines) > 6:
                        self.reactive_lines.pop(0)
                elif mode == "reactive_explosion":
                    for _ in range(6):
                        angle = random.uniform(0, math.pi * 2)
                        spd = random.uniform(2.0, 6.0)
                        self.reactive_explosions.append({
                            "x": cx, "y": cy,
                            "vx": math.cos(angle) * spd,
                            "vy": math.sin(angle) * spd,
                            "life": 1.0,
                            "color": random.choice([self.primary_color, self.secondary_color, self.tertiary_color])
                        })
                    if len(self.reactive_explosions) > 36:
                        self.reactive_explosions = self.reactive_explosions[-36:]

    def _start_audio(self):
        if not AUDIO_AVAILABLE or self.audio_stream is not None:
            return
        try:
            def raw_audio_callback(indata, frames, time_info, status):
                if status:
                    return
                # Zero-NumPy fast int16 PCM energy analysis
                samples = array.array("h")
                samples.frombytes(bytes(indata))
                n = len(samples)
                if n < 14:
                    return
                chunk = n // 14
                sens = self.audio_sensitivity
                total_abs = 0
                for i in range(14):
                    start_i = i * chunk
                    sub = samples[start_i:start_i + chunk]
                    avg_abs = sum(abs(x) for x in sub) / (chunk * 32768.0)
                    total_abs += avg_abs
                    val = min(1.0, math.log10(1.0 + avg_abs * 45.0) * sens)
                    self.audio_levels[i] = max(val, self.audio_levels[i] * 0.72)

                peak = min(1.0, (total_abs / 14.0) * 8.0 * sens)
                self.audio_peak = max(self.audio_peak * 0.82, peak)

            self.audio_stream = sd.RawInputStream(
                channels=1,
                samplerate=44100,
                blocksize=1024,
                dtype="int16",
                callback=raw_audio_callback
            )
            self.audio_stream.start()
        except Exception:
            self.audio_stream = None

    def _stop_audio(self):
        if self.audio_stream:
            try:
                self.audio_stream.stop()
                self.audio_stream.close()
            except Exception:
                pass
            self.audio_stream = None

    def set_mode(self, mode, **kwargs):
        with self.lock:
            self.current_mode = mode
            self._static_dirty = True
            if "brightness" in kwargs: self.brightness = float(kwargs["brightness"])
            if "speed" in kwargs: self.speed = float(kwargs["speed"])
            if "primary_color" in kwargs: self.primary_color = tuple(kwargs["primary_color"])
            if "secondary_color" in kwargs: self.secondary_color = tuple(kwargs["secondary_color"])
            if "tertiary_color" in kwargs: self.tertiary_color = tuple(kwargs["tertiary_color"])
            if "direction" in kwargs: self.direction = kwargs["direction"]
            if "audio_sensitivity" in kwargs: self.audio_sensitivity = float(kwargs["audio_sensitivity"])
            if "per_key_colors" in kwargs: self.per_key_colors = kwargs["per_key_colors"]

        if mode.startswith("audio_"):
            self._start_audio()
        else:
            self._stop_audio()

    def mark_dirty(self):
        self._static_dirty = True

    def set_key_color(self, key_id, rgb):
        with self.lock:
            self.per_key_colors[key_id] = tuple(rgb)
            self._static_dirty = True

    def _render_loop(self):
        t0 = time.time()
        frame_colors = {k["id"]: (0, 240, 255) for k in KEYS}

        while self.running:
            start_time = time.time()
            now = start_time - t0

            fps = max(10, min(60, int(self.target_fps)))
            frame_interval = 1.0 / fps

            # Check Idle Sleep Timeout
            if self.idle_timeout_minutes > 0:
                idle_sec = start_time - self.last_activity_time
                if idle_sec > self.idle_timeout_minutes * 60:
                    if not self.is_sleeping:
                        self.is_sleeping = True
                        self.driver.send_solid_color(0, 0, 0)
                    time.sleep(0.25)
                    continue

            with self.lock:
                mode = self.current_mode
                b = self.brightness
                spd = self.speed
                p_col = self.primary_color
                s_col = self.secondary_color
                t_col = self.tertiary_color
                direction = self.direction

            if mode == "firmware":
                time.sleep(0.15)
                continue

            # Eco optimization: Static and Per-Key modes only send HID frames when modified or keepalive
            if mode in ("static", "per_key"):
                if not self._static_dirty and (start_time - self._last_static_send) < 2.0:
                    time.sleep(0.05)
                    continue

                if mode == "static":
                    r, g, bv = int(p_col[0] * b), int(p_col[1] * b), int(p_col[2] * b)
                    for k in KEYS:
                        frame_colors[k["id"]] = (r, g, bv)
                else:
                    for kid, rgb in self.per_key_colors.items():
                        frame_colors[kid] = (int(rgb[0] * b), int(rgb[1] * b), int(rgb[2] * b))

                self.last_frame_colors = dict(frame_colors)
                self.driver.send_per_key_frame(frame_colors)
                self._static_dirty = False
                self._last_static_send = start_time
                time.sleep(0.04)
                continue

            # ---------------- DYNAMIC MODES ----------------
            if mode == "rainbow_wave":
                t_shift = now * 0.3 * spd
                for k in KEYS:
                    if direction == "left":
                        coord = -k["cx"] * 0.07
                    elif direction == "up":
                        coord = -k["cy"] * 0.2
                    elif direction == "down":
                        coord = k["cy"] * 0.2
                    else:
                        coord = k["cx"] * 0.06 + k["cy"] * 0.03
                    hue = (coord - t_shift) % 1.0
                    rgb = colorsys.hsv_to_rgb(hue, 1.0, b)
                    frame_colors[k["id"]] = (int(rgb[0] * 255), int(rgb[1] * 255), int(rgb[2] * 255))

            elif mode == "rainbow_spiral":
                t_shift = now * 0.4 * spd
                inv_2pi = 1.0 / (2.0 * math.pi)
                for k in KEYS:
                    hue = ((k["angle"] * inv_2pi + k["dist"] * 0.08) - t_shift) % 1.0
                    rgb = colorsys.hsv_to_rgb(hue, 1.0, b)
                    frame_colors[k["id"]] = (int(rgb[0] * 255), int(rgb[1] * 255), int(rgb[2] * 255))

            elif mode == "color_cycle":
                hue = (now * 0.2 * spd) % 1.0
                rgb = colorsys.hsv_to_rgb(hue, 1.0, b)
                c_tuple = (int(rgb[0] * 255), int(rgb[1] * 255), int(rgb[2] * 255))
                for k in KEYS:
                    frame_colors[k["id"]] = c_tuple

            elif mode == "gradient_sweep":
                phase = (math.sin(now * 1.5 * spd) + 1.0) * 0.5
                for k in KEYS:
                    t = (k["cx"] / 15.0 + phase) % 1.0
                    if t < 0.5:
                        f = t * 2.0
                        r = int((p_col[0] * (1.0 - f) + s_col[0] * f) * b)
                        g = int((p_col[1] * (1.0 - f) + s_col[1] * f) * b)
                        bv = int((p_col[2] * (1.0 - f) + s_col[2] * f) * b)
                    else:
                        f = (t - 0.5) * 2.0
                        r = int((s_col[0] * (1.0 - f) + t_col[0] * f) * b)
                        g = int((s_col[1] * (1.0 - f) + t_col[1] * f) * b)
                        bv = int((s_col[2] * (1.0 - f) + t_col[2] * f) * b)
                    frame_colors[k["id"]] = (r, g, bv)

            elif mode == "breathing":
                phase = (math.sin(now * 2.0 * spd) + 1.0) * 0.5
                inv_p = 1.0 - phase
                c_tuple = (
                    int((p_col[0] * phase + s_col[0] * inv_p) * b),
                    int((p_col[1] * phase + s_col[1] * inv_p) * b),
                    int((p_col[2] * phase + s_col[2] * inv_p) * b)
                )
                for k in KEYS:
                    frame_colors[k["id"]] = c_tuple

            elif mode == "reactive_ripple":
                base_c = (int(s_col[0] * 0.06 * b), int(s_col[1] * 0.06 * b), int(s_col[2] * 0.06 * b))
                for k in KEYS:
                    frame_colors[k["id"]] = base_c

                surviving = []
                for rip in self.reactive_ripples:
                    rip["radius"] += frame_interval * 9.0 * spd
                    rip["life"] -= frame_interval * 1.6 * spd
                    if rip["life"] > 0.0:
                        surviving.append(rip)
                self.reactive_ripples = surviving

                for rip in surviving:
                    rx, ry, rad, life = rip["col"], rip["row"], rip["radius"], rip["life"]
                    for k in KEYS:
                        dist = math.hypot(k["cx"] - rx, (k["cy"] - ry) * 1.6)
                        diff = abs(dist - rad)
                        if diff < 1.3:
                            intensity = (1.0 - diff / 1.3) * life * b
                            cr, cg, cb = frame_colors[k["id"]]
                            frame_colors[k["id"]] = (
                                min(255, int(cr + p_col[0] * intensity)),
                                min(255, int(cg + p_col[1] * intensity)),
                                min(255, int(cb + p_col[2] * intensity))
                            )

            elif mode == "reactive_single":
                base_c = (int(s_col[0] * 0.05 * b), int(s_col[1] * 0.05 * b), int(s_col[2] * 0.05 * b))
                for k in KEYS:
                    kid = k["id"]
                    life = self.reactive_singles.get(kid, 0.0)
                    if life > 0.0:
                        life = max(0.0, life - frame_interval * 2.0 * spd)
                        self.reactive_singles[kid] = life
                        intensity = life * b
                        frame_colors[kid] = (int(p_col[0] * intensity), int(p_col[1] * intensity), int(p_col[2] * intensity))
                    else:
                        frame_colors[kid] = base_c

            elif mode == "reactive_line":
                base_c = (int(s_col[0] * 0.05 * b), int(s_col[1] * 0.05 * b), int(s_col[2] * 0.05 * b))
                for k in KEYS:
                    frame_colors[k["id"]] = base_c

                surv_lines = []
                for l in self.reactive_lines:
                    l["life"] -= frame_interval * 2.2 * spd
                    if l["life"] > 0.0:
                        surv_lines.append(l)
                self.reactive_lines = surv_lines

                for l in surv_lines:
                    for k in KEYS:
                        row_dist = abs(k["row"] - l["row"])
                        col_dist = abs(k["cx"] - l["col"])
                        intensity = 0.0
                        if row_dist < 0.5:
                            intensity = max(intensity, (1.0 - col_dist / 15.0) * l["life"] * b)
                        if col_dist < 0.8:
                            intensity = max(intensity, (1.0 - row_dist / 5.0) * l["life"] * b)
                        if intensity > 0.0:
                            cr, cg, cb = frame_colors[k["id"]]
                            frame_colors[k["id"]] = (
                                min(255, int(cr + p_col[0] * intensity)),
                                min(255, int(cg + p_col[1] * intensity)),
                                min(255, int(cb + p_col[2] * intensity))
                            )

            elif mode == "reactive_explosion":
                base_c = (int(s_col[0] * 0.05 * b), int(s_col[1] * 0.05 * b), int(s_col[2] * 0.05 * b))
                for k in KEYS:
                    frame_colors[k["id"]] = base_c

                surv_exp = []
                for p in self.reactive_explosions:
                    p["x"] += p["vx"] * frame_interval * spd
                    p["y"] += p["vy"] * frame_interval * spd
                    p["life"] -= frame_interval * 2.0 * spd
                    if p["life"] > 0.0 and 0 <= p["x"] <= 15 and 0 <= p["y"] <= 5:
                        surv_exp.append(p)
                self.reactive_explosions = surv_exp

                for p in surv_exp:
                    px, py, plife, col = p["x"], p["y"], p["life"], p["color"]
                    for k in KEYS:
                        dist = math.hypot(k["cx"] - px, (k["cy"] - py) * 1.5)
                        if dist < 1.0:
                            fade = (1.0 - dist) * plife * b
                            cr, cg, cb = frame_colors[k["id"]]
                            frame_colors[k["id"]] = (
                                min(255, int(cr + col[0] * fade)),
                                min(255, int(cg + col[1] * fade)),
                                min(255, int(cb + col[2] * fade))
                            )

            elif mode == "audio_bars":
                for k in KEYS:
                    lvl = self.audio_levels[k["col_idx"]]
                    thresh = (4 - k["row"]) * 0.2
                    if lvl >= thresh:
                        norm_h = (4 - k["row"]) * 0.25
                        if norm_h < 0.5:
                            r = int(norm_h * 2.0 * 255 * b)
                            g = int(255 * b)
                            bv = int((1.0 - norm_h * 2.0) * 200 * b)
                        else:
                            r = int(255 * b)
                            g = int((1.0 - (norm_h - 0.5) * 2.0) * 255 * b)
                            bv = 0
                        frame_colors[k["id"]] = (r, g, bv)
                    else:
                        frame_colors[k["id"]] = (0, int(15 * b), int(30 * b))

            elif mode == "audio_pulse":
                intensity = self.audio_peak * b
                hue = (now * 0.15) % 1.0
                rgb = colorsys.hsv_to_rgb(hue, 1.0, intensity)
                c_tuple = (
                    min(255, int(rgb[0] * 255 + p_col[0] * 0.08 * b)),
                    min(255, int(rgb[1] * 255 + p_col[1] * 0.08 * b)),
                    min(255, int(rgb[2] * 255 + p_col[2] * 0.08 * b))
                )
                for k in KEYS:
                    frame_colors[k["id"]] = c_tuple

            elif mode == "audio_wave":
                rad = self.audio_peak * 8.0
                base_c = (int(s_col[0] * 0.06 * b), int(s_col[1] * 0.06 * b), int(s_col[2] * 0.06 * b))
                for k in KEYS:
                    diff = abs(k["dist"] - rad)
                    if diff < 1.8:
                        val = (1.0 - diff / 1.8) * b
                        frame_colors[k["id"]] = (int(p_col[0] * val), int(p_col[1] * val), int(p_col[2] * val))
                    else:
                        frame_colors[k["id"]] = base_c

            elif mode == "matrix":
                for i in range(14):
                    drop = self.matrix_drops[i]
                    drop["row"] += frame_interval * drop["speed"] * spd
                    if drop["row"] > 6.0:
                        drop["row"] = random.uniform(-4, 0)
                        drop["speed"] = random.uniform(2.5, 6.0)

                for k in KEYS:
                    drop = self.matrix_drops[k["col_idx"]]
                    dist = drop["row"] - k["row"]
                    if 0.0 <= dist < 0.7:
                        frame_colors[k["id"]] = (int(220 * b), int(255 * b), int(220 * b))
                    elif 0.7 <= dist < 3.2:
                        fade = 1.0 - (dist - 0.7) / 2.5
                        frame_colors[k["id"]] = (0, int(255 * fade * b), int(45 * fade * b))
                    else:
                        frame_colors[k["id"]] = (0, 0, 0)

            elif mode == "fire":
                for x in range(14):
                    self.fire_grid[4][x] = random.uniform(0.7, 1.0)
                for y in range(3, -1, -1):
                    for x in range(14):
                        left = self.fire_grid[y + 1][(x - 1) % 14]
                        mid = self.fire_grid[y + 1][x]
                        right = self.fire_grid[y + 1][(x + 1) % 14]
                        heat = (left + mid * 2.0 + right) * 0.25 - random.uniform(0.12, 0.24) / max(0.3, spd)
                        self.fire_grid[y][x] = max(0.0, heat)

                for k in KEYS:
                    heat = self.fire_grid[k["row"]][k["col_idx"]]
                    if heat < 0.3:
                        r, g, bv = int(heat / 0.3 * 180 * b), 0, 0
                    elif heat < 0.7:
                        f = (heat - 0.3) / 0.4
                        r, g, bv = int(255 * b), int(f * 180 * b), 0
                    else:
                        f = (heat - 0.7) / 0.3
                        r, g, bv = int(255 * b), int((180 + f * 75) * b), int(f * 140 * b)
                    frame_colors[k["id"]] = (r, g, bv)

            elif mode == "cyberpunk_glitch":
                self.glitch_timer += frame_interval * spd
                if self.glitch_timer > 0.12:
                    self.glitch_timer = 0.0
                    palette = [(0, 240, 255), (255, 0, 128), (255, 230, 0), (10, 15, 25)]
                    for k in KEYS:
                        if random.random() < 0.25:
                            self.glitch_colors[k["id"]] = random.choice(palette)
                for k in KEYS:
                    c = self.glitch_colors.get(k["id"], (0, 240, 255))
                    frame_colors[k["id"]] = (int(c[0] * b), int(c[1] * b), int(c[2] * b))

            elif mode == "police_lights":
                self.police_timer += frame_interval * 7.0 * spd
                side = int(self.police_timer) % 4
                for k in KEYS:
                    is_left = k["cx"] < 7.5
                    if side in (0, 1) and is_left:
                        frame_colors[k["id"]] = (int(255 * b), 0, 0)
                    elif side in (2, 3) and not is_left:
                        frame_colors[k["id"]] = (0, 0, int(255 * b))
                    else:
                        frame_colors[k["id"]] = (0, 0, 0)

            elif mode == "aurora":
                for k in KEYS:
                    wave1 = math.sin(k["cx"] * 0.35 + now * 1.5 * spd)
                    wave2 = math.cos(k["cy"] * 0.8 - now * 1.2 * spd)
                    t = (wave1 + wave2 + 2.0) * 0.25
                    if t < 0.5:
                        f = t * 2.0
                        r, g, bv = 0, int((220 + 35 * f) * b), int(120 * f * b)
                    else:
                        f = (t - 0.5) * 2.0
                        r, g, bv = int(140 * f * b), int(255 * (1.0 - f) * b), int((120 + 135 * f) * b)
                    frame_colors[k["id"]] = (r, g, bv)

            elif mode == "ocean_tide":
                for k in KEYS:
                    wave = math.sin(k["cx"] * 0.4 - now * 2.2 * spd)
                    t = (wave + 1.0) * 0.5
                    if t < 0.7:
                        f = t / 0.7
                        r, g, bv = int(5 * f * b), int((40 + 100 * f) * b), int((140 + 115 * f) * b)
                    else:
                        f = (t - 0.7) / 0.3
                        r, g, bv = int((5 + 230 * f) * b), int((140 + 115 * f) * b), int(255 * b)
                    frame_colors[k["id"]] = (r, g, bv)

            elif mode == "starlight":
                base_c = (int(p_col[0] * 0.05 * b), int(p_col[1] * 0.05 * b), int(p_col[2] * 0.05 * b))
                for k in KEYS:
                    kid = k["id"]
                    curr = self.stars[kid]
                    if curr <= 0.0 and random.random() < 0.025 * spd:
                        self.stars[kid] = 1.0
                    elif curr > 0.0:
                        self.stars[kid] = max(0.0, curr - frame_interval * 1.6 * spd)
                    st = self.stars[kid]
                    if st > 0.0:
                        frame_colors[kid] = (
                            int((p_col[0] * st + t_col[0] * (1.0 - st)) * b * st),
                            int((p_col[1] * st + t_col[1] * (1.0 - st)) * b * st),
                            int((p_col[2] * st + t_col[2] * (1.0 - st)) * b * st)
                        )
                    else:
                        frame_colors[kid] = base_c

            elif mode == "heartbeat":
                self.heartbeat_x = (self.heartbeat_x + frame_interval * 14.0 * spd) % 17.0
                for k in KEYS:
                    dist = self.heartbeat_x - k["cx"]
                    if 0.0 <= dist < 3.0:
                        fade = 1.0 - dist / 3.0
                        frame_colors[k["id"]] = (int(255 * fade * b), int(20 * fade * b), int(60 * fade * b))
                    else:
                        frame_colors[k["id"]] = (int(30 * b), int(5 * b), int(10 * b))

            elif mode == "radar":
                self.radar_angle = (self.radar_angle + frame_interval * 3.5 * spd) % (math.pi * 2)
                for k in KEYS:
                    diff = (self.radar_angle - k["angle"]) % (math.pi * 2)
                    if diff < 1.2:
                        fade = (1.0 - diff / 1.2) * b
                        frame_colors[k["id"]] = (0, int(255 * fade), int(100 * fade))
                    else:
                        frame_colors[k["id"]] = (0, int(15 * b), int(10 * b))

            elif mode == "lava_lamp":
                for blob in self.lava_blobs:
                    blob["x"] += blob["vx"] * frame_interval * spd
                    blob["y"] += blob["vy"] * frame_interval * spd
                    if blob["x"] < 1.0 or blob["x"] > 14.0: blob["vx"] *= -1
                    if blob["y"] < 0.5 or blob["y"] > 4.5: blob["vy"] *= -1

                for k in KEYS:
                    total_field = 0.0
                    for blob in self.lava_blobs:
                        d = math.hypot(k["cx"] - blob["x"], (k["cy"] - blob["y"]) * 1.6)
                        if d < blob["rad"]:
                            total_field += (1.0 - d / blob["rad"])
                    t = min(1.0, total_field)
                    frame_colors[k["id"]] = (
                        int((p_col[0] * t + s_col[0] * (1.0 - t)) * t * b),
                        int((p_col[1] * t + s_col[1] * (1.0 - t)) * t * b),
                        int((p_col[2] * t + s_col[2] * (1.0 - t)) * t * b)
                    )

            self.last_frame_colors = dict(frame_colors)
            self.driver.send_per_key_frame(frame_colors)

            elapsed = time.time() - start_time
            sleep_time = max(0.002, frame_interval - elapsed)
            time.sleep(sleep_time)
            self.actual_fps = round(1.0 / max(0.001, time.time() - start_time), 1)
