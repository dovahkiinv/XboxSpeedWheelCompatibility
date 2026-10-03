using System;
using System.IO;
using System.Text.Json;

namespace XboxWheelCompatibility.WheelTransformer
{
    public static class SettingsManager
    {
        public const double MinSensitivity = 0.01;
        public const double MaxSensitivity = 3.0;
        public const double DefaultSensitivity = 1.0;
        public const double MinDeadZone = 0.0;
        public const double MaxDeadZone = 0.5;
        public const double DefaultDeadZone = 0.05;
        public const double MinRotationDegrees = 90;
        public const double MaxRotationDegrees = 1080;
        public const double DefaultRotationDegrees = 180;
        public const double MinPhysicalDegrees = 30;
        public const double MaxPhysicalDegrees = 540;
        public const double DefaultPhysicalDegrees = 90;

        /// <summary>Upper bound of the anti-deadzone sliders (40 %).</summary>
        public const double MaxAntiDeadZone = 0.40;
        public const double DefaultAntiDeadZone = 0.0;

        private static readonly object FileLock = new();
        public static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "XboxWheelCompatibility"
        );
        private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

        private static SettingsData _data = new();
        private static bool _loaded = false;

        public static double Sensitivity
        {
            get { EnsureLoaded(); return _data.Sensitivity; }
            set => Update(d => d.Sensitivity = Math.Clamp(value, MinSensitivity, MaxSensitivity));
        }

        /// <summary>Steering dead zone around center (0..0.5 of full travel).</summary>
        public static double DeadZone
        {
            get { EnsureLoaded(); return _data.DeadZone; }
            set => Update(d => d.DeadZone = Math.Clamp(value, MinDeadZone, MaxDeadZone));
        }

        /// <summary>
        /// Virtual wheel rotation, lock to lock, in degrees (like 180/270/540/900/1080 on real wheels).
        /// Full lock in the game is reached at half of this angle to each side.
        /// </summary>
        public static double RotationDegrees
        {
            get { EnsureLoaded(); return _data.RotationDegrees; }
            set => Update(d => d.RotationDegrees = Math.Clamp(value, MinRotationDegrees, MaxRotationDegrees));
        }

        /// <summary>
        /// Calibration: how many degrees (to one side) the physical wheel is turned when the device reports
        /// full input (1.0). Speed Wheel: about 90.
        /// </summary>
        public static double PhysicalDegrees
        {
            get { EnsureLoaded(); return _data.PhysicalDegrees; }
            set => Update(d => d.PhysicalDegrees = Math.Clamp(value, MinPhysicalDegrees, MaxPhysicalDegrees));
        }

        // ----- Anti-deadzone -----
        // Games such as F1 25 ignore the first ~20 % of a steering axis even with linearity set to 0.
        // Anti-deadzone lifts every non-zero value by a fixed offset: sign(x) * (ad + (1 - ad) * |x|),
        // applied last (after dead zone, rotation angle and sensitivity), just before the value is sent.
        // 0 keeps the old behaviour untouched.

        /// <summary>Steering anti-deadzone, 0..0.40 of full lock.</summary>
        public static double SteeringAntiDeadZone
        {
            get { EnsureLoaded(); return _data.SteeringAntiDeadZone; }
            set => Update(d => d.SteeringAntiDeadZone = Math.Clamp(value, 0.0, MaxAntiDeadZone));
        }

        /// <summary>Throttle anti-deadzone, 0..0.40.</summary>
        public static double ThrottleAntiDeadZone
        {
            get { EnsureLoaded(); return _data.ThrottleAntiDeadZone; }
            set => Update(d => d.ThrottleAntiDeadZone = Math.Clamp(value, 0.0, MaxAntiDeadZone));
        }

        /// <summary>Brake anti-deadzone, 0..0.40.</summary>
        public static double BrakeAntiDeadZone
        {
            get { EnsureLoaded(); return _data.BrakeAntiDeadZone; }
            set => Update(d => d.BrakeAntiDeadZone = Math.Clamp(value, 0.0, MaxAntiDeadZone));
        }

        /// <summary>Hide the real Xbox 360 receiver from games with HidHide while the service runs.</summary>
        /// <summary>When hiding: also hide the XInput (XUSB) interface, not only the HID/DirectInput one.</summary>
        public static bool HideXInputInterface
        {
            get { EnsureLoaded(); return _data.HideXInputInterface; }
            set => Update(d => d.HideXInputInterface = value);
        }

        public static bool HideRealDevice
        {
            get { EnsureLoaded(); return _data.HideRealDevice; }
            set => Update(d => d.HideRealDevice = value);
        }

        /// <summary>Also output a virtual DirectInput wheel through vJoy.</summary>
        public static bool VJoyEnabled
        {
            get { EnsureLoaded(); return _data.VJoyEnabled; }
            set => Update(d => d.VJoyEnabled = value);
        }

        public static int VJoyDeviceId
        {
            get { EnsureLoaded(); return _data.VJoyDeviceId; }
            set => Update(d => d.VJoyDeviceId = Math.Clamp(value, 1, 16));
        }

        // ----- vJoy pedal layout -----
        /// <summary>Separate axes (Y = throttle, Z = brake) or one centered axis (Y).</summary>
        public static PedalAxisMode VJoyPedalAxisMode
        {
            get { EnsureLoaded(); return _data.VJoyPedalAxisMode; }
            set => Update(d => d.VJoyPedalAxisMode = Enum.IsDefined(value) ? value : PedalAxisMode.SeparateAxes);
        }

        /// <summary>Reverse the throttle axis on the vJoy wheel.</summary>
        public static bool VJoyInvertThrottle
        {
            get { EnsureLoaded(); return _data.VJoyInvertThrottle; }
            set => Update(d => d.VJoyInvertThrottle = value);
        }

        /// <summary>Reverse the brake axis on the vJoy wheel.</summary>
        public static bool VJoyInvertBrake
        {
            get { EnsureLoaded(); return _data.VJoyInvertBrake; }
            set => Update(d => d.VJoyInvertBrake = value);
        }

        // ----- Separate pedals -----
        public static bool PedalsEnabled { get { EnsureLoaded(); return _data.PedalsEnabled; } set => Update(d => d.PedalsEnabled = value); }
        /// <summary>"VID:PID" of the pedal device, empty = auto (first non-Microsoft joystick).</summary>
        public static string PedalsDevice { get { EnsureLoaded(); return _data.PedalsDevice ?? ""; } set => Update(d => d.PedalsDevice = value ?? ""); }
        public static int PedalsAxis { get { EnsureLoaded(); return _data.PedalsAxis; } set => Update(d => d.PedalsAxis = Math.Clamp(value, 0, 5)); }
        public static bool PedalsSwap { get { EnsureLoaded(); return _data.PedalsSwap; } set => Update(d => d.PedalsSwap = value); }
        public static double PedalsDeadZone { get { EnsureLoaded(); return _data.PedalsDeadZone; } set => Update(d => d.PedalsDeadZone = Math.Clamp(value, 0.0, 0.5)); }
        /// <summary>Pedal travel (after dead zone) that already gives 100 % (0.1..1.0; 0.5 = full at half press).</summary>
        public static double ThrottleRange { get { EnsureLoaded(); return _data.ThrottleRange; } set => Update(d => d.ThrottleRange = Math.Clamp(value, 0.1, 1.0)); }
        public static double BrakeRange { get { EnsureLoaded(); return _data.BrakeRange; } set => Update(d => d.BrakeRange = Math.Clamp(value, 0.1, 1.0)); }
        public static double PedalsCenter { get { EnsureLoaded(); return _data.PedalsCenter; } set => Update(d => d.PedalsCenter = Math.Clamp(value, 0.05, 0.95)); }
        /// <summary>Throttle / brake come from the wheel triggers, the separate pedals or both (max).</summary>
        public static PedalSourceMode PedalSource
        {
            get { EnsureLoaded(); return _data.PedalSource; }
            set => Update(d => d.PedalSource = Enum.IsDefined(value) ? value : PedalSourceMode.BothMax);
        }

        public static OutputMode Output
        {
            get { EnsureLoaded(); return _data.Output; }
            set => Update(d => d.Output = Enum.IsDefined(value) ? value : OutputMode.Auto);
        }

        public static DeviceSelectionMode DeviceMode
        {
            get { EnsureLoaded(); return _data.DeviceMode; }
            set => Update(d => d.DeviceMode = Enum.IsDefined(value) ? value : DeviceSelectionMode.Auto);
        }

        public static SteeringAxisSource SteeringAxis
        {
            get { EnsureLoaded(); return _data.SteeringAxis; }
            set => Update(d => d.SteeringAxis = Enum.IsDefined(value) ? value : SteeringAxisSource.Auto);
        }

        public static bool InvertSteering
        {
            get { EnsureLoaded(); return _data.InvertSteering; }
            set => Update(d => d.InvertSteering = value);
        }

        public static bool DiagnosticLogging
        {
            get { EnsureLoaded(); return _data.DiagnosticLogging; }
            set => Update(d => d.DiagnosticLogging = value);
        }

        private static void Update(Action<SettingsData> change)
        {
            EnsureLoaded();
            lock (FileLock)
            {
                change(_data);
                Save();
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;

            lock (FileLock)
            {
                if (_loaded) return;
                Load();
                _loaded = true;
            }
        }

        private static void Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;

                var json = File.ReadAllText(SettingsPath);
                var data = JsonSerializer.Deserialize<SettingsData>(json);
                if (data == null) return;

                data.Sensitivity = Math.Clamp(data.Sensitivity, MinSensitivity, MaxSensitivity);
                data.DeadZone = Math.Clamp(data.DeadZone, MinDeadZone, MaxDeadZone);
                data.RotationDegrees = Math.Clamp(data.RotationDegrees, MinRotationDegrees, MaxRotationDegrees);
                data.PhysicalDegrees = Math.Clamp(data.PhysicalDegrees, MinPhysicalDegrees, MaxPhysicalDegrees);
                data.SteeringAntiDeadZone = Math.Clamp(data.SteeringAntiDeadZone, 0.0, MaxAntiDeadZone);
                data.ThrottleAntiDeadZone = Math.Clamp(data.ThrottleAntiDeadZone, 0.0, MaxAntiDeadZone);
                data.BrakeAntiDeadZone = Math.Clamp(data.BrakeAntiDeadZone, 0.0, MaxAntiDeadZone);
                if (!Enum.IsDefined(data.Output)) data.Output = OutputMode.Auto;
                if (!Enum.IsDefined(data.DeviceMode)) data.DeviceMode = DeviceSelectionMode.Auto;
                if (!Enum.IsDefined(data.SteeringAxis)) data.SteeringAxis = SteeringAxisSource.Auto;
                if (!Enum.IsDefined(data.PedalSource)) data.PedalSource = PedalSourceMode.BothMax;
                if (!Enum.IsDefined(data.VJoyPedalAxisMode)) data.VJoyPedalAxisMode = PedalAxisMode.SeparateAxes;
                _data = data;
            }
            catch
            {
                // Corrupt or unreadable settings — fall back to defaults silently.
            }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Best effort — a service running as LocalSystem should always have access here.
            }
        }

        private class SettingsData
        {
            public double Sensitivity { get; set; } = DefaultSensitivity;
            public double DeadZone { get; set; } = DefaultDeadZone;
            public double RotationDegrees { get; set; } = DefaultRotationDegrees;
            public double PhysicalDegrees { get; set; } = DefaultPhysicalDegrees;
            public DeviceSelectionMode DeviceMode { get; set; } = DeviceSelectionMode.Auto;
            public OutputMode Output { get; set; } = OutputMode.Auto;
            public bool HideRealDevice { get; set; } = false;
            public bool HideXInputInterface { get; set; } = true;
            public bool VJoyEnabled { get; set; } = false;
            public int VJoyDeviceId { get; set; } = 1;
            public PedalAxisMode VJoyPedalAxisMode { get; set; } = PedalAxisMode.SeparateAxes;
            public bool VJoyInvertThrottle { get; set; } = false;
            public bool VJoyInvertBrake { get; set; } = false;
            public bool PedalsEnabled { get; set; } = false;
            public string PedalsDevice { get; set; } = "";
            public int PedalsAxis { get; set; } = 1;
            public bool PedalsSwap { get; set; } = false;
            public double PedalsDeadZone { get; set; } = 0.05;
            public double PedalsCenter { get; set; } = 0.5;
            public double ThrottleRange { get; set; } = 1.0;
            public double BrakeRange { get; set; } = 1.0;
            public PedalSourceMode PedalSource { get; set; } = PedalSourceMode.BothMax;
            public double SteeringAntiDeadZone { get; set; } = DefaultAntiDeadZone;
            public double ThrottleAntiDeadZone { get; set; } = DefaultAntiDeadZone;
            public double BrakeAntiDeadZone { get; set; } = DefaultAntiDeadZone;
            public SteeringAxisSource SteeringAxis { get; set; } = SteeringAxisSource.Auto;
            public bool InvertSteering { get; set; } = false;
            public bool DiagnosticLogging { get; set; } = true;
        }
    }
}
