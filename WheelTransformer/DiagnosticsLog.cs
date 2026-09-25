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

        /// <summary>Per-session log next to the service executable (overwritten on every service start).</summary>
        public static readonly string OutputLogPath = Path.Combine(AppContext.BaseDirectory, "Output.log");
        private static bool _outputLogStarted;

        /// <summary>Optional sink (e.g. ILogger) set by the service.</summary>
        public static Action<string>? ExternalSink { get; set; }

        public static void Write(string message)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}";

            lock (Lock)
            {
                RecentLines.AddLast(line);
                while (RecentLines.Count > MaxMemoryLines) RecentLines.RemoveFirst();

                WriteOutputLog(line);

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

        private static void WriteOutputLog(string line)
        {
            try
            {
                if (!_outputLogStarted)
                {
                    _outputLogStarted = true;
                    File.WriteAllText(OutputLogPath,
                        "Xbox Wheel Compatibility - Output.log" + Environment.NewLine +
                        $"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}  OS: {Environment.OSVersion}  64-bit: {Environment.Is64BitProcess}" + Environment.NewLine +
                        "----------------------------------------------------------------" + Environment.NewLine);
                }
                var info = new FileInfo(OutputLogPath);
                if (info.Exists && info.Length > 2 * MaxFileBytes) return;
                File.AppendAllText(OutputLogPath, line + Environment.NewLine);
            }
            catch
            {
                // Folder may be read-only (e.g. Program Files without admin) - diagnostics.log still works.
            }
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
