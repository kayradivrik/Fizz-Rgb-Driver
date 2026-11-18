using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace RedragonK617Studio
{
    public sealed class MacroStepItem
    {
        public string type { get; set; }
        public object value { get; set; }
    }

    public sealed class MacroDefinition
    {
        public string id { get; set; }
        public string name { get; set; }
        public string trigger { get; set; }
        public string repeat_mode { get; set; }
        public int repeat_count { get; set; }
        public bool enabled { get; set; }
        public List<MacroStepItem> steps { get; set; }
    }

    public sealed class NativeMacroEngine : IDisposable
    {
        private readonly string _macrosFile;
        private readonly object _lock = new object();
        public List<MacroDefinition> Macros = new List<MacroDefinition>();
        public bool GlobalEnabled = true;
        public bool IsRecording = false;
        public List<MacroStepItem> RecordedSteps = new List<MacroStepItem>();
        private long _lastRecTick = 0;
        private bool _isSimulating = false;
        private readonly Dictionary<string, ManualResetEvent> _activeLoops = new Dictionary<string, ManualResetEvent>();

        private IntPtr _hookId = IntPtr.Zero;
        private readonly NativeMethods.LowLevelKeyboardProc _hookProc;
        public event Action<int, string> OnPhysicalKeyPressed;

        private static readonly Dictionary<string, byte> KeyNameToVk = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            {"backspace", 0x08}, {"tab", 0x09}, {"enter", 0x0D}, {"shift", 0x10}, {"lshift", 0xA0}, {"rshift", 0xA1},
            {"ctrl", 0x11}, {"lctrl", 0xA2}, {"rctrl", 0xA3}, {"alt", 0x12}, {"lalt", 0xA4}, {"ralt", 0xA5},
            {"caps", 0x14}, {"esc", 0x1B}, {"space", 0x20}, {"left", 0x25}, {"up", 0x26}, {"right", 0x27}, {"down", 0x28},
            {"0", 0x30}, {"1", 0x31}, {"2", 0x32}, {"3", 0x33}, {"4", 0x34}, {"5", 0x35}, {"6", 0x36}, {"7", 0x37}, {"8", 0x38}, {"9", 0x39},
            {"a", 0x41}, {"b", 0x42}, {"c", 0x43}, {"d", 0x44}, {"e", 0x45}, {"f", 0x46}, {"g", 0x47}, {"h", 0x48}, {"i", 0x49},
            {"j", 0x4A}, {"k", 0x4B}, {"l", 0x4C}, {"m", 0x4D}, {"n", 0x4E}, {"o", 0x4F}, {"p", 0x50}, {"q", 0x51}, {"r", 0x52},
            {"s", 0x53}, {"t", 0x54}, {"u", 0x55}, {"v", 0x56}, {"w", 0x57}, {"x", 0x58}, {"y", 0x59}, {"z", 0x5A},
            {"f1", 0x70}, {"f2", 0x71}, {"f3", 0x72}, {"f4", 0x73}, {"f5", 0x74}, {"f6", 0x75},
            {"f7", 0x76}, {"f8", 0x77}, {"f9", 0x78}, {"f10", 0x79}, {"f11", 0x7A}, {"f12", 0x7B}
        };

        private static readonly Dictionary<int, string> VkToKeyName = new Dictionary<int, string>();

        static NativeMacroEngine()
        {
            foreach (var kv in KeyNameToVk)
            {
                if (!VkToKeyName.ContainsKey(kv.Value))
                    VkToKeyName[kv.Value] = kv.Key;
            }
        }

        public NativeMacroEngine(string baseDir)
        {
            _macrosFile = Path.Combine(baseDir, "macros.json");
            Load();
            _hookProc = HookCallback;
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _hookProc, NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_macrosFile))
                {
                    var ser = new JavaScriptSerializer();
                    var loaded = ser.Deserialize<List<MacroDefinition>>(File.ReadAllText(_macrosFile, Encoding.UTF8));
                    if (loaded != null) { Macros = loaded; return; }
                }
            }
            catch { }

            Macros = new List<MacroDefinition>
            {
                new MacroDefinition {
                    id = "macro_quickswitch", name = "CS2 / Valorant Quick Switch (AWP/Op)", trigger = "f6", repeat_mode = "once", repeat_count = 1, enabled = false,
                    steps = new List<MacroStepItem> {
                        new MacroStepItem { type = "key_press", value = "3" },
                        new MacroStepItem { type = "delay", value = 45 },
                        new MacroStepItem { type = "key_press", value = "1" }
                    }
                },
                new MacroDefinition {
                    id = "macro_autoclick", name = "Turbo Sol Tık (Auto-Clicker 40 CPS)", trigger = "f7", repeat_mode = "toggle_loop", repeat_count = 1, enabled = false,
                    steps = new List<MacroStepItem> {
                        new MacroStepItem { type = "mouse_click", value = "left" },
                        new MacroStepItem { type = "delay", value = 25 }
                    }
                },
                new MacroDefinition {
                    id = "macro_bhop", name = "BunnyHop Zıplama Döngüsü", trigger = "f8", repeat_mode = "toggle_loop", repeat_count = 1, enabled = false,
                    steps = new List<MacroStepItem> {
                        new MacroStepItem { type = "key_press", value = "space" },
                        new MacroStepItem { type = "delay", value = 32 }
                    }
                }
            };
            Save();
        }

        public void Save()
        {
            try
            {
                var ser = new JavaScriptSerializer();
                File.WriteAllText(_macrosFile, ser.Serialize(Macros), Encoding.UTF8);
            }
            catch { }
        }

        public void StartRecording()
        {
            lock (_lock)
            {
                RecordedSteps.Clear();
                _lastRecTick = Environment.TickCount;
                IsRecording = true;
            }
        }

        public List<MacroStepItem> StopRecording()
        {
            lock (_lock)
            {
                IsRecording = false;
                return new List<MacroStepItem>(RecordedSteps);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && !_isSimulating && (wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                string kName;
                if (!VkToKeyName.TryGetValue(vkCode, out kName))
                    kName = "vk_" + vkCode;

                if (OnPhysicalKeyPressed != null)
                    OnPhysicalKeyPressed(vkCode, kName);

                if (IsRecording)
                {
                    long now = Environment.TickCount;
                    int dt = (int)(now - _lastRecTick);
                    _lastRecTick = now;
                    lock (_lock)
                    {
                        if (RecordedSteps.Count > 0 && dt > 12)
                            RecordedSteps.Add(new MacroStepItem { type = "delay", value = Math.Min(2000, dt) });
                        RecordedSteps.Add(new MacroStepItem { type = "key_press", value = kName });
                    }
                }
                else if (GlobalEnabled)
                {
                    MacroDefinition matched = null;
                    lock (_lock)
                    {
                        for (int i = 0; i < Macros.Count; i++)
                        {
                            var m = Macros[i];
                            if (m.enabled && string.Equals(m.trigger, kName, StringComparison.OrdinalIgnoreCase))
                            {
                                matched = m;
                                break;
                            }
                        }
                    }
                    if (matched != null)
                    {
                        TriggerMacro(matched);
                    }
                }
            }
            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void TriggerMacro(MacroDefinition macro)
        {
            string mid = macro.id ?? "default";
            bool isToggle = string.Equals(macro.repeat_mode, "toggle_loop", StringComparison.OrdinalIgnoreCase);

            if (isToggle)
            {
                lock (_lock)
                {
                    ManualResetEvent existing;
                    if (_activeLoops.TryGetValue(mid, out existing))
                    {
                        existing.Set();
                        _activeLoops.Remove(mid);
                        return;
                    }
                    var stopEv = new ManualResetEvent(false);
                    _activeLoops[mid] = stopEv;
                    ThreadPool.QueueUserWorkItem(_ => ExecuteMacroWorker(macro, stopEv, true));
                }
            }
            else
            {
                var stopEv = new ManualResetEvent(false);
                ThreadPool.QueueUserWorkItem(_ => ExecuteMacroWorker(macro, stopEv, false));
            }
        }

        private void ExecuteMacroWorker(MacroDefinition macro, ManualResetEvent stopEv, bool loop)
        {
            int count = string.Equals(macro.repeat_mode, "repeat_n", StringComparison.OrdinalIgnoreCase)
                ? Math.Max(1, macro.repeat_count) : 1;

            try
            {
                if (loop)
                {
                    while (!stopEv.WaitOne(0) && GlobalEnabled && macro.enabled)
                    {
                        RunSteps(macro.steps, stopEv);
                        Thread.Sleep(5);
                    }
                }
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (stopEv.WaitOne(0) || !GlobalEnabled) break;
                        RunSteps(macro.steps, stopEv);
                    }
                }
            }
            finally
            {
                if (loop)
                {
                    lock (_lock) { _activeLoops.Remove(macro.id ?? "default"); }
                }
            }
        }

        private void RunSteps(List<MacroStepItem> steps, ManualResetEvent stopEv)
        {
            if (steps == null) return;
            for (int i = 0; i < steps.Count; i++)
            {
                if (stopEv.WaitOne(0)) return;
                var st = steps[i];
                string t = st.type ?? "";
                string valStr = Convert.ToString(st.value ?? "");

                if (t == "delay")
                {
                    int ms = 30;
                    int.TryParse(valStr, out ms);
                    Thread.Sleep(Math.Max(1, ms));
                }
                else if (t == "key_press")
                {
                    byte vk;
                    if (KeyNameToVk.TryGetValue(valStr, out vk))
                    {
                        _isSimulating = true;
                        NativeMethods.keybd_event(vk, 0, 0, UIntPtr.Zero);
                        Thread.Sleep(10);
                        NativeMethods.keybd_event(vk, 0, 2, UIntPtr.Zero);
                        _isSimulating = false;
                    }
                }
                else if (t == "mouse_click")
                {
                    string btn = valStr.ToLowerInvariant();
                    if (btn == "right")
                    {
                        NativeMethods.mouse_event(0x0008, 0, 0, 0, UIntPtr.Zero);
                        Thread.Sleep(10);
                        NativeMethods.mouse_event(0x0010, 0, 0, 0, UIntPtr.Zero);
                    }
                    else if (btn == "middle")
                    {
                        NativeMethods.mouse_event(0x0020, 0, 0, 0, UIntPtr.Zero);
                        Thread.Sleep(10);
                        NativeMethods.mouse_event(0x0040, 0, 0, 0, UIntPtr.Zero);
                    }
                    else
                    {
                        NativeMethods.mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
                        Thread.Sleep(10);
                        NativeMethods.mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
                    }
                }
                else if (t == "text")
                {
                    _isSimulating = true;
                    foreach (char ch in valStr)
                    {
                        string s = ch.ToString().ToLowerInvariant();
                        if (ch == ' ') s = "space";
                        byte vk;
                        if (KeyNameToVk.TryGetValue(s, out vk))
                        {
                            NativeMethods.keybd_event(vk, 0, 0, UIntPtr.Zero);
                            Thread.Sleep(8);
                            NativeMethods.keybd_event(vk, 0, 2, UIntPtr.Zero);
                            Thread.Sleep(8);
                        }
                    }
                    _isSimulating = false;
                }
            }
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }
    }

    // =========================================================================
    // ZERO-ALLOCATION RGB EFFECTS ENGINE
    // =========================================================================
    public sealed class RgbEffectsEngine : IDisposable
    {
        private readonly K617HardwareDriver _driver;
        private readonly SystemAudioMeter _audioMeter = new SystemAudioMeter();
        private readonly Thread _worker;
        private volatile bool _running = true;
        private readonly object _stateLock = new object();

        // Zero-allocation color buffers (61 keys)
        public readonly byte[] KeyR = new byte[61];
        public readonly byte[] KeyG = new byte[61];
        public readonly byte[] KeyB = new byte[61];

        public readonly byte[] PaintedR = new byte[61];
        public readonly byte[] PaintedG = new byte[61];
        public readonly byte[] PaintedB = new byte[61];

        public string CurrentMode = "rainbow_wave";
        public double Brightness = 1.0;
        public double Speed = 1.0;
        public double AudioSensitivity = 1.5;
        public string Direction = "right";
        public byte PrimaryR = 0, PrimaryG = 240, PrimaryB = 255;
        public byte SecondaryR = 255, SecondaryG = 0, SecondaryB = 128;
        public byte TertiaryR = 255, TertiaryG = 184, TertiaryB = 0;

        public int TargetFps = 35;
        public bool EcoMode = true;
        public int IdleTimeoutMinutes = 15;
        public double ActualFps = 35.0;

        private volatile bool _staticDirty = true;
        private double _lastStaticSend = 0.0;
        private long _lastInputTick = Environment.TickCount;
        private bool _isSleeping = false;

        // Reactive structures
        private struct Ripple { public double Cx, Cy, Radius, Life; }
        private struct LaserLine { public int Row; public double Col, Life; }
        private struct Spark { public double X, Y, Vx, Vy, Life; public byte R, G, B; }

        private readonly List<Ripple> _ripples = new List<Ripple>(16);
        private readonly double[] _singleFade = new double[61];
        private readonly List<LaserLine> _lines = new List<LaserLine>(8);
        private readonly List<Spark> _sparks = new List<Spark>(48);

        // Effect internal buffers
        private readonly double[] _matrixRow = new double[14];
        private readonly double[] _matrixSpd = new double[14];
        private readonly double[,] _fireGrid = new double[5, 14];
        private readonly double[] _stars = new double[61];
        private readonly byte[] _glitchR = new byte[61];
        private readonly byte[] _glitchG = new byte[61];
        private readonly byte[] _glitchB = new byte[61];
        private double _glitchTimer = 0;
        private double _policeTimer = 0;
        private double _heartbeatX = 0;
        private double _radarAngle = 0;
        private readonly double[] _blobX = { 3.5, 7.5, 11.5 };
        private readonly double[] _blobY = { 1.5, 2.5, 2.0 };
        private readonly double[] _blobVx = { 0.45, -0.35, 0.50 };
        private readonly double[] _blobVy = { 0.32, 0.42, -0.25 };
        private readonly double[] _blobRad = { 2.3, 2.6, 2.0 };
        private readonly Random _rng = new Random();

        public RgbEffectsEngine(K617HardwareDriver driver)
        {
            _driver = driver;
            for (int i = 0; i < 61; i++)
            {
                PaintedR[i] = 0; PaintedG[i] = 240; PaintedB[i] = 255;
                _glitchR[i] = 0; _glitchG[i] = 240; _glitchB[i] = 255;
            }
            for (int i = 0; i < 14; i++)
            {
                _matrixRow[i] = -_rng.NextDouble() * 5.0;
                _matrixSpd[i] = 2.5 + _rng.NextDouble() * 3.0;
            }

            _worker = new Thread(RenderLoop) { IsBackground = true, Priority = ThreadPriority.AboveNormal };
            _worker.Start();
        }

        public void NotifyKeyPress(int vkCode)
        {
            _lastInputTick = Environment.TickCount;
            if (_isSleeping)
            {
                _isSleeping = false;
                _staticDirty = true;
            }

            int kIdx;
            if (!K617HardwareDriver.VkToKeyIndex.TryGetValue(vkCode, out kIdx))
                return;

            var k = K617HardwareDriver.Keys[kIdx];
            lock (_stateLock)
            {
                if (CurrentMode == "reactive_ripple")
                {
                    if (_ripples.Count >= 12) _ripples.RemoveAt(0);
                    _ripples.Add(new Ripple { Cx = k.Cx, Cy = k.Cy, Radius = 0, Life = 1.0 });
                }
                else if (CurrentMode == "reactive_single")
                {
                    _singleFade[kIdx] = 1.0;
                }
                else if (CurrentMode == "reactive_line")
                {
                    if (_lines.Count >= 6) _lines.RemoveAt(0);
                    _lines.Add(new LaserLine { Row = k.Row, Col = k.Cx, Life = 1.0 });
                }
                else if (CurrentMode == "reactive_explosion")
                {
                    for (int i = 0; i < 5; i++)
                    {
                        if (_sparks.Count >= 40) _sparks.RemoveAt(0);
                        double ang = _rng.NextDouble() * Math.PI * 2.0;
                        double s = 2.0 + _rng.NextDouble() * 4.5;
                        int cPick = i % 3;
                        _sparks.Add(new Spark
                        {
                            X = k.Cx, Y = k.Cy,
                            Vx = Math.Cos(ang) * s, Vy = Math.Sin(ang) * s,
                            Life = 1.0,
                            R = cPick == 0 ? PrimaryR : (cPick == 1 ? SecondaryR : TertiaryR),
                            G = cPick == 0 ? PrimaryG : (cPick == 1 ? SecondaryG : TertiaryG),
                            B = cPick == 0 ? PrimaryB : (cPick == 1 ? SecondaryB : TertiaryB)
                        });
                    }
                }
            }
        }

        public void MarkDirty() { _staticDirty = true; }

        private static void HsvToRgb(double h, double s, double v, out byte r, out byte g, out byte b)
        {
            h = (h % 1.0 + 1.0) % 1.0;
            int i = (int)(h * 6.0);
            double f = h * 6.0 - i;
            double p = v * (1.0 - s);
            double q = v * (1.0 - f * s);
            double t = v * (1.0 - (1.0 - f) * s);
            double dr, dg, db;
            switch (i % 6)
            {
                case 0: dr = v; dg = t; db = p; break;
                case 1: dr = q; dg = v; db = p; break;
                case 2: dr = p; dg = v; db = t; break;
                case 3: dr = p; dg = q; db = v; break;
                case 4: dr = t; dg = p; db = v; break;
                default: dr = v; dg = p; db = q; break;
            }
            r = (byte)Math.Max(0, Math.Min(255, (int)(dr * 255.0)));
            g = (byte)Math.Max(0, Math.Min(255, (int)(dg * 255.0)));
            b = (byte)Math.Max(0, Math.Min(255, (int)(db * 255.0)));
        }

        private static byte ClampByte(double val)
        {
            int v = (int)val;
            return (byte)(v < 0 ? 0 : (v > 255 ? 255 : v));
        }

        private void RenderLoop()
        {
            var sw = Stopwatch.StartNew();
            var keys = K617HardwareDriver.Keys;

            while (_running)
            {
                double now = sw.Elapsed.TotalSeconds;
                int fps = Math.Max(10, Math.Min(60, TargetFps));
                double dt = 1.0 / fps;

                // Idle timeout check
                if (IdleTimeoutMinutes > 0)
                {
                    long idleMs = Environment.TickCount - _lastInputTick;
                    if (idleMs > IdleTimeoutMinutes * 60000L)
                    {
                        if (!_isSleeping)
                        {
                            _isSleeping = true;
                            _driver.SendSolidColor(0, 0, 0);
                        }
                        Thread.Sleep(250);
                        continue;
                    }
                }

                string mode = CurrentMode;
                double b = Brightness;
                double spd = Speed;

                if (mode == "firmware")
                {
                    Thread.Sleep(150);
                    continue;
                }

                if (mode == "static" || mode == "per_key")
                {
                    if (!_staticDirty && (now - _lastStaticSend) < 2.0)
                    {
                        Thread.Sleep(45);
                        continue;
                    }
                    if (mode == "static")
                    {
                        byte sr = ClampByte(PrimaryR * b);
                        byte sg = ClampByte(PrimaryG * b);
                        byte sb = ClampByte(PrimaryB * b);
                        for (int i = 0; i < 61; i++) { KeyR[i] = sr; KeyG[i] = sg; KeyB[i] = sb; }
                    }
                    else
                    {
                        for (int i = 0; i < 61; i++)
                        {
                            KeyR[i] = ClampByte(PaintedR[i] * b);
                            KeyG[i] = ClampByte(PaintedG[i] * b);
                            KeyB[i] = ClampByte(PaintedB[i] * b);
                        }
                    }
                    _driver.SendPerKeyColors(KeyR, KeyG, KeyB);
                    _staticDirty = false;
                    _lastStaticSend = now;
                    Thread.Sleep(40);
                    continue;
                }

                // Dynamic Software Modes
                if (mode == "rainbow_wave")
                {
                    double shift = now * 0.3 * spd;
                    for (int i = 0; i < 61; i++)
                    {
                        var k = keys[i];
                        double coord;
                        if (Direction == "left") coord = -k.Cx * 0.07;
                        else if (Direction == "up") coord = -k.Cy * 0.2;
                        else if (Direction == "down") coord = k.Cy * 0.2;
                        else coord = k.Cx * 0.06 + k.Cy * 0.03;
                        HsvToRgb(coord - shift, 1.0, b, out KeyR[i], out KeyG[i], out KeyB[i]);
                    }
                }
                else if (mode == "rainbow_spiral")
                {
                    double shift = now * 0.4 * spd;
                    double inv2Pi = 1.0 / (2.0 * Math.PI);
                    for (int i = 0; i < 61; i++)
                    {
                        var k = keys[i];
                        HsvToRgb(k.Angle * inv2Pi + k.Dist * 0.08 - shift, 1.0, b, out KeyR[i], out KeyG[i], out KeyB[i]);
                    }
                }
                else if (mode == "color_cycle")
                {
                    byte cr, cg, cb;
                    HsvToRgb(now * 0.2 * spd, 1.0, b, out cr, out cg, out cb);
                    for (int i = 0; i < 61; i++) { KeyR[i] = cr; KeyG[i] = cg; KeyB[i] = cb; }
                }
                else if (mode == "gradient_sweep")
                {
                    double phase = (Math.Sin(now * 1.5 * spd) + 1.0) * 0.5;
                    for (int i = 0; i < 61; i++)
                    {
                        double t = (keys[i].Cx / 15.0 + phase) % 1.0;
                        if (t < 0.5)
                        {
                            double f = t * 2.0;
                            KeyR[i] = ClampByte((PrimaryR * (1 - f) + SecondaryR * f) * b);
                            KeyG[i] = ClampByte((PrimaryG * (1 - f) + SecondaryG * f) * b);
                            KeyB[i] = ClampByte((PrimaryB * (1 - f) + SecondaryB * f) * b);
                        }
                        else
                        {
                            double f = (t - 0.5) * 2.0;
                            KeyR[i] = ClampByte((SecondaryR * (1 - f) + TertiaryR * f) * b);
                            KeyG[i] = ClampByte((SecondaryG * (1 - f) + TertiaryG * f) * b);
                            KeyB[i] = ClampByte((SecondaryB * (1 - f) + TertiaryB * f) * b);
                        }
                    }
                }
                else if (mode == "breathing")
                {
                    double p = (Math.Sin(now * 2.0 * spd) + 1.0) * 0.5;
                    byte br = ClampByte((PrimaryR * p + SecondaryR * (1 - p)) * b);
                    byte bg = ClampByte((PrimaryG * p + SecondaryG * (1 - p)) * b);
                    byte bb = ClampByte((PrimaryB * p + SecondaryB * (1 - p)) * b);
                    for (int i = 0; i < 61; i++) { KeyR[i] = br; KeyG[i] = bg; KeyB[i] = bb; }
                }
                else if (mode == "reactive_ripple")
                {
                    byte br = ClampByte(SecondaryR * 0.06 * b);
                    byte bg = ClampByte(SecondaryG * 0.06 * b);
                    byte bb = ClampByte(SecondaryB * 0.06 * b);
                    for (int i = 0; i < 61; i++) { KeyR[i] = br; KeyG[i] = bg; KeyB[i] = bb; }

                    lock (_stateLock)
                    {
                        for (int r = _ripples.Count - 1; r >= 0; r--)
                        {
                            var rip = _ripples[r];
                            rip.Radius += dt * 9.0 * spd;
                            rip.Life -= dt * 1.6 * spd;
                            if (rip.Life <= 0) { _ripples.RemoveAt(r); continue; }
                            _ripples[r] = rip;

                            for (int i = 0; i < 61; i++)
                            {
                                double dx = keys[i].Cx - rip.Cx;
                                double dy = (keys[i].Cy - rip.Cy) * 1.6;
                                double diff = Math.Abs(Math.Sqrt(dx * dx + dy * dy) - rip.Radius);
                                if (diff < 1.3)
                                {
                                    double inten = (1.0 - diff / 1.3) * rip.Life * b;
                                    KeyR[i] = ClampByte(KeyR[i] + PrimaryR * inten);
                                    KeyG[i] = ClampByte(KeyG[i] + PrimaryG * inten);
                                    KeyB[i] = ClampByte(KeyB[i] + PrimaryB * inten);
                                }
                            }
                        }
                    }
                }
                else if (mode == "reactive_single")
                {
                    byte br = ClampByte(SecondaryR * 0.05 * b);
                    byte bg = ClampByte(SecondaryG * 0.05 * b);
                    byte bb = ClampByte(SecondaryB * 0.05 * b);
                    lock (_stateLock)
                    {
                        for (int i = 0; i < 61; i++)
                        {
                            double lf = _singleFade[i];
                            if (lf > 0)
                            {
                                lf = Math.Max(0.0, lf - dt * 2.0 * spd);
                                _singleFade[i] = lf;
                                KeyR[i] = ClampByte(PrimaryR * lf * b);
                                KeyG[i] = ClampByte(PrimaryG * lf * b);
                                KeyB[i] = ClampByte(PrimaryB * lf * b);
                            }
                            else { KeyR[i] = br; KeyG[i] = bg; KeyB[i] = bb; }
                        }
                    }
                }
                else if (mode == "reactive_line")
                {
                    byte br = ClampByte(SecondaryR * 0.05 * b);
                    byte bg = ClampByte(SecondaryG * 0.05 * b);
                    byte bb = ClampByte(SecondaryB * 0.05 * b);
                    for (int i = 0; i < 61; i++) { KeyR[i] = br; KeyG[i] = bg; KeyB[i] = bb; }

                    lock (_stateLock)
                    {
                        for (int l = _lines.Count - 1; l >= 0; l--)
                        {
                            var ln = _lines[l];
                            ln.Life -= dt * 2.2 * spd;
                            if (ln.Life <= 0) { _lines.RemoveAt(l); continue; }
                            _lines[l] = ln;

                            for (int i = 0; i < 61; i++)
                            {
                                double rDist = Math.Abs(keys[i].Row - ln.Row);
                                double cDist = Math.Abs(keys[i].Cx - ln.Col);
                                double inten = 0;
                                if (rDist < 0.5) inten = Math.Max(inten, (1.0 - cDist / 15.0) * ln.Life * b);
                                if (cDist < 0.8) inten = Math.Max(inten, (1.0 - rDist / 5.0) * ln.Life * b);
                                if (inten > 0)
                                {
                                    KeyR[i] = ClampByte(KeyR[i] + PrimaryR * inten);
                                    KeyG[i] = ClampByte(KeyG[i] + PrimaryG * inten);
                                    KeyB[i] = ClampByte(KeyB[i] + PrimaryB * inten);
                                }
                            }
                        }
                    }
                }
                else if (mode == "reactive_explosion")
                {
                    byte br = ClampByte(SecondaryR * 0.05 * b);
                    byte bg = ClampByte(SecondaryG * 0.05 * b);
                    byte bb = ClampByte(SecondaryB * 0.05 * b);
                    for (int i = 0; i < 61; i++) { KeyR[i] = br; KeyG[i] = bg; KeyB[i] = bb; }

                    lock (_stateLock)
                    {
                        for (int s = _sparks.Count - 1; s >= 0; s--)
                        {
                            var sp = _sparks[s];
                            sp.X += sp.Vx * dt * spd;
                            sp.Y += sp.Vy * dt * spd;
                            sp.Life -= dt * 2.0 * spd;
                            if (sp.Life <= 0 || sp.X < 0 || sp.X > 15 || sp.Y < 0 || sp.Y > 5)
                            {
                                _sparks.RemoveAt(s);
                                continue;
                            }
                            _sparks[s] = sp;
                            for (int i = 0; i < 61; i++)
                            {
                                double dx = keys[i].Cx - sp.X;
                                double dy = (keys[i].Cy - sp.Y) * 1.5;
                                double d = Math.Sqrt(dx * dx + dy * dy);
                                if (d < 1.0)
                                {
                                    double fade = (1.0 - d) * sp.Life * b;
                                    KeyR[i] = ClampByte(KeyR[i] + sp.R * fade);
                                    KeyG[i] = ClampByte(KeyG[i] + sp.G * fade);
                                    KeyB[i] = ClampByte(KeyB[i] + sp.B * fade);
                                }
                            }
                        }
                    }
                }
                else if (mode.StartsWith("audio_"))
                {
                    _audioMeter.Update(AudioSensitivity, now);
                    if (mode == "audio_bars")
                    {
                        for (int i = 0; i < 61; i++)
                        {
                            double lvl = _audioMeter.Levels[keys[i].ColIdx];
                            double thresh = (4 - keys[i].Row) * 0.2;
                            if (lvl >= thresh)
                            {
                                double nh = (4 - keys[i].Row) * 0.25;
                                if (nh < 0.5)
                                {
                                    KeyR[i] = ClampByte(nh * 2.0 * 255 * b);
                                    KeyG[i] = ClampByte(255 * b);
                                    KeyB[i] = ClampByte((1.0 - nh * 2.0) * 200 * b);
                                }
                                else
                                {
                                    KeyR[i] = ClampByte(255 * b);
                                    KeyG[i] = ClampByte((1.0 - (nh - 0.5) * 2.0) * 255 * b);
                                    KeyB[i] = 0;
                                }
                            }
                            else { KeyR[i] = 0; KeyG[i] = ClampByte(15 * b); KeyB[i] = ClampByte(30 * b); }
                        }
                    }
                    else if (mode == "audio_pulse")
                    {
                        byte ar, ag, ab;
                        HsvToRgb(now * 0.15, 1.0, _audioMeter.Peak * b, out ar, out ag, out ab);
                        for (int i = 0; i < 61; i++)
                        {
                            KeyR[i] = ClampByte(ar + PrimaryR * 0.08 * b);
                            KeyG[i] = ClampByte(ag + PrimaryG * 0.08 * b);
                            KeyB[i] = ClampByte(ab + PrimaryB * 0.08 * b);
                        }
                    }
                    else
                    {
                        double rad = _audioMeter.Peak * 8.0;
                        for (int i = 0; i < 61; i++)
                        {
                            double diff = Math.Abs(keys[i].Dist - rad);
                            if (diff < 1.8)
                            {
                                double v = (1.0 - diff / 1.8) * b;
                                KeyR[i] = ClampByte(PrimaryR * v);
                                KeyG[i] = ClampByte(PrimaryG * v);
                                KeyB[i] = ClampByte(PrimaryB * v);
                            }
                            else
                            {
                                KeyR[i] = ClampByte(SecondaryR * 0.06 * b);
                                KeyG[i] = ClampByte(SecondaryG * 0.06 * b);
                                KeyB[i] = ClampByte(SecondaryB * 0.06 * b);
                            }
                        }
                    }
                }
                else if (mode == "matrix")
                {
                    for (int c = 0; c < 14; c++)
                    {
                        _matrixRow[c] += dt * _matrixSpd[c] * spd;
                        if (_matrixRow[c] > 6.0)
                        {
                            _matrixRow[c] = -_rng.NextDouble() * 4.0;
                            _matrixSpd[c] = 2.5 + _rng.NextDouble() * 3.5;
                        }
                    }
                    for (int i = 0; i < 61; i++)
                    {
                        double dist = _matrixRow[keys[i].ColIdx] - keys[i].Row;
                        if (dist >= 0 && dist < 0.7)
                        {
                            KeyR[i] = ClampByte(220 * b); KeyG[i] = ClampByte(255 * b); KeyB[i] = ClampByte(220 * b);
                        }
                        else if (dist >= 0.7 && dist < 3.2)
                        {
                            double fade = 1.0 - (dist - 0.7) / 2.5;
                            KeyR[i] = 0; KeyG[i] = ClampByte(255 * fade * b); KeyB[i] = ClampByte(45 * fade * b);
                        }
                        else { KeyR[i] = 0; KeyG[i] = 0; KeyB[i] = 0; }
                    }
                }
                else if (mode == "cyberpunk_glitch")
                {
                    _glitchTimer += dt * spd;
                    if (_glitchTimer > 0.12)
                    {
                        _glitchTimer = 0;
                        for (int i = 0; i < 61; i++)
                        {
                            if (_rng.NextDouble() < 0.25)
                            {
                                int p = _rng.Next(4);
                                if (p == 0) { _glitchR[i] = 0; _glitchG[i] = 240; _glitchB[i] = 255; }
                                else if (p == 1) { _glitchR[i] = 255; _glitchG[i] = 0; _glitchB[i] = 128; }
                                else if (p == 2) { _glitchR[i] = 255; _glitchG[i] = 230; _glitchB[i] = 0; }
                                else { _glitchR[i] = 10; _glitchG[i] = 15; _glitchB[i] = 25; }
                            }
                        }
                    }
                    for (int i = 0; i < 61; i++)
                    {
                        KeyR[i] = ClampByte(_glitchR[i] * b);
                        KeyG[i] = ClampByte(_glitchG[i] * b);
                        KeyB[i] = ClampByte(_glitchB[i] * b);
                    }
                }
                else if (mode == "fire")
                {
                    for (int x = 0; x < 14; x++) _fireGrid[4, x] = 0.7 + _rng.NextDouble() * 0.3;
                    for (int y = 3; y >= 0; y--)
                    {
                        for (int x = 0; x < 14; x++)
                        {
                            double l = _fireGrid[y + 1, (x + 13) % 14];
                            double m = _fireGrid[y + 1, x];
                            double r = _fireGrid[y + 1, (x + 1) % 14];
                            double h = (l + m * 2.0 + r) * 0.25 - (0.12 + _rng.NextDouble() * 0.12) / Math.Max(0.3, spd);
                            _fireGrid[y, x] = Math.Max(0.0, h);
                        }
                    }
                    for (int i = 0; i < 61; i++)
                    {
                        double h = _fireGrid[keys[i].Row, keys[i].ColIdx];
                        if (h < 0.3) { KeyR[i] = ClampByte(h / 0.3 * 180 * b); KeyG[i] = 0; KeyB[i] = 0; }
                        else if (h < 0.7) { double f = (h - 0.3) / 0.4; KeyR[i] = ClampByte(255 * b); KeyG[i] = ClampByte(f * 180 * b); KeyB[i] = 0; }
                        else { double f = (h - 0.7) / 0.3; KeyR[i] = ClampByte(255 * b); KeyG[i] = ClampByte((180 + f * 75) * b); KeyB[i] = ClampByte(f * 140 * b); }
                    }
                }
                else if (mode == "police_lights")
                {
                    _policeTimer += dt * 7.0 * spd;
                    int side = ((int)_policeTimer) % 4;
                    for (int i = 0; i < 61; i++)
                    {
                        bool left = keys[i].Cx < 7.5;
                        if ((side == 0 || side == 1) && left) { KeyR[i] = ClampByte(255 * b); KeyG[i] = 0; KeyB[i] = 0; }
                        else if ((side == 2 || side == 3) && !left) { KeyR[i] = 0; KeyG[i] = 0; KeyB[i] = ClampByte(255 * b); }
                        else { KeyR[i] = 0; KeyG[i] = 0; KeyB[i] = 0; }
                    }
                }
                else if (mode == "aurora")
                {
                    for (int i = 0; i < 61; i++)
                    {
                        double w1 = Math.Sin(keys[i].Cx * 0.35 + now * 1.5 * spd);
                        double w2 = Math.Cos(keys[i].Cy * 0.8 - now * 1.2 * spd);
                        double t = (w1 + w2 + 2.0) * 0.25;
                        if (t < 0.5)
                        {
                            double f = t * 2.0;
                            KeyR[i] = 0; KeyG[i] = ClampByte((220 + 35 * f) * b); KeyB[i] = ClampByte(120 * f * b);
                        }
                        else
                        {
                            double f = (t - 0.5) * 2.0;
                            KeyR[i] = ClampByte(140 * f * b); KeyG[i] = ClampByte(255 * (1 - f) * b); KeyB[i] = ClampByte((120 + 135 * f) * b);
                        }
                    }
                }
                else if (mode == "ocean_tide")
                {
                    for (int i = 0; i < 61; i++)
                    {
                        double t = (Math.Sin(keys[i].Cx * 0.4 - now * 2.2 * spd) + 1.0) * 0.5;
                        if (t < 0.7)
                        {
                            double f = t / 0.7;
                            KeyR[i] = ClampByte(5 * f * b); KeyG[i] = ClampByte((40 + 100 * f) * b); KeyB[i] = ClampByte((140 + 115 * f) * b);
                        }
                        else
                        {
                            double f = (t - 0.7) / 0.3;
                            KeyR[i] = ClampByte((5 + 230 * f) * b); KeyG[i] = ClampByte((140 + 115 * f) * b); KeyB[i] = ClampByte(255 * b);
                        }
                    }
                }
                else if (mode == "starlight")
                {
                    byte br = ClampByte(PrimaryR * 0.05 * b);
                    byte bg = ClampByte(PrimaryG * 0.05 * b);
                    byte bb = ClampByte(PrimaryB * 0.05 * b);
                    for (int i = 0; i < 61; i++)
                    {
                        if (_stars[i] <= 0 && _rng.NextDouble() < 0.025 * spd) _stars[i] = 1.0;
                        else if (_stars[i] > 0) _stars[i] = Math.Max(0.0, _stars[i] - dt * 1.6 * spd);
                        double st = _stars[i];
                        if (st > 0)
                        {
                            KeyR[i] = ClampByte((PrimaryR * st + TertiaryR * (1 - st)) * b * st);
                            KeyG[i] = ClampByte((PrimaryG * st + TertiaryG * (1 - st)) * b * st);
                            KeyB[i] = ClampByte((PrimaryB * st + TertiaryB * (1 - st)) * b * st);
                        }
                        else { KeyR[i] = br; KeyG[i] = bg; KeyB[i] = bb; }
                    }
                }
                else if (mode == "heartbeat")
                {
                    _heartbeatX = (_heartbeatX + dt * 14.0 * spd) % 17.0;
                    for (int i = 0; i < 61; i++)
                    {
                        double d = _heartbeatX - keys[i].Cx;
                        if (d >= 0 && d < 3.0)
                        {
                            double f = 1.0 - d / 3.0;
                            KeyR[i] = ClampByte(255 * f * b); KeyG[i] = ClampByte(20 * f * b); KeyB[i] = ClampByte(60 * f * b);
                        }
                        else { KeyR[i] = ClampByte(30 * b); KeyG[i] = ClampByte(5 * b); KeyB[i] = ClampByte(10 * b); }
                    }
                }
                else if (mode == "radar")
                {
                    _radarAngle = (_radarAngle + dt * 3.5 * spd) % (Math.PI * 2.0);
                    for (int i = 0; i < 61; i++)
                    {
                        double diff = ((_radarAngle - keys[i].Angle) % (Math.PI * 2.0) + Math.PI * 2.0) % (Math.PI * 2.0);
                        if (diff < 1.2)
                        {
                            double f = (1.0 - diff / 1.2) * b;
                            KeyR[i] = 0; KeyG[i] = ClampByte(255 * f); KeyB[i] = ClampByte(100 * f);
                        }
                        else { KeyR[i] = 0; KeyG[i] = ClampByte(15 * b); KeyB[i] = ClampByte(10 * b); }
                    }
                }
                else if (mode == "lava_lamp")
                {
                    for (int bIdx = 0; bIdx < 3; bIdx++)
                    {
                        _blobX[bIdx] += _blobVx[bIdx] * dt * spd;
                        _blobY[bIdx] += _blobVy[bIdx] * dt * spd;
                        if (_blobX[bIdx] < 1.0 || _blobX[bIdx] > 14.0) _blobVx[bIdx] *= -1;
                        if (_blobY[bIdx] < 0.5 || _blobY[bIdx] > 4.5) _blobVy[bIdx] *= -1;
                    }
                    for (int i = 0; i < 61; i++)
                    {
                        double field = 0;
                        for (int bIdx = 0; bIdx < 3; bIdx++)
                        {
                            double dx = keys[i].Cx - _blobX[bIdx];
                            double dy = (keys[i].Cy - _blobY[bIdx]) * 1.6;
                            double d = Math.Sqrt(dx * dx + dy * dy);
                            if (d < _blobRad[bIdx]) field += (1.0 - d / _blobRad[bIdx]);
                        }
                        double t = Math.Min(1.0, field);
                        KeyR[i] = ClampByte((PrimaryR * t + SecondaryR * (1 - t)) * t * b);
                        KeyG[i] = ClampByte((PrimaryG * t + SecondaryG * (1 - t)) * t * b);
                        KeyB[i] = ClampByte((PrimaryB * t + SecondaryB * (1 - t)) * t * b);
                    }
                }

                _driver.SendPerKeyColors(KeyR, KeyG, KeyB);

                double elapsed = sw.Elapsed.TotalSeconds - now;
                int sleepMs = Math.Max(2, (int)((dt - elapsed) * 1000.0));
                Thread.Sleep(sleepMs);
                ActualFps = Math.Round(1.0 / Math.Max(0.001, sw.Elapsed.TotalSeconds - now), 0);
            }
        }

        public void Dispose()
        {
            _running = false;
            try { _worker.Join(500); } catch { }
        }
    }
}
