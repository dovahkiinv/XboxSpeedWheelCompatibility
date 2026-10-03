using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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

        // Values that were actually sent to the virtual devices (raw -> processed -> output).
        // Read by the configurator's live tester so it shows exactly what the game receives.
        private static double _lastOutSteering;
        private static double _lastOutThrottle;
        private static double _lastOutBrake;
        private static double _lastTriggerThrottle;
        private static double _lastTriggerBrake;
        private static double _lastPedalThrottle;
        private static double _lastPedalBrake;
        private static bool _lastPedalsConnected;

        private static readonly Stopwatch AxisLogTimer = Stopwatch.StartNew();
        private static long _lastAxisLogMs;
        private static long _lastAxisLogChangeMs;
        private static string _lastAxisLogLine = "";

        /// <summary>Values are logged at most this often; when idle only every <see cref="AxisLogHeartbeatMs"/>.</summary>
        private const long AxisLogIntervalMs = 3000;
        private const long AxisLogHeartbeatMs = 15000;

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

        /// <summary>Steering as sent to the virtual devices (after sensitivity and anti-deadzone).</summary>
        public static double LastOutSteering => Volatile.Read(ref _lastOutSteering);
        public static double LastOutThrottle => Volatile.Read(ref _lastOutThrottle);
        public static double LastOutBrake => Volatile.Read(ref _lastOutBrake);
        /// <summary>Throttle straight from the device triggers (before merging with the pedals).</summary>
        public static double LastTriggerThrottle => Volatile.Read(ref _lastTriggerThrottle);
        public static double LastTriggerBrake => Volatile.Read(ref _lastTriggerBrake);
        /// <summary>Throttle straight from the separate pedal set (before merging).</summary>
        public static double LastPedalThrottle => Volatile.Read(ref _lastPedalThrottle);
        public static double LastPedalBrake => Volatile.Read(ref _lastPedalBrake);
        public static bool LastPedalsConnected => Volatile.Read(ref _lastPedalsConnected);

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
        /// followed by the rotation angle and the sensitivity curve. The anti-deadzone is applied
        /// afterwards in <see cref="ApplySteeringOutput"/>, right before the value is sent out.
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

            double triggerThrottle = 0.0, triggerBrake = 0.0;
            double pedalThrottle = 0.0, pedalBrake = 0.0;
            bool pedalsConnected = false;

            if (reading != null)
            {
                // Wheel / Speed Wheel triggers.
                triggerThrottle = reading.Throttle;
                triggerBrake = reading.Brake;

                // Separate pedals (may be disabled or not connected).
                pedalsConnected = PedalsManager.Update();
                pedalThrottle = PedalsManager.Throttle;
                pedalBrake = PedalsManager.Brake;

                var source = SettingsManager.PedalsSource;
                double throttle, brake;
                switch (source)
                {
                    case PedalSource.WheelTriggers:
                        throttle = triggerThrottle;
                        brake = triggerBrake;
                        break;
                    case PedalSource.SeparatePedals:
                        throttle = pedalThrottle;
                        brake = pedalBrake;
                        break;
                    default:
                        // Both (max): whichever is pressed more wins (original behaviour).
                        throttle = Math.Max(triggerThrottle, pedalThrottle);
                        brake = Math.Max(triggerBrake, pedalBrake);
                        break;
                }

                reading = reading with
                {
                    Throttle = Math.Clamp(throttle, 0.0, 1.0),
                    Brake = Math.Clamp(brake, 0.0, 1.0),
                };
            }

            Volatile.Write(ref _lastReading, reading);
            if (reading == null) return;

            // Final processing: raw -> dead zone/rotation/sensitivity -> anti-deadzone -> output.
            double outSteering = ApplySteeringOutput(reading.Steering);
            double outThrottle = ApplyThrottleOutput(reading.Throttle);
            double outBrake = ApplyBrakeOutput(reading.Brake);

            Volatile.Write(ref _lastOutSteering, outSteering);
            Volatile.Write(ref _lastOutThrottle, outThrottle);
            Volatile.Write(ref _lastOutBrake, outBrake);
            Volatile.Write(ref _lastTriggerThrottle, triggerThrottle);
            Volatile.Write(ref _lastTriggerBrake, triggerBrake);
            Volatile.Write(ref _lastPedalThrottle, pedalThrottle);
            Volatile.Write(ref _lastPedalBrake, pedalBrake);
            Volatile.Write(ref _lastPedalsConnected, pedalsConnected);

            // Periodic log: raw -> processed -> what vJoy gets.
            LogValuesIfDue(reading, triggerThrottle, triggerBrake, pedalThrottle, pedalBrake,
                pedalsConnected, outSteering, outThrottle, outBrake);

            if (Injector == null && !ViGEmOutput.Connected && !VJoyOutput.Connected) return;

            Interlocked.Increment(ref _injectAttempts);

            try
            {
                if (VJoyOutput.Connected)
                {
                    VJoyOutput.Submit(outSteering, outThrottle, outBrake, reading.OutputButtons);
                }

                if (ViGEmOutput.Connected)
                {
                    ViGEmOutput.Submit(reading.OutputButtons, outBrake, outThrottle, outSteering);
                }

                if (Injector != null)
                {
                // Output layout is identical for every device type:
                // steering -> LeftThumbstickX, throttle -> RightTrigger, brake -> LeftTrigger.
                Injector.InjectGamepadInput(new InjectedInputGamepadInfo(
                        new GamepadReading(
                            (ulong)DateTime.UtcNow.Ticks,
                            reading.OutputButtons,
                            outBrake, outThrottle,
                            outSteering, 0,
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
        /// Steering picture for the game: dead zone + rotation angle + sensitivity, then the
        /// anti-deadzone so games with a built-in dead zone (F1 25) react right away.
        /// </summary>
        public static double ApplySteeringOutput(double rawSteering) =>
            ApplyAntiDeadzone(ApplySensitivity(rawSteering), SettingsManager.SteeringAntiDeadzone);

        /// <summary>Throttle as sent to the virtual devices (0..1).</summary>
        public static double ApplyThrottleOutput(double throttle) =>
            ApplyPedalAntiDeadzone(throttle, SettingsManager.ThrottleAntiDeadzone);

        /// <summary>Brake as sent to the virtual devices (0..1).</summary>
        public static double ApplyBrakeOutput(double brake) =>
            ApplyPedalAntiDeadzone(brake, SettingsManager.BrakeAntiDeadzone);

        /// <summary>
        /// Anti-deadzone for a centred axis (-1..1), applied last, right before the value leaves the app:
        /// |x| &lt; 0.001 -> 0, otherwise sign(x) * (ad + (1 - ad) * |x|). ad = 0 returns x unchanged.
        /// </summary>
        public static double ApplyAntiDeadzone(double x, double antiDeadzone)
        {
            double ad = Math.Clamp(antiDeadzone, SettingsManager.MinAntiDeadzone, SettingsManager.MaxAntiDeadzone);
            if (ad <= 0.0) return Math.Clamp(x, -1.0, 1.0);
            if (Math.Abs(x) < 0.001) return 0.0;

            double sign = x < 0 ? -1.0 : 1.0;
            return Math.Clamp(sign * (ad + (1.0 - ad) * Math.Abs(x)), -1.0, 1.0);
        }

        /// <summary>Same as <see cref="ApplyAntiDeadzone"/> for a 0..1 pedal value (no sign).</summary>
        public static double ApplyPedalAntiDeadzone(double x, double antiDeadzone)
        {
            double v = Math.Clamp(x, 0.0, 1.0);
            double ad = Math.Clamp(antiDeadzone, SettingsManager.MinAntiDeadzone, SettingsManager.MaxAntiDeadzone);
            if (ad <= 0.0) return v;
            if (v < 0.001) return 0.0;

            return Math.Min(1.0, ad + (1.0 - ad) * v);
        }

        /// <summary>
        /// Logs raw axes, the processed values and what vJoy received, at most every 3 s.
        /// </summary>
        private static void LogValuesIfDue(UnifiedReading r,
            double triggerThrottle, double triggerBrake, double pedalThrottle, double pedalBrake,
            bool pedalsConnected, double outSteering, double outThrottle, double outBrake)
        {
            long now = AxisLogTimer.ElapsedMilliseconds;
            if (now - _lastAxisLogMs < AxisLogIntervalMs) return;

            string Format(double v) => v.ToString("+0.00;-0.00;+0.00", CultureInfo.InvariantCulture);
            string Pedal(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

            string vjoyPart;
            if (VJoyOutput.Connected)
            {
                vjoyPart = string.Format(CultureInfo.InvariantCulture,
                    "vJoy device {0} X={1} Y={2} Z={3} (mode={4}, invertThr={5}, invertBrk={6})",
                    SettingsManager.VJoyDeviceId, VJoyOutput.LastAxisX, VJoyOutput.LastAxisY,
                    VJoyOutput.LastAxisZ, SettingsManager.VJoyPedalAxisMode,
                    SettingsManager.VJoyInvertThrottle, SettingsManager.VJoyInvertBrake);
            }
            else
            {
                vjoyPart = "vJoy off";
            }

            string line = string.Format(CultureInfo.InvariantCulture,
                "Axes [{0}] raw: LX={1} LY={2} RX={3} RY={4} LT={5} RT={6} steer({7})={8} | triggers thr={9} brk={10} | pedals thr={11} brk={12} connected={13} source={14} | processed: deadzone={15} rotation={16:0}deg physical={17:0}deg sensitivity={18:0.00} anti-deadzone steer={19} thr={20} brk={21} | out: steer={22} thr={23} brk={24} buttons=0x{25:X4} | {26}",
                r.Kind, Format(r.LeftX), Format(r.LeftY), Format(r.RightX), Format(r.RightY),
                Pedal(r.LeftTrigger), Pedal(r.RightTrigger), r.SteeringAxisUsed, Format(r.Steering),
                Pedal(triggerThrottle), Pedal(triggerBrake), Pedal(pedalThrottle), Pedal(pedalBrake),
                pedalsConnected ? "yes" : "no", SettingsManager.PedalsSource,
                Pedal(SettingsManager.DeadZone), SettingsManager.RotationDegrees, SettingsManager.PhysicalDegrees,
                SettingsManager.Sensitivity, Pedal(SettingsManager.SteeringAntiDeadzone),
                Pedal(SettingsManager.ThrottleAntiDeadzone), Pedal(SettingsManager.BrakeAntiDeadzone),
                Format(outSteering), Pedal(outThrottle), Pedal(outBrake), r.RawButtons, vjoyPart);

            // Always log while something moves (every 3 s); when nothing changes, log a heartbeat
            // every 15 s so Output.log does not grow forever while the app idles.
            bool changed = line != _lastAxisLogLine;
            if (!changed && now - _lastAxisLogChangeMs < AxisLogHeartbeatMs) return;
            if (changed) _lastAxisLogChangeMs = now;
            _lastAxisLogMs = now;
            _lastAxisLogLine = line;

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
