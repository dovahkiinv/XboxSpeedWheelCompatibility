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

            if (reading != null && PedalsManager.Update())
            {
                // Separate pedals are merged with the wheel triggers (whichever is pressed more).
                reading = reading with
                {
                    Throttle = Math.Max(reading.Throttle, PedalsManager.Throttle),
                    Brake = Math.Max(reading.Brake, PedalsManager.Brake),
                };
            }

            Volatile.Write(ref _lastReading, reading);
            if (reading == null) return;

            LogAxesIfChanged(reading);

            if (Injector == null && !ViGEmOutput.Connected && !VJoyOutput.Connected) return;

            double adjustedWheel = ApplySensitivity(reading.Steering);

            Interlocked.Increment(ref _injectAttempts);

            try
            {
                if (VJoyOutput.Connected)
                {
                    VJoyOutput.Submit(adjustedWheel, reading.Throttle, reading.Brake, reading.OutputButtons);
                }

                if (ViGEmOutput.Connected)
                {
                    ViGEmOutput.Submit(reading.OutputButtons, reading.Brake, reading.Throttle, adjustedWheel);
                }

                if (Injector != null)
                {
                // Output layout is identical for every device type:
                // steering -> LeftThumbstickX, throttle -> RightTrigger, brake -> LeftTrigger.
                Injector.InjectGamepadInput(new InjectedInputGamepadInfo(
                        new GamepadReading(
                            (ulong)DateTime.UtcNow.Ticks,
                            reading.OutputButtons,
                            reading.Brake, reading.Throttle,
                            adjustedWheel, 0,
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

        /// <summary>Logs raw axes at most every 2 s, only when they changed noticeably.</summary>
        private static void LogAxesIfChanged(UnifiedReading r)
        {
            long now = AxisLogTimer.ElapsedMilliseconds;
            if (now - _lastAxisLogMs < 2000) return;

            string line = string.Format(CultureInfo.InvariantCulture,
                "Axes [{0}] LX={1:+0.00;-0.00;+0.00} LY={2:+0.00;-0.00;+0.00} RX={3:+0.00;-0.00;+0.00} RY={4:+0.00;-0.00;+0.00} LT={5:0.00} RT={6:0.00} steer({7})={8:+0.00;-0.00;+0.00} thr={9:0.00} brk={10:0.00} buttons=0x{11:X4}",
                r.Kind, r.LeftX, r.LeftY, r.RightX, r.RightY, r.LeftTrigger, r.RightTrigger,
                r.SteeringAxisUsed, r.Steering, r.Throttle, r.Brake, r.RawButtons);

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
