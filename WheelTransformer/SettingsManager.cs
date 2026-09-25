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

        // ----- Separate pedals -----
        public static bool PedalsEnabled { get { EnsureLoaded(); return _data.PedalsEnabled; } set => Update(d => d.PedalsEnabled = value); }
        /// <summary>"VID:PID" of the pedal device, empty = auto (first non-Microsoft joystick).</summary>
        public static string PedalsDevice { get { EnsureLoaded(); return _data.PedalsDevice ?? ""; } set => Update(d => d.PedalsDevice = value ?? ""); }
        public static int PedalsAxis { get { EnsureLoaded(); return _data.PedalsAxis; } set => Update(d => d.PedalsAxis = Math.Clamp(value, 0, 5)); }
        public static bool PedalsSwap { get { EnsureLoaded(); return _data.PedalsSwap; } set => Update(d => d.PedalsSwap = value); }
        public static double PedalsDeadZone { get { EnsureLoaded(); return _data.PedalsDeadZone; } set => Update(d => d.PedalsDeadZone = Math.Clamp(value, 0.0, 0.5)); }
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
                if (!Enum.IsDefined(data.Output)) data.Output = OutputMode.Auto;
                if (!Enum.IsDefined(data.DeviceMode)) data.DeviceMode = DeviceSelectionMode.Auto;
                if (!Enum.IsDefined(data.SteeringAxis)) data.SteeringAxis = SteeringAxisSource.Auto;
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
            public bool VJoyEnabled { get; set; } = false;
            public int VJoyDeviceId { get; set; } = 1;
            public bool PedalsEnabled { get; set; } = false;
            public string PedalsDevice { get; set; } = "";
            public int PedalsAxis { get; set; } = 1;
            public bool PedalsSwap { get; set; } = false;
            public double PedalsDeadZone { get; set; } = 0.05;
            public double PedalsCenter { get; set; } = 0.5;
            public SteeringAxisSource SteeringAxis { get; set; } = SteeringAxisSource.Auto;
            public bool InvertSteering { get; set; } = false;
            public bool DiagnosticLogging { get; set; } = true;
        }
    }
}
