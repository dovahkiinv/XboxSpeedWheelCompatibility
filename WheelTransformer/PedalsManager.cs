using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace XboxWheelCompatibility.WheelTransformer
{
    /// <summary>
    /// Separate pedal set (any DirectInput joystick, e.g. "Steering Wheel" VID_05B8&amp;PID_0A20) read via WinMM.
    /// Typical combined pedals: both pedals on ONE axis — rest = center, one pedal moves it up, the other down.
    /// Output: throttle / brake 0..1 which are merged (max) with the Speed Wheel triggers.
    /// </summary>
    public static class PedalsManager
    {
        // ---------- WinMM ----------
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct JOYCAPSW
        {
            public ushort wMid;
            public ushort wPid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szPname;
            public uint wXmin, wXmax, wYmin, wYmax, wZmin, wZmax;
            public uint wNumButtons, wPeriodMin, wPeriodMax;
            public uint wRmin, wRmax, wUmin, wUmax, wVmin, wVmax;
            public uint wCaps, wMaxAxes, wNumAxes, wMaxButtons;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szRegKey;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szOEMVxD;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOYINFOEX
        {
            public uint dwSize, dwFlags, dwXpos, dwYpos, dwZpos, dwRpos, dwUpos, dwVpos;
            public uint dwButtons, dwButtonNumber, dwPOV, dwReserved1, dwReserved2;
        }

        private const uint JOY_RETURNALL = 0x000000FF;
        private const uint JOYERR_NOERROR = 0;

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern uint joyGetDevCapsW(UIntPtr uJoyID, out JOYCAPSW pjc, uint cbjc);

        [DllImport("winmm.dll")]
        private static extern uint joyGetPosEx(uint uJoyID, ref JOYINFOEX pji);

        // ---------- State ----------
        public static readonly string[] AxisNames = { "X", "Y", "Z", "R (X rot)", "U (Y rot)", "V" };

        public sealed class PedalDevice
        {
            public int JoyId;
            public ushort Vid, Pid;
            public string Name = "";
            public string Key => $"{Vid:X4}:{Pid:X4}";
            public uint[] Min = new uint[6];
            public uint[] Max = new uint[6];
            public int NumAxes;
            public override string ToString() => $"{Name} ({Key}, joystick {JoyId + 1}, {NumAxes} axes)";
        }

        private static readonly object Lock = new();
        private static List<PedalDevice> _devices = new();
        private static PedalDevice? _active;
        private static readonly Stopwatch ScanTimer = Stopwatch.StartNew();
        private static long _lastScanMs = -100000;
        private static string _lastLoggedDevice = "";

        public static double[] RawAxes { get; private set; } = new double[6];
        public static double Throttle { get; private set; }
        public static double Brake { get; private set; }
        public static bool Connected => _active != null;
        public static string ActiveName => _active?.ToString() ?? "";

        public static IReadOnlyList<PedalDevice> Devices { get { lock (Lock) return _devices.ToList(); } }

        private static string LookupOemName(ushort vid, ushort pid, string fallback)
        {
            string sub = $@"System\CurrentControlSet\Control\MediaProperties\PrivateProperties\Joystick\OEM\VID_{vid:X4}&PID_{pid:X4}";
            foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using var key = root.OpenSubKey(sub);
                    if (key?.GetValue("OEMName") is string n && !string.IsNullOrWhiteSpace(n)) return n;
                }
                catch { }
            }
            return string.IsNullOrWhiteSpace(fallback) ? "Joystick" : fallback;
        }

        /// <summary>Enumerates WinMM joysticks (max every 2 s unless forced).</summary>
        public static void Scan(bool force = false)
        {
            long now = ScanTimer.ElapsedMilliseconds;
            if (!force && now - _lastScanMs < 2000) return;
            _lastScanMs = now;

            var list = new List<PedalDevice>();
            for (uint id = 0; id < 16; id++)
            {
                var info = new JOYINFOEX { dwSize = (uint)Marshal.SizeOf<JOYINFOEX>(), dwFlags = JOY_RETURNALL };
                if (joyGetPosEx(id, ref info) != JOYERR_NOERROR) continue;
                if (joyGetDevCapsW((UIntPtr)id, out var caps, (uint)Marshal.SizeOf<JOYCAPSW>()) != JOYERR_NOERROR) continue;

                list.Add(new PedalDevice
                {
                    JoyId = (int)id,
                    Vid = caps.wMid,
                    Pid = caps.wPid,
                    Name = LookupOemName(caps.wMid, caps.wPid, caps.szPname),
                    NumAxes = (int)caps.wNumAxes,
                    Min = new[] { caps.wXmin, caps.wYmin, caps.wZmin, caps.wRmin, caps.wUmin, caps.wVmin },
                    Max = new[] { caps.wXmax, caps.wYmax, caps.wZmax, caps.wRmax, caps.wUmax, caps.wVmax },
                });
            }

            lock (Lock)
            {
                _devices = list;
                string wanted = SettingsManager.PedalsDevice;
                _active = string.IsNullOrEmpty(wanted)
                    // Auto: first non-Microsoft (045E), non-vJoy (1234) device.
                    ? list.FirstOrDefault(d => d.Vid != 0x045E && d.Vid != 0x1234)
                    : list.FirstOrDefault(d => string.Equals(d.Key, wanted, StringComparison.OrdinalIgnoreCase));
            }

            string now2 = _active?.ToString() ?? "none";
            if (now2 != _lastLoggedDevice)
            {
                _lastLoggedDevice = now2;
                DiagnosticsLog.Write("Pedals: joysticks = [" + string.Join("; ", list.Select(d => d.ToString())) + "], active = " + now2);
            }
        }

        private static double Norm(uint v, uint min, uint max) => max > min ? Math.Clamp((v - (double)min) / (max - min), 0.0, 1.0) : 0.5;

        private static double ApplyDeadZone(double v, double dz) => v <= dz ? 0.0 : Math.Min(1.0, (v - dz) / (1.0 - dz));

        /// <summary>Reads the pedals. Returns false if disabled or not connected.</summary>
        /// <summary>Reads raw axes of the active pedal device (also used by the GUI before enabling).</summary>
        public static bool ReadRaw()
        {
            Scan();
            PedalDevice? dev;
            lock (Lock) dev = _active;
            if (dev == null) { RawAxes = new double[6]; return false; }

            var info = new JOYINFOEX { dwSize = (uint)Marshal.SizeOf<JOYINFOEX>(), dwFlags = JOY_RETURNALL };
            if (joyGetPosEx((uint)dev.JoyId, ref info) != JOYERR_NOERROR)
            {
                _lastScanMs = -100000; // device gone -> rescan next time
                return false;
            }

            uint[] pos = { info.dwXpos, info.dwYpos, info.dwZpos, info.dwRpos, info.dwUpos, info.dwVpos };
            var raw = new double[6];
            for (int i = 0; i < 6; i++) raw[i] = Norm(pos[i], dev.Min[i], dev.Max[i]);
            RawAxes = raw;
            return true;
        }

        public static bool Update()
        {
            if (!SettingsManager.PedalsEnabled) { Throttle = Brake = 0; return false; }
            if (!ReadRaw()) { Throttle = Brake = 0; return false; }
            var raw = RawAxes;

            int axis = Math.Clamp(SettingsManager.PedalsAxis, 0, 5);
            double value = raw[axis];
            double center = Math.Clamp(SettingsManager.PedalsCenter, 0.05, 0.95);
            double dz = SettingsManager.PedalsDeadZone;

            // Up (towards 0) and down (towards 1) from the rest position, each scaled to 0..1.
            double up = value < center ? (center - value) / center : 0.0;
            double down = value > center ? (value - center) / (1.0 - center) : 0.0;
            up = ApplyDeadZone(up, dz);
            down = ApplyDeadZone(down, dz);

            double throttle = SettingsManager.PedalsSwap ? down : up;
            double brake = SettingsManager.PedalsSwap ? up : down;

            // Pedal range: e.g. 0.5 = 100 % already at half of the pedal travel.
            Throttle = Math.Min(1.0, throttle / Math.Clamp(SettingsManager.ThrottleRange, 0.1, 1.0));
            Brake = Math.Min(1.0, brake / Math.Clamp(SettingsManager.BrakeRange, 0.1, 1.0));
            return true;
        }

        /// <summary>Stores the current axis position as the rest (center) position.</summary>
        public static string CalibrateCenter()
        {
            PedalDevice? dev;
            lock (Lock) dev = _active;
            if (dev == null) return "No pedal device connected.";
            double v = RawAxes[Math.Clamp(SettingsManager.PedalsAxis, 0, 5)];
            SettingsManager.PedalsCenter = v;
            string msg = string.Format(CultureInfo.InvariantCulture, "Pedal center calibrated at {0:0.000} (axis {1}).", v, AxisNames[SettingsManager.PedalsAxis]);
            DiagnosticsLog.Write("Pedals: " + msg);
            return msg;
        }
    }
}
