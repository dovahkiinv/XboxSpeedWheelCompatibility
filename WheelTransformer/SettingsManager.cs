using System;
using System.IO;
using System.Text.Json;

namespace XboxWheelCompatibility.WheelTransformer
{
    public static class SettingsManager
    {
        public const double MinSensitivity = 0.1;
        public const double MaxSensitivity = 3.0;
        public const double DefaultSensitivity = 1.0;

        private static readonly object FileLock = new();
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "XboxWheelCompatibility"
        );
        private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

        private static double _sensitivity = DefaultSensitivity;
        private static bool _loaded = false;

        public static double Sensitivity
        {
            get
            {
                EnsureLoaded();
                return _sensitivity;
            }
            set
            {
                var clamped = Math.Clamp(value, MinSensitivity, MaxSensitivity);
                lock (FileLock)
                {
                    _sensitivity = clamped;
                    _loaded = true;
                    Save();
                }
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

                _sensitivity = Math.Clamp(data.Sensitivity, MinSensitivity, MaxSensitivity);
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
                var json = JsonSerializer.Serialize(new SettingsData { Sensitivity = _sensitivity });
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
        }
    }
}
