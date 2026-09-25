using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Windows.Gaming.Input;

namespace XboxWheelCompatibility.WheelTransformer
{
    /// <summary>
    /// Finds the input device that drives the virtual controller.
    /// Priority (Auto mode):
    ///   1. real RacingWheel (Windows.Gaming.Input.RacingWheel) – original behaviour, unchanged,
    ///   2. Xbox 360 Wireless Speed Wheel via XInput (sub type = Wheel),
    ///   3. Windows.Gaming.Input.Gamepad fallback (wireless / Xbox 360 receiver pads only,
    ///      so the virtual injected controller is never picked up and looped back).
    /// </summary>
    public class WheelManager
    {
        private static readonly object WheelListLock = new();

        // --- Original RacingWheel API (kept) ---
        public static readonly List<RacingWheel> ActiveWheels = new();
        public static event EventHandler<RacingWheel?>? MainWheelChanged;
        public static RacingWheel? MainWheel = null;

        // --- Speed Wheel / gamepad support ---
        public static event EventHandler<InputDeviceKind>? ActiveDeviceChanged;
        public static InputDeviceKind ActiveKind { get; private set; } = InputDeviceKind.None;
        public static Gamepad? MainGamepad { get; private set; }
        public static int XInputIndex { get; private set; } = -1;
        public static string ActiveDeviceName { get; private set; } = "";

        /// <summary>Human readable description of every device seen during the last scan.</summary>
        public static string[] LastScan { get; private set; } = Array.Empty<string>();

        private static readonly Stopwatch ScanTimer = Stopwatch.StartNew();
        private static long _lastScanMs = -100000;
        private const long ScanIntervalMs = 1000;
        private static bool _initialized = false;
        private static bool _firstScanLogged = false;

        // Axis range tracking for SteeringAxisSource.Auto
        private static readonly double[] AxisMaxAbs = new double[5];

        public static void UpdateWheels()
        {
            lock (WheelListLock)
            {
                foreach (var OldWheel in ActiveWheels.ToList())
                {
                    if (RacingWheel.RacingWheels.Contains(OldWheel)) continue;

                    ActiveWheels.Remove(OldWheel);
                }

                foreach (var NewWheel in RacingWheel.RacingWheels)
                {
                    if (ActiveWheels.Contains(NewWheel)) continue;

                    ActiveWheels.Add(NewWheel);
                }

                MainWheel = ActiveWheels.Count > 0 ? ActiveWheels.Last() : null;
                MainWheelChanged?.Invoke(null, MainWheel);
            }

            SelectDevice(force: true);
        }

        /// <summary>Re-evaluates which device should be active. Cheap to call every tick.</summary>
        public static void SelectDevice(bool force = false)
        {
            long now = ScanTimer.ElapsedMilliseconds;
            if (!force && now - _lastScanMs < ScanIntervalMs) return;
            _lastScanMs = now;

            var scan = new List<string>();
            var mode = SettingsManager.DeviceMode;

            InputDeviceKind kind = InputDeviceKind.None;
            string name = "";
            Gamepad? gamepad = null;
            int xinputIndex = -1;

            // 1. Real racing wheels
            RacingWheel? wheel;
            lock (WheelListLock) { wheel = MainWheel; }
            foreach (var rw in RacingWheel.RacingWheels)
            {
                scan.Add("RacingWheel: " + SafeName(RawGameController.FromGameController(rw), "Racing wheel"));
            }
            if (mode != DeviceSelectionMode.SpeedWheelOnly && wheel != null)
            {
                kind = InputDeviceKind.RacingWheel;
                name = SafeName(RawGameController.FromGameController(wheel), "Racing wheel");
            }

            // 2. XInput slots.
            // Score: 3 = sub type Wheel (0x02); 2 = wireless, non-gamepad sub type (the Speed Wheel was seen
            // reporting 0x52 through the Xbox 360 Wireless Receiver); 1 = any wireless XInput device.
            // Wired devices are ignored so our own virtual controller is never read back.
            int bestXInput = -1, bestScore = 0;
            byte bestSubType = 0;
            for (int i = 0; i < XInputNative.MaxControllers; i++)
            {
                if (!XInputNative.TryGetCapabilities(i, out var caps)) continue;
                bool wireless = (caps.Flags & XInputNative.CapsWireless) != 0;
                int score = caps.SubType == XInputNative.SubTypeWheel ? 3
                    : wireless && caps.SubType != XInputNative.SubTypeGamepad ? 2
                    : wireless ? 1 : 0;
                scan.Add($"XInput slot {i}: type=0x{caps.Type:X2} subtype={XInputNative.SubTypeName(caps.SubType)} flags=0x{caps.Flags:X4} wireless={wireless} score={score}");
                if (score > bestScore) { bestScore = score; bestXInput = i; bestSubType = caps.SubType; }
            }
            if (!XInputNative.Available) scan.Add("XInput unavailable: " + XInputNative.LoadError);

            if (kind == InputDeviceKind.None && mode != DeviceSelectionMode.RacingWheelOnly && bestXInput >= 0)
            {
                kind = InputDeviceKind.XInputSpeedWheel;
                xinputIndex = bestXInput;
                name = bestScore >= 2
                    ? $"Xbox 360 Wireless Speed Wheel (XInput slot {bestXInput}, subtype 0x{bestSubType:X2})"
                    : $"Xbox 360 wireless controller (XInput slot {bestXInput})";
            }

            // 3. Gamepad fallback
            foreach (var pad in Gamepad.Gamepads)
            {
                RawGameController? raw = null;
                try { raw = RawGameController.FromGameController(pad); } catch { }
                bool candidate = IsPhysicalXbox360Pad(raw);
                scan.Add($"Gamepad: {SafeName(raw, "Controller")} wireless={raw?.IsWireless} vid=0x{raw?.HardwareVendorId:X4} pid=0x{raw?.HardwareProductId:X4} candidate={candidate}");

                if (kind == InputDeviceKind.None && mode != DeviceSelectionMode.RacingWheelOnly && candidate)
                {
                    kind = InputDeviceKind.Gamepad;
                    gamepad = pad;
                    name = SafeName(raw, "Controller (Xbox 360 Wireless Receiver for Windows)");
                }
            }

            LastScan = scan.ToArray();

            bool deviceChanged = kind != ActiveKind
                || xinputIndex != XInputIndex
                || !ReferenceEquals(gamepad, MainGamepad);

            ActiveKind = kind;
            XInputIndex = xinputIndex;
            MainGamepad = gamepad;
            ActiveDeviceName = name;

            if (deviceChanged || !_firstScanLogged)
            {
                _firstScanLogged = true;
                Array.Clear(AxisMaxAbs);
                var sb = new StringBuilder();
                sb.Append($"Active device -> {kind}{(name.Length > 0 ? " (" + name + ")" : "")}; mode={mode}. Scan:");
                if (scan.Count == 0) sb.Append(" nothing found");
                foreach (var line in scan) sb.Append(Environment.NewLine).Append("    ").Append(line);
                DiagnosticsLog.Write(sb.ToString());

                ActiveDeviceChanged?.Invoke(null, kind);
            }
        }

        /// <summary>
        /// Only accept gamepads that look like physical Xbox 360 wireless devices. The virtual controller
        /// created by InputInjector is also listed in Gamepad.Gamepads; it is not wireless, so this keeps
        /// us from reading back our own output.
        /// </summary>
        private static bool IsPhysicalXbox360Pad(RawGameController? raw)
        {
            if (raw == null) return false;
            if (raw.IsWireless) return true;
            string display = raw.DisplayName ?? "";
            return display.Contains("360", StringComparison.OrdinalIgnoreCase)
                && display.Contains("Wireless", StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeName(RawGameController? raw, string fallback)
        {
            try
            {
                var n = raw?.DisplayName;
                return string.IsNullOrWhiteSpace(n) ? fallback : n!;
            }
            catch { return fallback; }
        }

        /// <summary>Reads the active device and converts it to a common format. Returns null if none.</summary>
        public static UnifiedReading? ReadActive()
        {
            switch (ActiveKind)
            {
                case InputDeviceKind.RacingWheel:
                {
                    var wheel = MainWheel;
                    if (wheel == null) return null;
                    var r = wheel.GetCurrentReading();
                    return new UnifiedReading
                    {
                        Kind = InputDeviceKind.RacingWheel,
                        DeviceName = ActiveDeviceName,
                        Steering = r.Wheel,
                        Throttle = r.Throttle,
                        Brake = r.Brake,
                        Clutch = r.Clutch,
                        Handbrake = r.Handbrake,
                        WheelButtons = r.Buttons,
                        OutputButtons = MapRacingWheelButtons(r.Buttons),
                        RawButtons = (int)r.Buttons,
                    };
                }

                case InputDeviceKind.XInputSpeedWheel:
                {
                    int idx = XInputIndex;
                    if (idx < 0 || !XInputNative.TryGetState(idx, out var s)) return null;
                    var g = s.Gamepad;
                    return BuildPadReading(
                        InputDeviceKind.XInputSpeedWheel,
                        XInputNative.NormalizeThumb(g.sThumbLX), XInputNative.NormalizeThumb(g.sThumbLY),
                        XInputNative.NormalizeThumb(g.sThumbRX), XInputNative.NormalizeThumb(g.sThumbRY),
                        XInputNative.NormalizeTrigger(g.bLeftTrigger), XInputNative.NormalizeTrigger(g.bRightTrigger),
                        MapXInputButtons(g.wButtons), g.wButtons);
                }

                case InputDeviceKind.Gamepad:
                {
                    var pad = MainGamepad;
                    if (pad == null) return null;
                    var r = pad.GetCurrentReading();
                    return BuildPadReading(
                        InputDeviceKind.Gamepad,
                        r.LeftThumbstickX, r.LeftThumbstickY, r.RightThumbstickX, r.RightThumbstickY,
                        r.LeftTrigger, r.RightTrigger, r.Buttons, (int)r.Buttons);
                }
            }
            return null;
        }

        private static UnifiedReading BuildPadReading(InputDeviceKind kind,
            double lx, double ly, double rx, double ry, double lt, double rt,
            GamepadButtons buttons, int rawButtons)
        {
            TrackAxis(1, lx); TrackAxis(2, rx); TrackAxis(3, ly); TrackAxis(4, ry);

            var axis = ResolveSteeringAxis();
            double steering = axis switch
            {
                SteeringAxisSource.RightX => rx,
                SteeringAxisSource.LeftY => ly,
                SteeringAxisSource.RightY => ry,
                _ => lx,
            };
            if (SettingsManager.InvertSteering) steering = -steering;

            // Only pass through buttons the Speed Wheel actually has (A/B/X/Y, D-pad, LB/RB gears,
            // Start/Back, Guide is not exposed). Thumb clicks are passed too in case a pad is used.
            return new UnifiedReading
            {
                Kind = kind,
                DeviceName = ActiveDeviceName,
                Steering = Math.Clamp(steering, -1.0, 1.0),
                Throttle = rt,
                Brake = lt,
                OutputButtons = buttons,
                LeftX = lx, LeftY = ly, RightX = rx, RightY = ry,
                LeftTrigger = lt, RightTrigger = rt,
                RawButtons = rawButtons,
                SteeringAxisUsed = axis,
            };
        }

        private static void TrackAxis(int index, double value)
        {
            double a = Math.Abs(value);
            if (a > AxisMaxAbs[index]) AxisMaxAbs[index] = a;
        }

        /// <summary>
        /// For SteeringAxisSource.Auto: use LeftX, unless LeftX has never moved (&lt; 0.15) while some other
        /// stick axis has travelled more than 0.5 – then that axis is almost certainly the wheel.
        /// </summary>
        public static SteeringAxisSource ResolveSteeringAxis()
        {
            var configured = SettingsManager.SteeringAxis;
            if (configured != SteeringAxisSource.Auto) return configured;

            if (AxisMaxAbs[1] >= 0.15) return SteeringAxisSource.LeftX;

            int best = 1;
            for (int i = 2; i <= 4; i++)
            {
                if (AxisMaxAbs[i] > AxisMaxAbs[best]) best = i;
            }
            return AxisMaxAbs[best] > 0.5 ? (SteeringAxisSource)best : SteeringAxisSource.LeftX;
        }

        public static GamepadButtons MapRacingWheelButtons(RacingWheelButtons b)
        {
            GamepadButtons o = GamepadButtons.None;
            if (b.HasFlag(RacingWheelButtons.DPadDown)) o |= GamepadButtons.DPadDown;
            if (b.HasFlag(RacingWheelButtons.DPadUp)) o |= GamepadButtons.DPadUp;
            if (b.HasFlag(RacingWheelButtons.DPadLeft)) o |= GamepadButtons.DPadLeft;
            if (b.HasFlag(RacingWheelButtons.DPadRight)) o |= GamepadButtons.DPadRight;
            if (b.HasFlag(RacingWheelButtons.NextGear)) o |= GamepadButtons.RightShoulder;
            if (b.HasFlag(RacingWheelButtons.PreviousGear)) o |= GamepadButtons.LeftShoulder;
            if (b.HasFlag(RacingWheelButtons.Button1)) o |= GamepadButtons.Menu;
            if (b.HasFlag(RacingWheelButtons.Button2)) o |= GamepadButtons.View;
            if (b.HasFlag(RacingWheelButtons.Button3)) o |= GamepadButtons.A;
            if (b.HasFlag(RacingWheelButtons.Button4)) o |= GamepadButtons.B;
            if (b.HasFlag(RacingWheelButtons.Button5)) o |= GamepadButtons.X;
            if (b.HasFlag(RacingWheelButtons.Button6)) o |= GamepadButtons.Y;
            return o;
        }

        public static GamepadButtons MapXInputButtons(ushort b)
        {
            GamepadButtons o = GamepadButtons.None;
            if ((b & XInputNative.DPadUp) != 0) o |= GamepadButtons.DPadUp;
            if ((b & XInputNative.DPadDown) != 0) o |= GamepadButtons.DPadDown;
            if ((b & XInputNative.DPadLeft) != 0) o |= GamepadButtons.DPadLeft;
            if ((b & XInputNative.DPadRight) != 0) o |= GamepadButtons.DPadRight;
            if ((b & XInputNative.Start) != 0) o |= GamepadButtons.Menu;
            if ((b & XInputNative.Back) != 0) o |= GamepadButtons.View;
            if ((b & XInputNative.LeftThumb) != 0) o |= GamepadButtons.LeftThumbstick;
            if ((b & XInputNative.RightThumb) != 0) o |= GamepadButtons.RightThumbstick;
            if ((b & XInputNative.LeftShoulder) != 0) o |= GamepadButtons.LeftShoulder;
            if ((b & XInputNative.RightShoulder) != 0) o |= GamepadButtons.RightShoulder;
            if ((b & XInputNative.ButtonA) != 0) o |= GamepadButtons.A;
            if ((b & XInputNative.ButtonB) != 0) o |= GamepadButtons.B;
            if ((b & XInputNative.ButtonX) != 0) o |= GamepadButtons.X;
            if ((b & XInputNative.ButtonY) != 0) o |= GamepadButtons.Y;
            return o;
        }

        private static void ListenForWheelChanges()
        {
            LifecycleManager.Tick += (object? Sender, EventArgs Event) =>
            {
                if (RacingWheel.RacingWheels.Count != ActiveWheels.Count)
                {
                    UpdateWheels();
                }
                else
                {
                    SelectDevice();
                }
            };
        }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            ListenForWheelChanges();
            DiagnosticsLog.Write($"WheelManager initialised. Device mode: {SettingsManager.DeviceMode}, steering axis: {SettingsManager.SteeringAxis}, invert: {SettingsManager.InvertSteering}, sensitivity: {SettingsManager.Sensitivity:0.00}");
            SelectDevice(force: true);
        }
    }
}
