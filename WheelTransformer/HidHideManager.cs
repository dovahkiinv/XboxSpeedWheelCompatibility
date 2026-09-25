using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace XboxWheelCompatibility.WheelTransformer
{
    /// <summary>
    /// Hides the real Xbox 360 Wireless Receiver devices (Speed Wheel: "Steering Wheel" in joy.cpl and
    /// "Controller (Xbox 360 Wireless Receiver for Windows)") from games using the HidHide driver,
    /// so games only see the virtual ViGEm controller. This service is whitelisted and keeps reading it.
    /// Requires HidHide: https://github.com/nefarius/HidHide/releases
    /// </summary>
    public static class HidHideManager
    {
        // Xbox 360 Wireless Receiver for Windows. NOT the ViGEm pad (VID_045E&PID_028E).
        private static readonly string[] DeviceIdPatterns = { "VID_045E&PID_0719", "VID_045E&PID_0291", "VID_045E&PID_02A9" };

        private static readonly object Lock = new();
        private static readonly List<string> HiddenByUs = new();

        public static string Status { get; private set; } = "Off";
        public static bool Active { get; private set; }

        public static string? FindCli()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Nefarius Software Solutions", "HidHide", "x64", "HidHideCLI.exe"),
                @"C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe",
                @"C:\Program Files\Nefarius Software Solutions\HidHide\HidHideCLI.exe",
            };
            return candidates.FirstOrDefault(File.Exists);
        }

        private static (int code, string output) Run(string exe, string args, int timeoutMs = 15000)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi)!;
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return (-1, "timeout"); }
            return (p.ExitCode, (stdout + stderr).Trim());
        }

        /// <summary>Instance IDs of all present devices belonging to the Xbox 360 receiver (HID and USB/XUSB nodes).</summary>
        private static List<string> FindReceiverInstanceIds()
        {
            string filter = string.Join("|", DeviceIdPatterns);
            string script = "Get-PnpDevice -PresentOnly | Where-Object { $_.InstanceId -match '" + filter + "' } | ForEach-Object { $_.InstanceId }";
            var (_, output) = Run("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + script + "\"");
            return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => DeviceIdPatterns.Any(p => l.Contains(p, StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string Enable()
        {
            lock (Lock)
            {
                try
                {
                    var cli = FindCli();
                    if (cli == null)
                    {
                        Status = "HidHide not installed. Get it from github.com/nefarius/HidHide/releases, reboot, then enable again.";
                        Active = false;
                        DiagnosticsLog.Write("HidHide: " + Status);
                        return Status;
                    }

                    string self = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "WheelCompatibilityService.exe");
                    var reg = Run(cli, $"--app-reg \"{self}\"");
                    DiagnosticsLog.Write($"HidHide: app-reg {self} -> {reg.code} {reg.output}");

                    var ids = FindReceiverInstanceIds();
                    if (ids.Count == 0)
                    {
                        Status = "No Xbox 360 receiver devices found to hide.";
                        DiagnosticsLog.Write("HidHide: " + Status);
                    }
                    foreach (var id in ids)
                    {
                        var r = Run(cli, $"--dev-hide \"{id}\"");
                        DiagnosticsLog.Write($"HidHide: dev-hide {id} -> {r.code} {r.output}");
                        if (!HiddenByUs.Contains(id, StringComparer.OrdinalIgnoreCase)) HiddenByUs.Add(id);
                    }

                    var on = Run(cli, "--cloak-on");
                    DiagnosticsLog.Write($"HidHide: cloak-on -> {on.code} {on.output}");

                    Active = ids.Count > 0;
                    if (Active) Status = $"Real receiver hidden from games ({ids.Count} device nodes). Reconnect the wheel or restart the game if it still sees it.";
                    return Status;
                }
                catch (Exception ex)
                {
                    Status = "HidHide error: " + ex.GetType().Name + ": " + ex.Message;
                    Active = false;
                    DiagnosticsLog.Write(Status);
                    return Status;
                }
            }
        }

        public static string Disable()
        {
            lock (Lock)
            {
                try
                {
                    var cli = FindCli();
                    if (cli == null) { Status = "Off"; Active = false; return Status; }

                    var ids = HiddenByUs.Count > 0 ? HiddenByUs.ToList() : FindReceiverInstanceIds();
                    foreach (var id in ids)
                    {
                        var r = Run(cli, $"--dev-unhide \"{id}\"");
                        DiagnosticsLog.Write($"HidHide: dev-unhide {id} -> {r.code} {r.output}");
                    }
                    HiddenByUs.Clear();

                    // Leave cloaking on if the user hides other devices; our devices are unhidden anyway.
                    Active = false;
                    Status = "Off - games see the real wheel again.";
                    DiagnosticsLog.Write("HidHide: " + Status);
                    return Status;
                }
                catch (Exception ex)
                {
                    Status = "HidHide error: " + ex.GetType().Name + ": " + ex.Message;
                    DiagnosticsLog.Write(Status);
                    return Status;
                }
            }
        }
    }
}
