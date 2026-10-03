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
        /// <summary>Anti-deadzone is a fraction (0 = off, 0.40 = 40 %) added on top of the output.</summary>
        public const double MinAntiDeadzone = 0.0;
        public const double MaxAntiDeadzone = 0.40;
        public const double DefaultAntiDeadzone = 0.0;

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

        /// <summary>vJoy pedal mapping: separate Y/Z axes (default) or one centred Y axis.</summary>
        public static VJoyPedalMode VJoyPedalAxisMode
        {
            get { EnsureLoaded(); return _data.VJoyPedalAxisMode; }
            set => Update(d => d.VJoyPedalAxisMode = Enum.IsDefined(value) ? value : VJoyPedalMode.SeparateAxes);
        }

        /// <summary>Invert the vJoy throttle axis (pedal at rest rests at the axis maximum).</summary>
        public static bool VJoyInvertThrottle
        {
            get { EnsureLoaded(); return _data.VJoyInvertThrottle; }
            set => Update(d => d.VJoyInvertThrottle = value);
        }

        /// <summary>Invert the vJoy brake axis (pedal at rest rests at the axis maximum).</summary>
        public static bool VJoyInvertBrake
        {
            get { EnsureLoaded(); return _data.VJoyInvertBrake; }
            set => Update(d => d.VJoyInvertBrake = value);
        }

        /// <summary>
        /// Anti-deadzone for steering (0..0.40). Applied LAST, after dead zone, rotation angle and
        /// sensitivity, right before the value is sent to ViGEm / InputInjector / vJoy:
        ///   |x| &lt; 0.001 -&gt; 0,  otherwise sign(x) * (ad + (1 - ad) * |x|).
        /// Compensates the built-in dead zone some games have (F1 25 needs about 0.15-0.20).
        /// </summary>
        public static double SteeringAntiDeadzone
        {
            get { EnsureLoaded(); return _data.SteeringAntiDeadzone; }
            set => Update(d => d.SteeringAntiDeadzone = Math.Clamp(value, MinAntiDeadzone, MaxAntiDeadzone));
        }

        /// <summary>Anti-deadzone for throttle (0..0.40), same idea but without a sign (0..1).</summary>
        public static double ThrottleAntiDeadzone
        {
            get { EnsureLoaded(); return _data.ThrottleAntiDeadzone; }
            set => Update(d => d.ThrottleAntiDeadzone = Math.Clamp(value, MinAntiDeadzone, MaxAntiDeadzone));
        }

        /// <summary>Anti-deadzone for brake (0..0.40), same idea but without a sign (0..1).</summary>
        public static double BrakeAntiDeadzone
        {
            get { EnsureLoaded(); return _data.BrakeAntiDeadzone; }
            set => Update(d => d.BrakeAntiDeadzone = Math.Clamp(value, MinAntiDeadzone, MaxAntiDeadzone));
        }

        // ----- Separate pedals -----
        /// <summary>Where throttle / brake come from: wheel triggers, separate pedals or both (max).</summary>
        public static PedalSource PedalsSource
        {
            get { EnsureLoaded(); return _data.PedalsSource; }
            set => Update(d => d.PedalsSource = Enum.IsDefined(value) ? value : PedalSource.BothMax);
        }
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
                // Old settings.json files have no anti-deadzone keys at all - the defaults above (0)
                // keep the behaviour identical to previous versions.
                data.SteeringAntiDeadzone = Math.Clamp(data.SteeringAntiDeadzone, MinAntiDeadzone, MaxAntiDeadzone);
                data.ThrottleAntiDeadzone = Math.Clamp(data.ThrottleAntiDeadzone, MinAntiDeadzone, MaxAntiDeadzone);
                data.BrakeAntiDeadzone = Math.Clamp(data.BrakeAntiDeadzone, MinAntiDeadzone, MaxAntiDeadzone);
                if (!Enum.IsDefined(data.Output)) data.Output = OutputMode.Auto;
                if (!Enum.IsDefined(data.DeviceMode)) data.DeviceMode = DeviceSelectionMode.Auto;
                if (!Enum.IsDefined(data.SteeringAxis)) data.SteeringAxis = SteeringAxisSource.Auto;
                if (!Enum.IsDefined(data.PedalsSource)) data.PedalsSource = PedalSource.BothMax;
                if (!Enum.IsDefined(data.VJoyPedalAxisMode)) data.VJoyPedalAxisMode = VJoyPedalMode.SeparateAxes;
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
            public VJoyPedalMode VJoyPedalAxisMode { get; set; } = VJoyPedalMode.SeparateAxes;
            public bool VJoyInvertThrottle { get; set; } = false;
            public bool VJoyInvertBrake { get; set; } = false;
            public double SteeringAntiDeadzone { get; set; } = DefaultAntiDeadzone;
            public double ThrottleAntiDeadzone { get; set; } = DefaultAntiDeadzone;
            public double BrakeAntiDeadzone { get; set; } = DefaultAntiDeadzone;
            public PedalSource PedalsSource { get; set; } = PedalSource.BothMax;
            public bool PedalsEnabled { get; set; } = false;
            public string PedalsDevice { get; set; } = "";
            public int PedalsAxis { get; set; } = 1;
            public bool PedalsSwap { get; set; } = false;
            public double PedalsDeadZone { get; set; } = 0.05;
            public double PedalsCenter { get; set; } = 0.5;
            public double ThrottleRange { get; set; } = 1.0;
            public double BrakeRange { get; set; } = 1.0;
            public SteeringAxisSource SteeringAxis { get; set; } = SteeringAxisSource.Auto;
            public bool InvertSteering { get; set; } = false;
            public bool DiagnosticLogging { get; set; } = true;
        }
    }
}
