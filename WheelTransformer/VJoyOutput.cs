using System;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Gaming.Input;

namespace XboxWheelCompatibility.WheelTransformer
{
    /// <summary>
    /// Virtual DirectInput wheel through the vJoy driver (https://github.com/BrunnerInnovation/vJoy/releases
    /// or the original jshafer817/vJoy). Games see a device with separate axes:
    ///   X = steering (full axis = half of the configured rotation angle to each side, e.g. ±90° at 180°),
    ///   Y = throttle, Z = brake, buttons 1-14.
    /// Configure the vJoy device in "Configure vJoy" with axes X, Y, Z and at least 14 buttons.
    /// </summary>
    public static class VJoyOutput
    {
        private const uint HID_USAGE_X = 0x30;
        private const uint HID_USAGE_Y = 0x31;
        private const uint HID_USAGE_Z = 0x32;

        private enum VjdStat { Own = 0, Free = 1, Busy = 2, Missing = 3, Unknown = 4 }

        [DllImport("vJoyInterface.dll")] private static extern bool vJoyEnabled();
        [DllImport("vJoyInterface.dll")] private static extern VjdStat GetVJDStatus(uint rID);
        [DllImport("vJoyInterface.dll")] private static extern bool AcquireVJD(uint rID);
        [DllImport("vJoyInterface.dll")] private static extern void RelinquishVJD(uint rID);
        [DllImport("vJoyInterface.dll")] private static extern bool ResetVJD(uint rID);
        [DllImport("vJoyInterface.dll")] private static extern bool SetAxis(int value, uint rID, uint axis);
        [DllImport("vJoyInterface.dll")] private static extern bool SetBtn(bool value, uint rID, byte nBtn);
        [DllImport("vJoyInterface.dll")] private static extern bool GetVJDAxisExist(uint rID, uint axis);
        [DllImport("vJoyInterface.dll")] private static extern bool GetVJDAxisMax(uint rID, uint axis, ref int max);
        [DllImport("vJoyInterface.dll")] private static extern int GetVJDButtonNumber(uint rID);

        private static bool _libraryLoaded;
        private static uint _deviceId;
        private static int _axisMax = 32767;
        private static int _buttonCount;
        private static bool _hasY, _hasZ;

        public static bool Connected { get; private set; }
        public static string Status { get; private set; } = "Off";

        private static bool EnsureLibrary()
        {
            if (_libraryLoaded) return true;

            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string[] candidates =
            {
                Path.Combine(AppContext.BaseDirectory, "vJoyInterface.dll"),
                Path.Combine(pf, "vJoy", "x64", "vJoyInterface.dll"),
                @"C:\Program Files\vJoy\x64\vJoyInterface.dll",
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c) && NativeLibrary.TryLoad(c, out _))
                {
                    _libraryLoaded = true;
                    DiagnosticsLog.Write("vJoy: loaded " + c);
                    return true;
                }
            }
            Status = "vJoy not installed (vJoyInterface.dll not found). Install vJoy, configure device 1 with axes X, Y, Z and 14+ buttons, reboot.";
            return false;
        }

        public static string Connect(int deviceId)
        {
            Disconnect();
            try
            {
                if (!EnsureLibrary()) { DiagnosticsLog.Write("vJoy: " + Status); return Status; }

                if (!vJoyEnabled())
                {
                    Status = "vJoy driver is installed but disabled. Enable it in 'Configure vJoy'.";
                    DiagnosticsLog.Write("vJoy: " + Status);
                    return Status;
                }

                uint id = (uint)Math.Clamp(deviceId, 1, 16);
                var stat = GetVJDStatus(id);
                if (stat == VjdStat.Missing || stat == VjdStat.Unknown)
                {
                    Status = $"vJoy device {id} is not configured. Open 'Configure vJoy', enable device {id} with axes X, Y, Z and 14+ buttons.";
                    DiagnosticsLog.Write("vJoy: " + Status);
                    return Status;
                }
                if (stat == VjdStat.Busy)
                {
                    Status = $"vJoy device {id} is used by another program (e.g. x360ce, Joystick Gremlin). Close it or pick another device.";
                    DiagnosticsLog.Write("vJoy: " + Status);
                    return Status;
                }
                if (!AcquireVJD(id))
                {
                    Status = $"Could not acquire vJoy device {id}.";
                    DiagnosticsLog.Write("vJoy: " + Status);
                    return Status;
                }

                ResetVJD(id);
                _deviceId = id;
                int max = 0;
                _axisMax = GetVJDAxisMax(id, HID_USAGE_X, ref max) && max > 0 ? max : 32767;
                _hasY = GetVJDAxisExist(id, HID_USAGE_Y);
                _hasZ = GetVJDAxisExist(id, HID_USAGE_Z);
                _buttonCount = GetVJDButtonNumber(id);
                Connected = true;

                Status = $"vJoy wheel active on device {id} (X = steering{(_hasY ? ", Y = throttle" : "")}{(_hasZ ? ", Z = brake" : "")}, {_buttonCount} buttons).";
                if (!_hasY || !_hasZ || _buttonCount < 14)
                    Status += " Tip: enable axes X, Y, Z and 14 buttons in 'Configure vJoy' for full mapping.";
                DiagnosticsLog.Write("vJoy: " + Status);
                return Status;
            }
            catch (Exception ex)
            {
                Connected = false;
                Status = "vJoy error: " + ex.GetType().Name + ": " + ex.Message;
                DiagnosticsLog.Write(Status);
                return Status;
            }
        }

        public static void Disconnect()
        {
            if (!Connected) return;
            try
            {
                ResetVJD(_deviceId);
                RelinquishVJD(_deviceId);
            }
            catch { }
            Connected = false;
            Status = "Off";
            DiagnosticsLog.Write("vJoy: released device " + _deviceId);
        }

        private static int ToCenteredAxis(double v) => (int)Math.Round((Math.Clamp(v, -1.0, 1.0) + 1.0) / 2.0 * _axisMax);
        private static int ToPedalAxis(double v) => (int)Math.Round(Math.Clamp(v, 0.0, 1.0) * _axisMax);

        public static void Submit(double steering, double throttle, double brake, GamepadButtons b)
        {
            if (!Connected) return;
            uint id = _deviceId;

            SetAxis(ToCenteredAxis(steering), id, HID_USAGE_X);
            if (_hasY) SetAxis(ToPedalAxis(throttle), id, HID_USAGE_Y);
            if (_hasZ) SetAxis(ToPedalAxis(brake), id, HID_USAGE_Z);

            void Btn(byte n, GamepadButtons flag)
            {
                if (n <= _buttonCount) SetBtn(b.HasFlag(flag), id, n);
            }
            Btn(1, GamepadButtons.A);
            Btn(2, GamepadButtons.B);
            Btn(3, GamepadButtons.X);
            Btn(4, GamepadButtons.Y);
            Btn(5, GamepadButtons.LeftShoulder);   // gear down
            Btn(6, GamepadButtons.RightShoulder);  // gear up
            Btn(7, GamepadButtons.View);
            Btn(8, GamepadButtons.Menu);
            Btn(9, GamepadButtons.LeftThumbstick);
            Btn(10, GamepadButtons.RightThumbstick);
            Btn(11, GamepadButtons.DPadUp);
            Btn(12, GamepadButtons.DPadDown);
            Btn(13, GamepadButtons.DPadLeft);
            Btn(14, GamepadButtons.DPadRight);
        }
    }
}
