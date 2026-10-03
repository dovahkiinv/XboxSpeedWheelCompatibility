using ServiceWire.TcpIp;
using Windows.Gaming.Input;
using XboxWheelCompatibility.CommunicationInterface;
using XboxWheelCompatibility.WheelTransformer;

namespace XboxWheelCompatibility.WheelCompatibilityService
{
    public class WheelCompatibilityWorker : BackgroundService, IWheelCompatibilityService
    {
        private readonly ILogger<WheelCompatibilityWorker> Logger;
        private readonly TcpHost TCPHost;

        public int GetMainWheelIndex()
        {
            switch (WheelManager.ActiveKind)
            {
                case InputDeviceKind.RacingWheel:
                    var wheel = WheelManager.MainWheel;
                    return wheel == null ? -1 : RacingWheel.RacingWheels.ToList().IndexOf(wheel);
                case InputDeviceKind.XInputSpeedWheel:
                case InputDeviceKind.Gamepad:
                    return 0;
                default:
                    return -1;
            }
        }

        public void Start()
        {
            WheelInputTransformer.Start();
        }

        public void Stop()
        {
            WheelInputTransformer.Stop();
        }

        public double GetSensitivity()
        {
            return SettingsManager.Sensitivity;
        }

        public void SetSensitivity(double Sensitivity)
        {
            double previous = SettingsManager.Sensitivity;
            SettingsManager.Sensitivity = Sensitivity;
            LogChange($"Sensitivity change: {previous:0.00} -> {SettingsManager.Sensitivity:0.00}", previous, SettingsManager.Sensitivity);
        }

        public double GetDeadZone()
        {
            return SettingsManager.DeadZone;
        }

        public void SetDeadZone(double DeadZone)
        {
            double previous = SettingsManager.DeadZone;
            SettingsManager.DeadZone = DeadZone;
            LogChange($"Dead zone change: {previous:0.00} -> {SettingsManager.DeadZone:0.00}", previous, SettingsManager.DeadZone);
        }

        public double GetRotationDegrees() => SettingsManager.RotationDegrees;
        public void SetRotationDegrees(double Degrees)
        {
            double previous = SettingsManager.RotationDegrees;
            SettingsManager.RotationDegrees = Degrees;
            LogChange($"Rotation angle change: {previous:0} -> {SettingsManager.RotationDegrees:0} deg", previous, SettingsManager.RotationDegrees);
        }

        public double GetPhysicalDegrees() => SettingsManager.PhysicalDegrees;
        public void SetPhysicalDegrees(double Degrees)
        {
            double previous = SettingsManager.PhysicalDegrees;
            SettingsManager.PhysicalDegrees = Degrees;
            LogChange($"Physical turn calibration change: {previous:0} -> {SettingsManager.PhysicalDegrees:0} deg", previous, SettingsManager.PhysicalDegrees);
        }

        /// <summary>Logs a settings change only when the value really changed (no spam while dragging).</summary>
        private static void LogChange(string message, double previous, double current)
        {
            if (Math.Abs(previous - current) < 0.0001) return;
            DiagnosticsLog.Write(message);
        }

        public void SetSteeringAntiDeadzone(double AntiDeadzone)
        {
            double previous = SettingsManager.SteeringAntiDeadzone;
            SettingsManager.SteeringAntiDeadzone = AntiDeadzone;
            LogChange($"Steering anti-deadzone change: requested {AntiDeadzone:0.00}, stored {SettingsManager.SteeringAntiDeadzone:0.00} (0..0.40, 0 = off).",
                previous, SettingsManager.SteeringAntiDeadzone);
        }

        public void SetThrottleAntiDeadzone(double AntiDeadzone)
        {
            double previous = SettingsManager.ThrottleAntiDeadzone;
            SettingsManager.ThrottleAntiDeadzone = AntiDeadzone;
            LogChange($"Throttle anti-deadzone change: requested {AntiDeadzone:0.00}, stored {SettingsManager.ThrottleAntiDeadzone:0.00} (0..0.40, 0 = off).",
                previous, SettingsManager.ThrottleAntiDeadzone);
        }

        public void SetBrakeAntiDeadzone(double AntiDeadzone)
        {
            double previous = SettingsManager.BrakeAntiDeadzone;
            SettingsManager.BrakeAntiDeadzone = AntiDeadzone;
            LogChange($"Brake anti-deadzone change: requested {AntiDeadzone:0.00}, stored {SettingsManager.BrakeAntiDeadzone:0.00} (0..0.40, 0 = off).",
                previous, SettingsManager.BrakeAntiDeadzone);
        }

        public void SetPedalsSource(int Source)
        {
            SettingsManager.PedalsSource = (PedalSource)Source;
            DiagnosticsLog.Write("Pedal source set to " + SettingsManager.PedalsSource
                + " (0 = wheel triggers, 1 = separate pedals, 2 = both max)");
        }

        public void SetVJoyPedalAxisMode(int Mode)
        {
            SettingsManager.VJoyPedalAxisMode = (VJoyPedalMode)Mode;
            DiagnosticsLog.Write("vJoy pedal axis mode set to " + SettingsManager.VJoyPedalAxisMode
                + " (0 = separate Y/Z axes, 1 = centred Y axis)");
        }

        public void SetVJoyAxisInvert(bool InvertThrottle, bool InvertBrake)
        {
            SettingsManager.VJoyInvertThrottle = InvertThrottle;
            SettingsManager.VJoyInvertBrake = InvertBrake;
            DiagnosticsLog.Write($"vJoy axis invert: throttle={SettingsManager.VJoyInvertThrottle}, brake={SettingsManager.VJoyInvertBrake}");
        }

        public DeviceStatus GetDeviceStatus()
        {
            return new DeviceStatus
            {
                DeviceKind = (int)WheelManager.ActiveKind,
                DeviceName = WheelManager.ActiveDeviceName,
                DeviceMode = (int)SettingsManager.DeviceMode,
                SteeringAxis = (int)SettingsManager.SteeringAxis,
                InvertSteering = SettingsManager.InvertSteering,
                HideRealDevice = SettingsManager.HideRealDevice,
                HidHideStatus = HidHideManager.Status,
                HideXInputInterface = SettingsManager.HideXInputInterface,
                VJoyEnabled = SettingsManager.VJoyEnabled,
                VJoyDeviceId = SettingsManager.VJoyDeviceId,
                VJoyStatus = VJoyOutput.Status,
                VJoyPedalAxisMode = (int)SettingsManager.VJoyPedalAxisMode,
                VJoyInvertThrottle = SettingsManager.VJoyInvertThrottle,
                VJoyInvertBrake = SettingsManager.VJoyInvertBrake,
                VJoyAxisWarning = VJoyOutput.AxisWarning.Length > 0,
                VJoyAxisMessage = VJoyOutput.AxisWarning,
                RotationDegrees = SettingsManager.RotationDegrees,
                SteeringAntiDeadzone = SettingsManager.SteeringAntiDeadzone,
                ScanLines = WheelManager.LastScan,
                RecentLog = DiagnosticsLog.GetRecent(12),
                LogPath = DiagnosticsLog.OutputLogPath + "  |  " + DiagnosticsLog.LogPath,
            };
        }

        public void SetDeviceMode(int Mode)
        {
            SettingsManager.DeviceMode = (DeviceSelectionMode)Mode;
            DiagnosticsLog.Write("Device mode set to " + SettingsManager.DeviceMode);
            WheelManager.SelectDevice(force: true);
        }

        public void SetSteeringAxis(int Axis)
        {
            SettingsManager.SteeringAxis = (SteeringAxisSource)Axis;
            DiagnosticsLog.Write("Steering axis set to " + SettingsManager.SteeringAxis);
        }

        public void SetInvertSteering(bool Invert)
        {
            SettingsManager.InvertSteering = Invert;
            DiagnosticsLog.Write("Invert steering set to " + Invert);
        }

        public string SetHideRealDevice(bool Hide)
        {
            SettingsManager.HideRealDevice = Hide;
            return Hide ? HidHideManager.Enable() : HidHideManager.Disable();
        }

        public PedalsStatus GetPedalsStatus()
        {
            if (!SettingsManager.PedalsEnabled) PedalsManager.ReadRaw(); // live raw axes before enabling
            return new PedalsStatus
            {
                Enabled = SettingsManager.PedalsEnabled,
                Connected = PedalsManager.Connected,
                ActiveDevice = PedalsManager.ActiveName,
                DeviceKey = SettingsManager.PedalsDevice,
                Devices = PedalsManager.Devices.Select(d => d.Key + "|" + d.ToString()).ToArray(),
                Axis = SettingsManager.PedalsAxis,
                Swap = SettingsManager.PedalsSwap,
                DeadZone = SettingsManager.PedalsDeadZone,
                Center = SettingsManager.PedalsCenter,
                ThrottleRange = SettingsManager.ThrottleRange,
                BrakeRange = SettingsManager.BrakeRange,
                Source = (int)SettingsManager.PedalsSource,
                ThrottleAntiDeadzone = SettingsManager.ThrottleAntiDeadzone,
                BrakeAntiDeadzone = SettingsManager.BrakeAntiDeadzone,
                RawAxes = PedalsManager.RawAxes,
                Throttle = PedalsManager.Throttle,
                Brake = PedalsManager.Brake,
            };
        }

        public void SetPedals(bool Enabled, string DeviceKey, int Axis, bool Swap, double DeadZone)
        {
            bool changedDevice = SettingsManager.PedalsDevice != (DeviceKey ?? "");
            SettingsManager.PedalsDevice = DeviceKey ?? "";
            SettingsManager.PedalsAxis = Axis;
            SettingsManager.PedalsSwap = Swap;
            SettingsManager.PedalsDeadZone = DeadZone;
            SettingsManager.PedalsEnabled = Enabled;
            if (changedDevice) PedalsManager.Scan(force: true);
            DiagnosticsLog.Write($"Pedals: enabled={Enabled} device='{DeviceKey}' axis={Axis} swap={Swap} deadzone={DeadZone:0.00} source={SettingsManager.PedalsSource}");
        }

        public string CalibratePedalsCenter() => PedalsManager.CalibrateCenter();

        public void SetPedalsRange(double ThrottleRange, double BrakeRange)
        {
            SettingsManager.ThrottleRange = ThrottleRange;
            SettingsManager.BrakeRange = BrakeRange;
            DiagnosticsLog.Write($"Pedal range: throttle 100% at {SettingsManager.ThrottleRange:0.00}, brake 100% at {SettingsManager.BrakeRange:0.00}");
        }

        public void SetVJoy(bool Enabled, int DeviceId)
        {
            SettingsManager.VJoyDeviceId = DeviceId;
            SettingsManager.VJoyEnabled = Enabled;
            DiagnosticsLog.Write($"vJoy wheel {(Enabled ? "enabled" : "disabled")} (device {SettingsManager.VJoyDeviceId}, pedal mode {SettingsManager.VJoyPedalAxisMode}, invert throttle={SettingsManager.VJoyInvertThrottle} brake={SettingsManager.VJoyInvertBrake})");
            InjectionManager.ReconfigureOutputs();
        }

        public string SetHideXInputInterface(bool Hide)
        {
            SettingsManager.HideXInputInterface = Hide;
            if (!SettingsManager.HideRealDevice) return "Saved. Takes effect when hiding is enabled.";
            HidHideManager.Disable();
            return HidHideManager.Enable();
        }

        public void SetOutputMode(int Mode)
        {
            SettingsManager.Output = (OutputMode)Mode;
            DiagnosticsLog.Write("Output mode set to " + SettingsManager.Output);
            InjectionManager.ReconfigureOutputs();
        }

        public InjectionDiagnostics GetInjectionDiagnostics()
        {
            return new InjectionDiagnostics
            {
                InjectorAvailable = InjectionManager.InjectorAvailable,
                InjectorErrorMessage = InjectionManager.InjectorErrorMessage,
                InjectAttempts = InjectionManager.InjectAttempts,
                InjectSuccesses = InjectionManager.InjectSuccesses,
                LastInjectError = InjectionManager.LastInjectError,
                ActiveOutputs = InjectionManager.ActiveOutputs,
                ViGEmActive = InjectionManager.ViGEmActive,
                ViGEmError = InjectionManager.ViGEmError,
                OutputMode = (int)SettingsManager.Output,
            };
        }

        public WheelReadingSnapshot GetReadingSnapshot()
        {
            var reading = InjectionManager.LastReading;
            if (reading == null || reading.Kind == InputDeviceKind.None)
            {
                return new WheelReadingSnapshot
                {
                    HasWheel = false,
                    DeviceKind = (int)WheelManager.ActiveKind,
                    DeviceName = WheelManager.ActiveDeviceName,
                };
            }

            return new WheelReadingSnapshot
            {
                HasWheel = true,
                Wheel = reading.Steering,
                // Values the injection loop actually sent, so the tester shows what the game receives
                // (dead zone -> rotation angle -> sensitivity -> anti-deadzone).
                WheelAdjusted = InjectionManager.LastOutSteering,
                Throttle = InjectionManager.LastOutThrottle,
                Brake = InjectionManager.LastOutBrake,
                TriggerThrottle = InjectionManager.LastTriggerThrottle,
                TriggerBrake = InjectionManager.LastTriggerBrake,
                PedalThrottle = InjectionManager.LastPedalThrottle,
                PedalBrake = InjectionManager.LastPedalBrake,
                PedalsConnected = InjectionManager.LastPedalsConnected,
                PedalSource = (int)SettingsManager.PedalsSource,
                Clutch = reading.Clutch,
                Handbrake = reading.Handbrake,
                Buttons = reading.RawButtons,
                OutputButtons = (int)reading.OutputButtons,
                DeviceKind = (int)reading.Kind,
                DeviceName = reading.DeviceName,
                LeftX = reading.LeftX,
                LeftY = reading.LeftY,
                RightX = reading.RightX,
                RightY = reading.RightY,
                LeftTrigger = reading.LeftTrigger,
                RightTrigger = reading.RightTrigger,
                SteeringAxisUsed = reading.Kind == InputDeviceKind.RacingWheel ? "Wheel" : reading.SteeringAxisUsed.ToString(),
            };
        }

        public WheelCompatibilityWorker(ILogger<WheelCompatibilityWorker> logger)
        {
            Logger = logger;
            TCPHost = new TcpHost(16581);
            DiagnosticsLog.ExternalSink = message => Logger.LogInformation("{Message}", message);
        }

        protected override Task ExecuteAsync(CancellationToken Cancellation)
        {
            try
            {
                TCPHost.AddService<IWheelCompatibilityService>(this);

                WheelInputTransformer.Start();

                TCPHost.Open();
                DiagnosticsLog.Write("Configuration endpoint listening on TCP 127.0.0.1:16581.");

                if (SettingsManager.HideRealDevice)
                {
                    _ = Task.Run(() => HidHideManager.Enable());
                }
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("Service start failed (is another copy / the old installed service running on port 16581?): " + ex);
                throw;
            }

            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken Cancellation)
        {
            TCPHost.Close();

            // Never leave the real wheel hidden when the service is not running.
            if (HidHideManager.Active)
            {
                HidHideManager.Disable();
            }

            WheelInputTransformer.Stop();

            return Task.CompletedTask;
        }
    }
}
