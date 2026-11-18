import os
import sys
import json
import time
import socket
import threading
import webbrowser
import mimetypes
from urllib.parse import urlparse, parse_qs
from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler

from k617_driver import K617Driver, KEYS
from macro_engine import MacroEngine
from settings_manager import SettingsManager, trim_process_memory, get_process_memory_mb
from effects_engine import EffectsEngine

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
WEB_DIR = os.path.join(BASE_DIR, "web")
PROFILES_DIR = os.path.join(BASE_DIR, "profiles")
os.makedirs(PROFILES_DIR, exist_ok=True)

settings_mgr = SettingsManager()
macro_engine = MacroEngine()
driver = K617Driver()
engine = EffectsEngine(driver, macro_engine=macro_engine, settings_mgr=settings_mgr)

# Apply saved settings on startup
s_data = settings_mgr.data
engine.target_fps = s_data.get("target_fps", 35)
engine.eco_mode = s_data.get("eco_mode", True)
engine.idle_timeout_minutes = s_data.get("idle_timeout_minutes", 15)
macro_engine.global_enabled = s_data.get("macros_enabled", True)
engine.set_mode(
    s_data.get("last_mode", "rainbow_wave"),
    brightness=s_data.get("brightness", 1.0),
    speed=s_data.get("speed", 1.0),
    primary_color=s_data.get("primary_color", [0, 240, 255]),
    secondary_color=s_data.get("secondary_color", [255, 0, 128]),
    tertiary_color=s_data.get("tertiary_color", [255, 184, 0]),
    direction=s_data.get("direction", "right"),
    audio_sensitivity=s_data.get("audio_sensitivity", 1.5),
)


class StudioRequestHandler(BaseHTTPRequestHandler):
    """Ultra-lightweight stdlib HTTP handler (replaces FastAPI/Uvicorn to save ~30MB RAM)."""

    def log_message(self, format, *args):
        pass  # Silence console spam for maximum performance

    def _send_json(self, payload, status=200):
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def _read_json(self):
        length = int(self.headers.get("Content-Length", 0))
        if length <= 0:
            return {}
        raw = self.rfile.read(length)
        return json.loads(raw.decode("utf-8"))

    def do_GET(self):
        parsed = urlparse(self.path)
        path = parsed.path

        if path == "/api/status":
            self._send_json({
                "connected": driver.connected,
                "vid": "0x258A",
                "pid": "0x0049",
                "mode": engine.current_mode,
                "brightness": engine.brightness,
                "speed": engine.speed,
                "fps": engine.actual_fps,
                "ram_mb": get_process_memory_mb(),
                "macros_enabled": macro_engine.global_enabled,
                "settings": settings_mgr.data
            })
            return

        if path == "/api/get_live_colors":
            self._send_json({
                "colors": engine.last_frame_colors,
                "fps": engine.actual_fps,
                "ram_mb": get_process_memory_mb()
            })
            return

        if path == "/api/load_profile":
            qs = parse_qs(parsed.query)
            name = qs.get("name", [""])[0]
            safe_name = os.path.basename(name)
            if not safe_name.endswith(".json"):
                safe_name += ".json"
            p_path = os.path.join(PROFILES_DIR, safe_name)
            if os.path.exists(p_path):
                with open(p_path, "r", encoding="utf-8") as f:
                    data = json.load(f)
                for kid, rgb in data.get("colors", {}).items():
                    engine.set_key_color(kid, tuple(rgb))
                engine.set_mode("per_key")
                self._send_json(data)
            else:
                self._send_json({"error": "Profile not found"}, status=404)
            return

        if path == "/api/macros":
            self._send_json({
                "macros": macro_engine.get_macros(),
                "global_enabled": macro_engine.global_enabled,
                "recording": macro_engine.recording
            })
            return

        if path == "/api/settings":
            self._send_json({
                "settings": settings_mgr.data,
                "ram_mb": get_process_memory_mb(),
                "fps": engine.actual_fps
            })
            return

        # Serve static files from WEB_DIR
        if path == "/" or path == "":
            path = "/index.html"
        file_path = os.path.normpath(os.path.join(WEB_DIR, path.lstrip("/")))
        if file_path.startswith(WEB_DIR) and os.path.isfile(file_path):
            mime, _ = mimetypes.guess_type(file_path)
            with open(file_path, "rb") as f:
                content = f.read()
            self.send_response(200)
            self.send_header("Content-Type", mime or "application/octet-stream")
            self.send_header("Content-Length", str(len(content)))
            self.end_headers()
            self.wfile.write(content)
        else:
            self.send_response(404)
            self.end_headers()

    def do_POST(self):
        parsed = urlparse(self.path)
        path = parsed.path
        req = self._read_json()

        if path == "/api/set_mode":
            mode = req.get("mode", "rainbow_wave")
            kwargs = {}
            for k in ("brightness", "speed", "primary_color", "secondary_color", "tertiary_color", "direction", "audio_sensitivity"):
                if k in req:
                    kwargs[k] = req[k]
            engine.set_mode(mode, **kwargs)
            settings_mgr.update({"last_mode": mode, **kwargs})
            self._send_json({"status": "ok", "mode": mode})
            return

        if path == "/api/update_params":
            if "brightness" in req: engine.brightness = float(req["brightness"])
            if "speed" in req: engine.speed = float(req["speed"])
            if "primary_color" in req: engine.primary_color = tuple(req["primary_color"])
            if "secondary_color" in req: engine.secondary_color = tuple(req["secondary_color"])
            if "tertiary_color" in req: engine.tertiary_color = tuple(req["tertiary_color"])
            if "direction" in req: engine.direction = req["direction"]
            if "audio_sensitivity" in req: engine.audio_sensitivity = float(req["audio_sensitivity"])
            engine.mark_dirty()
            settings_mgr.update(req)
            self._send_json({"status": "ok"})
            return

        if path == "/api/paint_key":
            if engine.current_mode != "per_key":
                engine.set_mode("per_key")
            engine.set_key_color(req.get("key_id", "esc"), tuple(req.get("rgb", [0, 240, 255])))
            self._send_json({"status": "ok"})
            return

        if path == "/api/paint_batch":
            if engine.current_mode != "per_key":
                engine.set_mode("per_key")
            for kid, rgb in req.get("keys", {}).items():
                engine.set_key_color(kid, tuple(rgb))
            self._send_json({"status": "ok"})
            return

        if path == "/api/fill_all":
            engine.set_mode("per_key")
            rgb = tuple(req.get("rgb", [0, 0, 0]))
            for k in KEYS:
                engine.set_key_color(k["id"], rgb)
            self._send_json({"status": "ok"})
            return

        if path == "/api/firmware_effect":
            engine.set_mode("firmware")
            res = driver.send_firmware_effect(
                req.get("effect", "fw-rainbow"),
                color=tuple(req.get("color", [255, 0, 0])),
                speed=int(req.get("speed", 3)),
                brightness=int(req.get("brightness", 4))
            )
            self._send_json({"status": "ok" if res else "error"})
            return

        if path == "/api/save_profile":
            name = req.get("name", "Ozel_Profil")
            safe_name = "".join(c for c in name if c.isalnum() or c in ("_", "-")) + ".json"
            p_path = os.path.join(PROFILES_DIR, safe_name)
            with open(p_path, "w", encoding="utf-8") as f:
                json.dump({"name": name, "colors": req.get("colors", {})}, f, indent=2)
            self._send_json({"status": "ok", "file": safe_name})
            return

        # Macro Endpoints
        if path == "/api/macro_save":
            m = macro_engine.add_or_update_macro(req)
            self._send_json({"status": "ok", "macro": m, "macros": macro_engine.get_macros()})
            return

        if path == "/api/macro_delete":
            macro_engine.delete_macro(req.get("id", ""))
            self._send_json({"status": "ok", "macros": macro_engine.get_macros()})
            return

        if path == "/api/macro_toggle":
            if "global_enabled" in req:
                macro_engine.global_enabled = bool(req["global_enabled"])
                settings_mgr.update({"macros_enabled": macro_engine.global_enabled})
            if "id" in req and "enabled" in req:
                macro_engine.toggle_macro(req["id"], bool(req["enabled"]))
            self._send_json({"status": "ok", "macros": macro_engine.get_macros(), "global_enabled": macro_engine.global_enabled})
            return

        if path == "/api/macro_record_start":
            macro_engine.start_recording()
            self._send_json({"status": "ok", "recording": True})
            return

        if path == "/api/macro_record_stop":
            steps = macro_engine.stop_recording()
            self._send_json({"status": "ok", "recording": False, "steps": steps})
            return

        # Settings & Memory Optimization Endpoints
        if path == "/api/settings":
            settings_mgr.update(req)
            if "target_fps" in req: engine.target_fps = int(req["target_fps"])
            if "eco_mode" in req: engine.eco_mode = bool(req["eco_mode"])
            if "idle_timeout_minutes" in req: engine.idle_timeout_minutes = int(req["idle_timeout_minutes"])
            if "macros_enabled" in req: macro_engine.global_enabled = bool(req["macros_enabled"])
            if engine.eco_mode:
                trim_process_memory()
            self._send_json({"status": "ok", "settings": settings_mgr.data, "ram_mb": get_process_memory_mb()})
            return

        if path == "/api/trim_memory":
            trim_process_memory()
            self._send_json({"status": "ok", "ram_mb": get_process_memory_mb()})
            return

        self._send_json({"error": "Not found"}, status=404)


def find_free_port(start_port=54321):
    port = start_port
    while port < 65535:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            if s.connect_ex(("127.0.0.1", port)) != 0:
                return port
            port += 1
    return 54321


def start_server(port):
    server = ThreadingHTTPServer(("127.0.0.1", port), StudioRequestHandler)
    server.serve_forever()


def periodic_memory_trimmer():
    while True:
        time.sleep(10)
        if engine.eco_mode:
            trim_process_memory()


def _post_window_launch_optimize():
    for delay in (0.6, 1.5, 3.0):
        time.sleep(delay)
        trim_process_memory()


def main():
    trim_process_memory()
    port = find_free_port(54321)
    url = f"http://127.0.0.1:{port}"

    server_thread = threading.Thread(target=start_server, args=(port,), daemon=True)
    server_thread.start()

    trim_thread = threading.Thread(target=periodic_memory_trimmer, daemon=True)
    trim_thread.start()

    time.sleep(0.2)
    trim_process_memory()

    use_browser = "--browser" in sys.argv
    if not use_browser:
        try:
            import webview
            window = webview.create_window(
                "Redragon K617 Fizz — Pro RGB & Macro Studio v3.0",
                url,
                width=1260,
                height=860,
                min_size=(980, 700),
                background_color="#07090e"
            )
            webview.start(_post_window_launch_optimize)
            engine.stop()
            driver.close()
            sys.exit(0)
        except Exception:
            pass

    webbrowser.open(url)
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        engine.stop()
        driver.close()


if __name__ == "__main__":
    main()
