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
            public DeviceSelectionMode DeviceMode { get; set; } = DeviceSelectionMode.Auto;
            public SteeringAxisSource SteeringAxis { get; set; } = SteeringAxisSource.Auto;
            public bool InvertSteering { get; set; } = false;
            public bool DiagnosticLogging { get; set; } = true;
        }
    }
}
