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
        // 0719/0291/02A9 = receiver, 02A1 = wireless device connected through the receiver (the Speed Wheel).
        private static readonly string[] DeviceIdPatterns = { "VID_045E&PID_0719", "VID_045E&PID_0291", "VID_045E&PID_02A9", "VID_045E&PID_02A1" };
        private static readonly string[] NamePatterns = { "Steering Wheel", "Xbox 360 Wireless", "Wireless Receiver", "Speed Wheel" };
        // Never hide our own ViGEm controller.
        private static readonly string[] ExcludePatterns = { "PID_028E", "ViGEm", "XBOX 360 For Windows" };

        private static bool IsReceiverRelated(string text)
        {
            if (ExcludePatterns.Any(x => text.Contains(x, StringComparison.OrdinalIgnoreCase))) return false;
            if (DeviceIdPatterns.Any(x => text.Contains(x, StringComparison.OrdinalIgnoreCase))) return true;
            // Name match only for Microsoft devices (VID_045E) - other brands' "Steering Wheel" stay visible.
            return text.Contains("VID_045E", StringComparison.OrdinalIgnoreCase)
                && NamePatterns.Any(x => text.Contains(x, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Parses "HidHideCLI --dev-gaming" JSON and returns the HID instance paths and their USB base
        /// containers for receiver devices (XInput devices need both hidden).
        /// </summary>
        private static List<string> FindFromHidHide(string cli)
        {
            var result = new List<string>();
            var (code, output) = Run(cli, "--dev-gaming");
            DiagnosticsLog.Write($"HidHide: dev-gaming -> {code}{Environment.NewLine}{output}");
            int start = output.IndexOf('[');
            if (start < 0) return result;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(output.Substring(start));
                Walk(doc.RootElement, "", result);
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("HidHide: could not parse dev-gaming output: " + ex.Message);
            }
            return result;
        }

        private static void Walk(System.Text.Json.JsonElement e, string parentName, List<string> result)
        {
            if (e.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in e.EnumerateArray()) Walk(item, parentName, result);
                return;
            }
            if (e.ValueKind != System.Text.Json.JsonValueKind.Object) return;

            string name = parentName;
            if (e.TryGetProperty("friendlyName", out var fn) && fn.ValueKind == System.Text.Json.JsonValueKind.String)
                name = fn.GetString() ?? parentName;

            if (e.TryGetProperty("deviceInstancePath", out var dip) && dip.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                string path = dip.GetString() ?? "";
                string basePath = e.TryGetProperty("baseContainerDeviceInstancePath", out var b) && b.ValueKind == System.Text.Json.JsonValueKind.String ? b.GetString() ?? "" : "";
                string desc = e.TryGetProperty("description", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.String ? d.GetString() ?? "" : "";
                string all = name + " | " + desc + " | " + path + " | " + basePath;
                if (IsReceiverRelated(all))
                {
                    if (path.Length > 0) result.Add(path);
                    if (basePath.Length > 0 && !basePath.Contains("PID_028E", StringComparison.OrdinalIgnoreCase)) result.Add(basePath);
                }
            }

            foreach (var prop in e.EnumerateObject())
            {
                if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Array || prop.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
                    Walk(prop.Value, name, result);
            }
        }

        private static readonly object Lock = new();
        private static readonly List<string> HiddenByUs = new();

        public static string Status { get; private set; } = "Off";
        public static bool Active { get; private set; }

        /// <summary>Receiver parent node: USB\VID_045E&amp;PID_0719\serial (no "&amp;IG_" interface suffix).</summary>
        private static bool IsReceiverRoot(string id) =>
            id.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase)
            && !id.Contains("&IG_", StringComparison.OrdinalIgnoreCase)
            && (id.Contains("PID_0719", StringComparison.OrdinalIgnoreCase)
                || id.Contains("PID_0291", StringComparison.OrdinalIgnoreCase)
                || id.Contains("PID_02A9", StringComparison.OrdinalIgnoreCase));

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
            // Lists "InstanceId<TAB>FriendlyName<TAB>Class" of every present device; filter in C#.
            string script = "Get-PnpDevice -PresentOnly | ForEach-Object { $_.InstanceId + [char]9 + $_.FriendlyName + [char]9 + $_.Class }";
            var (_, output) = Run("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + script + "\"");
            var ids = new List<string>();
            var log = new System.Text.StringBuilder("HidHide: receiver-related PnP devices:");
            foreach (var raw in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim();
                if (!IsReceiverRelated(line)) continue;
                log.Append(Environment.NewLine).Append("    ").Append(line);
                ids.Add(line.Split('\t')[0].Trim());
            }
            DiagnosticsLog.Write(log.ToString());
            return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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

                    var all = FindReceiverInstanceIds()
                        .Concat(FindFromHidHide(cli))
                        .Where(id => !ExcludePatterns.Any(x => id.Contains(x, StringComparison.OrdinalIgnoreCase)))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    // Never hide the receiver root node (XnaComposite parent) - hiding it made games
                    // (e.g. CarX) hang during controller enumeration. Unhide it in case an older version hid it.
                    foreach (var root in all.Where(IsReceiverRoot))
                    {
                        var r = Run(cli, $"--dev-unhide \"{root}\"");
                        DiagnosticsLog.Write($"HidHide: keep receiver root visible, dev-unhide {root} -> {r.code} {r.output}");
                    }

                    bool hideXInput = SettingsManager.HideXInputInterface;
                    var ids = all
                        .Where(id => !IsReceiverRoot(id))
                        // HID\... = DirectInput view; USB\...&IG_xx = XInput (XUSB) interface of the wheel.
                        .Where(id => hideXInput || id.StartsWith("HID\\", StringComparison.OrdinalIgnoreCase))
                        .ToList();
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
                    if (Active) Status = $"Real receiver hidden from games ({ids.Count} device nodes). Close and reopen joy.cpl / the game; if still visible, turn the wheel off and on.";
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

                    var ids = HiddenByUs.Count > 0
                        ? HiddenByUs.ToList()
                        : FindReceiverInstanceIds().Concat(FindFromHidHide(cli)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
