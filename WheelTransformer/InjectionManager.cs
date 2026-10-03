using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using Windows.Gaming.Input;
using Windows.UI.Input.Preview.Injection;

namespace XboxWheelCompatibility.WheelTransformer
{
    public class InjectionManager
    {
        private static InputInjector? Injector;
        private static string? InjectorCreationError;
        private static bool _gamepadInjectionInitialized;
        private static bool _initialized;

        private static long _injectAttempts;
        private static long _injectSuccesses;
        private static string? _lastInjectError;
        private static UnifiedReading? _lastReading;

        private static readonly Stopwatch AxisLogTimer = Stopwatch.StartNew();
        private static long _lastAxisLogMs;
        private static string _lastAxisLogLine = "";

        private static volatile bool _outputsDirty = true;
        private static readonly object OutputLock = new();

        /// <summary>True when at least one virtual controller is active.</summary>
        public static bool InjectorAvailable => Injector != null || ViGEmOutput.Connected || VJoyOutput.Connected;
        public static string? InjectorErrorMessage => InjectorCreationError ?? ViGEmOutput.Error;
        public static bool InputInjectorActive => Injector != null;
        public static bool ViGEmActive => ViGEmOutput.Connected;
        public static string? ViGEmError => ViGEmOutput.Error;

        /// <summary>Human readable description of the active outputs.</summary>
        public static string ActiveOutputs
        {
            get
            {
                var parts = new List<string>();
                if (ViGEmOutput.Connected) parts.Add("ViGEm Xbox 360");
                if (Injector != null) parts.Add("InputInjector");
                if (VJoyOutput.Connected) parts.Add("vJoy wheel");
                return parts.Count > 0 ? string.Join(" + ", parts) : "none";
            }
        }

        /// <summary>Request outputs to be recreated (after the output mode changed).</summary>
        public static void ReconfigureOutputs() => _outputsDirty = true;

        private static void ApplyOutputMode()
        {
            lock (OutputLock)
            {
                if (!_outputsDirty) return;
                _outputsDirty = false;

                var mode = SettingsManager.Output;
                if (SettingsManager.VJoyEnabled)
                {
                    VJoyOutput.Connect(SettingsManager.VJoyDeviceId);
                }
                else
                {
                    VJoyOutput.Disconnect();
                }

                bool wantViGEm = mode == OutputMode.ViGEm || mode == OutputMode.Both || mode == OutputMode.Auto;
                bool vigemOk = wantViGEm && ViGEmOutput.TryConnect();
                if (!wantViGEm) ViGEmOutput.Disconnect();

                bool wantInjector = mode == OutputMode.InputInjector || mode == OutputMode.Both
                    || (mode == OutputMode.Auto && !vigemOk);

                if (wantInjector)
                {
                    EnsureInjector();
                }
                else
                {
                    DestroyInjector();
                    InjectorCreationError = null;
                }

                DiagnosticsLog.Write($"Output mode {mode}: active outputs = {ActiveOutputs}");
            }
        }
        public static long InjectAttempts => Interlocked.Read(ref _injectAttempts);
        public static long InjectSuccesses => Interlocked.Read(ref _injectSuccesses);
        public static string? LastInjectError => _lastInjectError;

        /// <summary>Last reading taken from the active device (thread-safe reference swap).</summary>
        public static UnifiedReading? LastReading => Volatile.Read(ref _lastReading);

        private static void EnsureInjector()
        {
            if (Injector != null) return;

            try
            {
                Injector = InputInjector.TryCreate();
                if (Injector == null)
                {
                    InjectorCreationError = "Windows could not create the gamepad injector. Check the Xbox Accessory Management Service (XboxGipSvc), then restart WheelCompatibilityService.";
                    DiagnosticsLog.Write("InputInjector.TryCreate returned null.");
                    return;
                }

                try
                {
                    // Documented requirement before InjectGamepadInput; creates the virtual Xbox controller.
                    Injector.InitializeGamepadInjection();
                    _gamepadInjectionInitialized = true;
                    DiagnosticsLog.Write("Virtual gamepad injection initialised.");
                }
                catch (Exception ex)
                {
                    // Keep going: older builds of this project injected without this call.
                    DiagnosticsLog.Write("InitializeGamepadInjection failed: " + ex.GetType().Name + ": " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                InjectorCreationError = ex.GetType().Name + ": " + ex.Message;
                DiagnosticsLog.Write("InputInjector creation failed: " + InjectorCreationError);
            }
        }

        /// <summary>
        /// Dead zone (values inside it become 0, the rest is rescaled so full lock is still 1.0),
        /// followed by the sensitivity curve.
        /// </summary>
        public static double ApplySensitivity(double wheelValue)
        {
            double deadZone = SettingsManager.DeadZone;
            double mag = Math.Abs(wheelValue);
            if (mag <= deadZone) return 0.0;
            // Dead zone: rescale so full input is still 1.0.
            mag = Math.Min(1.0, (mag - deadZone) / (1.0 - deadZone));

            // Rotation angle: physical angle turned -> fraction of the virtual wheel's half-rotation.
            // e.g. physical 90 deg, rotation 180 -> 1:1; rotation 90 -> full lock at 45 deg;
            // rotation 540 -> turning 90 deg gives 1/3 lock (the game never reaches full lock).
            double physicalAngle = mag * SettingsManager.PhysicalDegrees;
            double halfRotation = SettingsManager.RotationDegrees / 2.0;
            wheelValue = Math.Sign(wheelValue) * Math.Min(1.0, physicalAngle / halfRotation);

            double sensitivity = SettingsManager.Sensitivity;
            if (sensitivity <= 0) return wheelValue;
            if (Math.Abs(sensitivity - 1.0) < 0.0001) return wheelValue;

            double sign = wheelValue < 0 ? -1.0 : 1.0;
            double magnitude = Math.Min(1.0, Math.Abs(wheelValue));
            double exponent = 1.0 / sensitivity;
            double curved = Math.Pow(magnitude, exponent);

            return Math.Clamp(sign * curved, -1.0, 1.0);
        }

        // ---------------- Anti-deadzone ----------------
        // Some games (F1 25 for example) ignore the first ~20 % of a steering axis even with their own
        // linearity / dead zone settings at 0. Anti-deadzone lifts every non-zero value by a fixed
        // offset so the smallest real input already lands above the game's dead zone:
        //     |x| < 0.001  ->  0
        //     otherwise    ->  sign(x) * (ad + (1 - ad) * |x|)
        // It is applied LAST (after dead zone, rotation angle and sensitivity), right before sending,
        // for the vJoy wheel, ViGEm and InputInjector alike. ad = 0 leaves the value untouched.

        /// <summary>Anti-deadzone for the steering axis (-1..1).</summary>
        public static double ApplySteeringAntiDeadZone(double wheelValue)
            => ApplyAntiDeadZone(wheelValue, SettingsManager.SteeringAntiDeadZone);

        /// <summary>Anti-deadzone for the throttle (0..1, no sign).</summary>
        public static double ApplyThrottleAntiDeadZone(double value)
            => ApplyPedalAntiDeadZone(value, SettingsManager.ThrottleAntiDeadZone);

        /// <summary>Anti-deadzone for the brake (0..1, no sign).</summary>
        public static double ApplyBrakeAntiDeadZone(double value)
            => ApplyPedalAntiDeadZone(value, SettingsManager.BrakeAntiDeadZone);

        private static double ApplyAntiDeadZone(double value, double antiDeadZone)
        {
            if (antiDeadZone <= 0.0) return value;

            double magnitude = Math.Min(1.0, Math.Abs(value));
            if (magnitude < 0.001) return 0.0;

            double lifted = antiDeadZone + (1.0 - antiDeadZone) * magnitude;
            return Math.Clamp(Math.Sign(value) * lifted, -1.0, 1.0);
        }

        private static double ApplyPedalAntiDeadZone(double value, double antiDeadZone)
        {
            if (antiDeadZone <= 0.0) return value;

            double v = Math.Clamp(value, 0.0, 1.0);
            if (v < 0.001) return 0.0;

            return Math.Clamp(antiDeadZone + (1.0 - antiDeadZone) * v, 0.0, 1.0);
        }

        private static void InjectCurrentReading()
        {
            UnifiedReading? reading;
            try
            {
                reading = WheelManager.ReadActive();
            }
            catch (Exception ex)
            {
                _lastInjectError = "Read failed: " + ex.GetType().Name + ": " + ex.Message;
                reading = null;
            }

            if (reading != null)
            {
                // PedalsManager.Update() always runs so the Pedals tab keeps showing live values even
                // when the pedal source is set to the wheel triggers.
                bool pedalsActive = PedalsManager.Update();

                double triggerThrottle = reading.Throttle;
                double triggerBrake = reading.Brake;
                double pedalThrottle = pedalsActive ? PedalsManager.Throttle : 0.0;
                double pedalBrake = pedalsActive ? PedalsManager.Brake : 0.0;

                double throttle, brake;
                switch (SettingsManager.PedalSource)
                {
                    case PedalSourceMode.WheelTriggers:
                        throttle = triggerThrottle;
                        brake = triggerBrake;
                        break;
                    case PedalSourceMode.SeparatePedals:
                        throttle = pedalThrottle;
                        brake = pedalBrake;
                        break;
                    default: // BothMax - original behaviour: whichever is pressed harder wins.
                        throttle = Math.Max(triggerThrottle, pedalThrottle);
                        brake = Math.Max(triggerBrake, pedalBrake);
                        break;
                }

                reading = reading with
                {
                    Throttle = throttle,
                    Brake = brake,
                    TriggerThrottle = triggerThrottle,
                    TriggerBrake = triggerBrake,
                    PedalThrottle = pedalThrottle,
                    PedalBrake = pedalBrake,
                };
            }

            Volatile.Write(ref _lastReading, reading);
            if (reading == null) return;

            double adjustedWheel = ApplySensitivity(reading.Steering);
            double sentWheel = ApplySteeringAntiDeadZone(adjustedWheel);
            double sentThrottle = ApplyThrottleAntiDeadZone(reading.Throttle);
            double sentBrake = ApplyBrakeAntiDeadZone(reading.Brake);

            LogAxesIfChanged(reading, adjustedWheel, sentWheel, sentThrottle, sentBrake);

            if (Injector == null && !ViGEmOutput.Connected && !VJoyOutput.Connected) return;

            Interlocked.Increment(ref _injectAttempts);

            try
            {
                if (VJoyOutput.Connected)
                {
                    VJoyOutput.Submit(sentWheel, sentThrottle, sentBrake, reading.OutputButtons);
                }

                if (ViGEmOutput.Connected)
                {
                    ViGEmOutput.Submit(reading.OutputButtons, sentBrake, sentThrottle, sentWheel);
                }

                if (Injector != null)
                {
                // Output layout is identical for every device type:
                // steering -> LeftThumbstickX, throttle -> RightTrigger, brake -> LeftTrigger.
                Injector.InjectGamepadInput(new InjectedInputGamepadInfo(
                        new GamepadReading(
                            (ulong)DateTime.UtcNow.Ticks,
                            reading.OutputButtons,
                            sentBrake, sentThrottle,
                            sentWheel, 0,
                            0, 0
                        )
                    )
                );
                }
                Interlocked.Increment(ref _injectSuccesses);
                _lastInjectError = null;
            }
            catch (Exception ex)
            {
                _lastInjectError = ex.GetType().Name + ": " + ex.Message;
            }
        }

        /// <summary>
        /// Logs raw -> processed -> sent values at most every 2 s, only when they changed noticeably.
        /// </summary>
        private static void LogAxesIfChanged(UnifiedReading r, double processed, double sentSteering, double sentThrottle, double sentBrake)
        {
            long now = AxisLogTimer.ElapsedMilliseconds;
            if (now - _lastAxisLogMs < 2000) return;

            var sb = new StringBuilder(256);
            sb.Append("Axes [").Append(r.Kind).Append(']');
            sb.AppendFormat(CultureInfo.InvariantCulture, " steer raw={0:+0.00;-0.00;+0.00} proc={1:+0.00;-0.00;+0.00} sent={2:+0.00;-0.00;+0.00}",
                r.Steering, processed, sentSteering);
            sb.AppendFormat(CultureInfo.InvariantCulture, " | thr trig={0:0.00} ped={1:0.00} out={2:0.00} sent={3:0.00}",
                r.TriggerThrottle, r.PedalThrottle, r.Throttle, sentThrottle);
            sb.AppendFormat(CultureInfo.InvariantCulture, " | brk trig={0:0.00} ped={1:0.00} out={2:0.00} sent={3:0.00}",
                r.TriggerBrake, r.PedalBrake, r.Brake, sentBrake);
            sb.Append(" | src=").Append(SettingsManager.PedalSource);
            sb.AppendFormat(CultureInfo.InvariantCulture, " adz steer={0:0.00} thr={1:0.00} brk={2:0.00}",
                SettingsManager.SteeringAntiDeadZone, SettingsManager.ThrottleAntiDeadZone, SettingsManager.BrakeAntiDeadZone);
            sb.AppendFormat(CultureInfo.InvariantCulture, " | LX={0:+0.00;-0.00;+0.00} LY={1:+0.00;-0.00;+0.00} RX={2:+0.00;-0.00;+0.00} RY={3:+0.00;-0.00;+0.00}",
                r.LeftX, r.LeftY, r.RightX, r.RightY);
            sb.AppendFormat(CultureInfo.InvariantCulture, " LT={0:0.00} RT={1:0.00}", r.LeftTrigger, r.RightTrigger);
            sb.Append(" steerAxis=").Append(r.SteeringAxisUsed);
            sb.AppendFormat(CultureInfo.InvariantCulture, " buttons=0x{0:X4}", r.RawButtons);

            string line = sb.ToString();

            if (line == _lastAxisLogLine) return;
            _lastAxisLogLine = line;
            _lastAxisLogMs = now;
            DiagnosticsLog.Write(line);
        }

        public static void Initialize()
        {
            _ = SettingsManager.Sensitivity;
            _outputsDirty = true;
            ApplyOutputMode();

            if (_initialized) return;
            _initialized = true;

            LifecycleManager.Tick += (object? Sender, EventArgs Event) =>
            {
                if (!LifecycleManager.Started) return;
                if (_outputsDirty) ApplyOutputMode();

                if (RacingWheel.RacingWheels.Count > WheelManager.ActiveWheels.Count)
                {
                    WheelManager.UpdateWheels();
                }

                if (WheelManager.ActiveKind == InputDeviceKind.None)
                {
                    Volatile.Write(ref _lastReading, null);
                    return;
                }

                InjectCurrentReading();
            };
        }

        public static void Destroy()
        {
            lock (OutputLock)
            {
                ViGEmOutput.Disconnect();
                VJoyOutput.Disconnect();
                DestroyInjector();
            }
        }

        private static void DestroyInjector()
        {
            try
            {
                if (_gamepadInjectionInitialized)
                {
                    Injector?.UninitializeGamepadInjection();
                }
            }
            catch { }
            _gamepadInjectionInitialized = false;
            Injector = null;
        }
    }
}
