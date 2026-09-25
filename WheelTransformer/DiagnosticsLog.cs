using System;
using System.Collections.Generic;
using System.IO;

namespace XboxWheelCompatibility.WheelTransformer
{
    /// <summary>
    /// Small rolling text log in %ProgramData%\XboxWheelCompatibility\diagnostics.log.
    /// Used so device detection and axis values can be checked on real hardware.
    /// </summary>
    public static class DiagnosticsLog
    {
        private const long MaxFileBytes = 1024 * 1024;
        private const int MaxMemoryLines = 200;

        private static readonly object Lock = new();
        private static readonly LinkedList<string> RecentLines = new();

        public static readonly string LogPath = Path.Combine(SettingsManager.SettingsDirectory, "diagnostics.log");

        /// <summary>Optional sink (e.g. ILogger) set by the service.</summary>
        public static Action<string>? ExternalSink { get; set; }

        public static void Write(string message)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}";

            lock (Lock)
            {
                RecentLines.AddLast(line);
                while (RecentLines.Count > MaxMemoryLines) RecentLines.RemoveFirst();

                if (SettingsManager.DiagnosticLogging)
                {
                    try
                    {
                        Directory.CreateDirectory(SettingsManager.SettingsDirectory);
                        var info = new FileInfo(LogPath);
                        if (info.Exists && info.Length > MaxFileBytes)
                        {
                            string old = LogPath + ".old";
                            if (File.Exists(old)) File.Delete(old);
                            File.Move(LogPath, old);
                        }
                        File.AppendAllText(LogPath, line + Environment.NewLine);
                    }
                    catch
                    {
                        // Logging must never break input.
                    }
                }
            }

            try { ExternalSink?.Invoke(message); } catch { }
        }

        public static string[] GetRecent(int count)
        {
            lock (Lock)
            {
                var all = new List<string>(RecentLines);
                int skip = Math.Max(0, all.Count - count);
                return all.GetRange(skip, all.Count - skip).ToArray();
            }
        }
    }
}
