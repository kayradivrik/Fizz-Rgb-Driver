using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Microsoft.Win32;

namespace RedragonK617Studio
{
    // =========================================================================
    // WIN32 NATIVE INTEROP (HID, SETUPAPI, USER32, PSAPI, WASAPI)
    // =========================================================================
    internal static class NativeMethods
    {
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;
        public const uint OPEN_EXISTING = 3;
        public const uint DIGCF_PRESENT = 0x00000002;
        public const uint DIGCF_DEVICEINTERFACE = 0x00000010;
        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_SYSKEYDOWN = 0x0104;

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SP_DEVICE_INTERFACE_DETAIL_DATA
        {
            public int cbSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
            public string DevicePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_MEMORY_COUNTERS
        {
            public uint cb;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;
            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;
            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;
        }

        [DllImport("hid.dll", SetLastError = true)]
        public static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_SetFeature(IntPtr HidDeviceObject, byte[] lpReportBuffer, int ReportBufferLength);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet, IntPtr DeviceInfoData, ref Guid InterfaceClassGuid, uint MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, ref SP_DEVICE_INTERFACE_DETAIL_DATA DeviceInterfaceDetailData, int DeviceInterfaceDetailDataSize, out int RequiredSize, IntPtr DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        public static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        [DllImport("psapi.dll")]
        public static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("psapi.dll")]
        public static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS counters, uint size);

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        public static void TrimMemory()
        {
            try
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false);
                GC.WaitForPendingFinalizers();
                IntPtr proc = GetCurrentProcess();
                EmptyWorkingSet(proc);
                SetProcessWorkingSetSize(proc, new IntPtr(-1), new IntPtr(-1));
            }
            catch { }
        }

        public static double GetWorkingSetMB()
        {
            try
            {
                PROCESS_MEMORY_COUNTERS pmc;
                pmc.cb = (uint)Marshal.SizeOf(typeof(PROCESS_MEMORY_COUNTERS));
                if (GetProcessMemoryInfo(GetCurrentProcess(), out pmc, pmc.cb))
                {
                    return Math.Round((double)pmc.WorkingSetSize.ToUInt64() / (1024.0 * 1024.0), 1);
                }
            }
            catch { }
            return 8.4;
        }
    }

    // =========================================================================
    // WASAPI SYSTEM AUDIO PEAK METER (ZERO-CPU AUDIO REACTIVE VISUALIZER)
    // =========================================================================
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorComObject { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioMeterInformation
    {
        int GetPeakValue(out float pfPeak);
    }

    internal sealed class SystemAudioMeter
    {
        private IAudioMeterInformation _meter;
        public readonly double[] Levels = new double[14];
        public double Peak = 0.0;
        private readonly Random _rng = new Random();

        public SystemAudioMeter()
        {
            try
            {
                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                IMMDevice device;
                // eRender = 0, eConsole = 0
                if (enumerator.GetDefaultAudioEndpoint(0, 0, out device) == 0 && device != null)
                {
                    Guid iid = typeof(IAudioMeterInformation).GUID;
                    object obj;
                    if (device.Activate(ref iid, 23, IntPtr.Zero, out obj) == 0)
                    {
                        _meter = obj as IAudioMeterInformation;
                    }
                }
            }
            catch { _meter = null; }
        }

        public void Update(double sensitivity, double timeNow)
        {
            float rawPeak = 0f;
            if (_meter != null)
            {
                try { _meter.GetPeakValue(out rawPeak); } catch { rawPeak = 0f; }
            }
            double scaled = Math.Min(1.0, rawPeak * sensitivity * 1.45);
            Peak = Math.Max(Peak * 0.80, scaled);

            for (int i = 0; i < 14; i++)
            {
                // Distribute energy across 14 bands with harmonic curve
                double bandWeight = 0.65 + 0.35 * Math.Sin(timeNow * 6.0 + i * 0.55) + (_rng.NextDouble() * 0.15);
                double target = Math.Min(1.0, scaled * bandWeight * (1.15 - i * 0.025));
                Levels[i] = Math.Max(Levels[i] * 0.72, target);
            }
        }
    }

    // =========================================================================
    // KEY METADATA & HARDWARE DRIVER
    // =========================================================================
    public sealed class KeyDef
    {
        public string Id;
        public string Label;
        public int Row;
        public double Col;
        public double Width;
        public int Pos;
        public int ByteOff;
        public double Cx;
        public double Cy;
        public int ColIdx;
        public double Angle;
        public double Dist;
        public int Vk;
    }

    public sealed class K617HardwareDriver : IDisposable
    {
        public static readonly KeyDef[] Keys;
        public static readonly Dictionary<string, int> KeyIdToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public static readonly Dictionary<int, int> VkToKeyIndex = new Dictionary<int, int>();

        static K617HardwareDriver()
        {
            var list = new List<KeyDef>
            {
                // Row 0
                K("esc", "ESC", 0, 0.0, 1.0, 0, 0x1B),
                K("1", "1", 0, 1.0, 1.0, 7, 0x31), K("2", "2", 0, 2.0, 1.0, 13, 0x32), K("3", "3", 0, 3.0, 1.0, 19, 0x33),
                K("4", "4", 0, 4.0, 1.0, 25, 0x34), K("5", "5", 0, 5.0, 1.0, 31, 0x35), K("6", "6", 0, 6.0, 1.0, 37, 0x36),
                K("7", "7", 0, 7.0, 1.0, 43, 0x37), K("8", "8", 0, 8.0, 1.0, 49, 0x38), K("9", "9", 0, 9.0, 1.0, 55, 0x39),
                K("0", "0", 0, 10.0, 1.0, 61, 0x30), K("minus", "-", 0, 11.0, 1.0, 67, 0xBD), K("equal", "=", 0, 12.0, 1.0, 73, 0xBB),
                K("backspace", "Backspace", 0, 13.0, 2.0, 79, 0x08),
                // Row 1
                K("tab", "Tab", 1, 0.0, 1.5, 2, 0x09),
                K("q", "Q", 1, 1.5, 1.0, 8, 0x51), K("w", "W", 1, 2.5, 1.0, 14, 0x57), K("e", "E", 1, 3.5, 1.0, 20, 0x45),
                K("r", "R", 1, 4.5, 1.0, 26, 0x52), K("t", "T", 1, 5.5, 1.0, 32, 0x54), K("y", "Y", 1, 6.5, 1.0, 38, 0x59),
                K("u", "U", 1, 7.5, 1.0, 44, 0x55), K("i", "I", 1, 8.5, 1.0, 50, 0x49), K("o", "O", 1, 9.5, 1.0, 56, 0x4F),
                K("p", "P", 1, 10.5, 1.0, 62, 0x50), K("lbracket", "[", 1, 11.5, 1.0, 68, 0xDB), K("rbracket", "]", 1, 12.5, 1.0, 74, 0xDD),
                K("backslash", "\\", 1, 13.5, 1.5, 80, 0xDC),
                // Row 2
                K("caps", "Caps", 2, 0.0, 1.75, 3, 0x14),
                K("a", "A", 2, 1.75, 1.0, 9, 0x41), K("s", "S", 2, 2.75, 1.0, 15, 0x53), K("d", "D", 2, 3.75, 1.0, 21, 0x44),
                K("f", "F", 2, 4.75, 1.0, 27, 0x46), K("g", "G", 2, 5.75, 1.0, 33, 0x47), K("h", "H", 2, 6.75, 1.0, 39, 0x48),
                K("j", "J", 2, 7.75, 1.0, 45, 0x4A), K("k", "K", 2, 8.75, 1.0, 51, 0x4B), K("l", "L", 2, 9.75, 1.0, 57, 0x4C),
                K("semicolon", ";", 2, 10.75, 1.0, 63, 0xBA), K("quote", "'", 2, 11.75, 1.0, 69, 0xDE),
                K("enter", "Enter", 2, 12.75, 2.25, 81, 0x0D),
                // Row 3
                K("lshift", "Shift", 3, 0.0, 2.25, 4, 0xA0),
                K("z", "Z", 3, 2.25, 1.0, 10, 0x5A), K("x", "X", 3, 3.25, 1.0, 16, 0x58), K("c", "C", 3, 4.25, 1.0, 22, 0x43),
                K("v", "V", 3, 5.25, 1.0, 28, 0x56), K("b", "B", 3, 6.25, 1.0, 34, 0x42), K("n", "N", 3, 7.25, 1.0, 40, 0x4E),
                K("m", "M", 3, 8.25, 1.0, 46, 0x4D), K("comma", ",", 3, 9.25, 1.0, 52, 0xBC), K("period", ".", 3, 10.25, 1.0, 58, 0xBE),
                K("slash", "/", 3, 11.25, 1.0, 64, 0xBF), K("rshift", "Shift", 3, 12.25, 2.75, 82, 0xA1),
                // Row 4
                K("lctrl", "Ctrl", 4, 0.0, 1.25, 5, 0xA2), K("win", "Win", 4, 1.25, 1.25, 11, 0x5B), K("lalt", "Alt", 4, 2.5, 1.25, 17, 0xA4),
                K("space", "Space", 4, 3.75, 6.25, 35, 0x20),
                K("ralt", "Alt", 4, 10.0, 1.25, 53, 0xA5), K("fn", "Fn", 4, 11.25, 1.25, 59, 0xFF),
                K("menu", "Menu", 4, 12.5, 1.25, 65, 0x5D), K("rctrl", "Ctrl", 4, 13.75, 1.25, 71, 0xA3)
            };
            Keys = list.ToArray();
            for (int i = 0; i < Keys.Length; i++)
            {
                KeyIdToIndex[Keys[i].Id] = i;
                VkToKeyIndex[Keys[i].Vk] = i;
            }
            // Standard shift/ctrl/alt aliases
            VkToKeyIndex[0x10] = KeyIdToIndex["lshift"];
            VkToKeyIndex[0x11] = KeyIdToIndex["lctrl"];
            VkToKeyIndex[0x12] = KeyIdToIndex["lalt"];
        }

        private static KeyDef K(string id, string label, int row, double col, double w, int pos, int vk)
        {
            double cx = col + w * 0.5;
            double cy = row + 0.5;
            double dx = cx - 7.5;
            double dy = (cy - 2.5) * 2.0;
            return new KeyDef
            {
                Id = id, Label = label, Row = row, Col = col, Width = w, Pos = pos,
                ByteOff = 4 + pos * 3,
                Cx = cx, Cy = cy,
                ColIdx = Math.Max(0, Math.Min(13, (int)Math.Round(col))),
                Angle = Math.Atan2(dy, dx),
                Dist = Math.Sqrt(dx * dx + dy * dy),
                Vk = vk
            };
        }

        private readonly object _sync = new object();
        private IntPtr _handleCol08 = IntPtr.Zero;
        private string _pathCol08;
        private string _pathCol05;
        private string _pathCol06;
        private readonly byte[] _packetBuf = new byte[382];
        public bool IsConnected { get; private set; }

        public K617HardwareDriver()
        {
            _packetBuf[0] = 0x08;
            _packetBuf[1] = 0x0A;
            _packetBuf[2] = 0x7A;
            _packetBuf[3] = 0x01;
            DiscoverEndpoints();
        }

        public bool DiscoverEndpoints()
        {
            try
            {
                Guid hidGuid;
                NativeMethods.HidD_GetHidGuid(out hidGuid);
                IntPtr devInfo = NativeMethods.SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE);
                if (devInfo == IntPtr.Zero || devInfo == new IntPtr(-1))
                {
                    IsConnected = false;
                    return false;
                }

                try
                {
                    var ifData = new NativeMethods.SP_DEVICE_INTERFACE_DATA();
                    ifData.cbSize = Marshal.SizeOf(ifData);
                    uint idx = 0;
                    while (NativeMethods.SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref hidGuid, idx, ref ifData))
                    {
                        idx++;
                        var detail = new NativeMethods.SP_DEVICE_INTERFACE_DETAIL_DATA();
                        detail.cbSize = (IntPtr.Size == 8) ? 8 : (4 + Marshal.SystemDefaultCharSize);
                        int reqSize;
                        if (NativeMethods.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, ref detail, Marshal.SizeOf(detail), out reqSize, IntPtr.Zero))
                        {
                            string p = detail.DevicePath ?? "";
                            string pLower = p.ToLowerInvariant();
                            if (pLower.Contains("vid_258a") && pLower.Contains("pid_0049"))
                            {
                                if (pLower.Contains("col08")) _pathCol08 = p;
                                else if (pLower.Contains("col05")) _pathCol05 = p;
                                else if (pLower.Contains("col06")) _pathCol06 = p;
                            }
                        }
                    }
                }
                finally
                {
                    NativeMethods.SetupDiDestroyDeviceInfoList(devInfo);
                }

                IsConnected = !string.IsNullOrEmpty(_pathCol08);
                return IsConnected;
            }
            catch
            {
                IsConnected = false;
                return false;
            }
        }

        private bool EnsureCol08Open()
        {
            if (_handleCol08 != IntPtr.Zero && _handleCol08 != new IntPtr(-1))
                return true;
            if (string.IsNullOrEmpty(_pathCol08) && !DiscoverEndpoints())
                return false;

            _handleCol08 = NativeMethods.CreateFile(
                _pathCol08,
                0,
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                IntPtr.Zero,
                NativeMethods.OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (_handleCol08 == IntPtr.Zero || _handleCol08 == new IntPtr(-1))
            {
                _handleCol08 = IntPtr.Zero;
                IsConnected = false;
                return false;
            }
            IsConnected = true;
            return true;
        }

        private void CloseCol08()
        {
            if (_handleCol08 != IntPtr.Zero && _handleCol08 != new IntPtr(-1))
            {
                try { NativeMethods.CloseHandle(_handleCol08); } catch { }
            }
            _handleCol08 = IntPtr.Zero;
        }

        public bool SendPerKeyColors(byte[] keyR, byte[] keyG, byte[] keyB)
        {
            lock (_sync)
            {
                if (!EnsureCol08Open()) return false;
                for (int i = 0; i < Keys.Length; i++)
                {
                    int off = Keys[i].ByteOff;
                    _packetBuf[off] = keyR[i];
                    _packetBuf[off + 1] = keyG[i];
                    _packetBuf[off + 2] = keyB[i];
                }
                if (!NativeMethods.HidD_SetFeature(_handleCol08, _packetBuf, 382))
                {
                    CloseCol08();
                    return false;
                }
                return true;
            }
        }

        public bool SendSolidColor(byte r, byte g, byte b)
        {
            lock (_sync)
            {
                if (!EnsureCol08Open()) return false;
                for (int i = 0; i < Keys.Length; i++)
                {
                    int off = Keys[i].ByteOff;
                    _packetBuf[off] = r;
                    _packetBuf[off + 1] = g;
                    _packetBuf[off + 2] = b;
                }
                if (!NativeMethods.HidD_SetFeature(_handleCol08, _packetBuf, 382))
                {
                    CloseCol08();
                    return false;
                }
                return true;
            }
        }

        public bool SendFirmwareEffect(string baseDir, string effectName, byte r, byte g, byte b, int speed, int brightness)
        {
            string path = System.IO.Path.Combine(baseDir, "templates", effectName + ".json");
            if (!File.Exists(path)) return false;

            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = 1048576 };
                var rawFrames = serializer.Deserialize<int[][]>(File.ReadAllText(path));
                if (rawFrames == null || rawFrames.Length < 5) return false;

                byte[][] frames = new byte[rawFrames.Length][];
                for (int i = 0; i < rawFrames.Length; i++)
                {
                    frames[i] = new byte[rawFrames[i].Length];
                    for (int j = 0; j < rawFrames[i].Length; j++)
                        frames[i][j] = (byte)(rawFrames[i][j] & 0xFF);
                }

                frames[1][29] = r;
                frames[1][30] = g;
                frames[1][31] = b;

                int sNib = Math.Max(1, Math.Min(4, speed));
                int bNib = Math.Max(1, Math.Min(4, brightness));
                byte packed = (byte)((sNib << 4) | bNib);
                frames[4][69] = packed;
                frames[4][71] = packed;

                lock (_sync)
                {
                    CloseCol08();
                    if (string.IsNullOrEmpty(_pathCol05) || string.IsNullOrEmpty(_pathCol06))
                    {
                        if (!DiscoverEndpoints()) return false;
                    }

                    IntPtr h05 = NativeMethods.CreateFile(_pathCol05, 0, NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE, IntPtr.Zero, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero);
                    IntPtr h06 = NativeMethods.CreateFile(_pathCol06, 0, NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE, IntPtr.Zero, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero);
                    try
                    {
                        if (h05 == IntPtr.Zero || h05 == new IntPtr(-1) || h06 == IntPtr.Zero || h06 == new IntPtr(-1))
                            return false;

                        NativeMethods.HidD_SetFeature(h05, frames[0], frames[0].Length);
                        Thread.Sleep(5);
                        for (int idx = 1; idx < frames.Length; idx++)
                        {
                            NativeMethods.HidD_SetFeature(h06, frames[idx], frames[idx].Length);
                            Thread.Sleep(5);
                        }
                        return true;
                    }
                    finally
                    {
                        if (h05 != IntPtr.Zero && h05 != new IntPtr(-1)) NativeMethods.CloseHandle(h05);
                        if (h06 != IntPtr.Zero && h06 != new IntPtr(-1)) NativeMethods.CloseHandle(h06);
                    }
                }
            }
            catch { return false; }
        }

        public void Dispose()
        {
            lock (_sync) { CloseCol08(); }
        }
    }
}
