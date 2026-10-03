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
            SettingsManager.Sensitivity = Sensitivity;
        }

        public double GetDeadZone()
        {
            return SettingsManager.DeadZone;
        }

        public void SetDeadZone(double DeadZone)
        {
            SettingsManager.DeadZone = DeadZone;
        }

        public double GetRotationDegrees() => SettingsManager.RotationDegrees;
        public void SetRotationDegrees(double Degrees) => SettingsManager.RotationDegrees = Degrees;
        public double GetPhysicalDegrees() => SettingsManager.PhysicalDegrees;
        public void SetPhysicalDegrees(double Degrees) => SettingsManager.PhysicalDegrees = Degrees;

        /// <summary>Rounds a slider value and skips the write (and the log line) when nothing changed.</summary>
        private static bool SetIfChanged(double current, double value, out double rounded)
        {
            rounded = Math.Round(Math.Clamp(value, 0.0, SettingsManager.MaxAntiDeadZone), 2);
            if (Math.Abs(current - rounded) < 0.005) return false;
            return true;
        }

        public void SetSteeringAntiDeadZone(double Value)
        {
            if (!SetIfChanged(SettingsManager.SteeringAntiDeadZone, Value, out double v)) return;
            SettingsManager.SteeringAntiDeadZone = v;
            DiagnosticsLog.Write($"Steering anti-deadzone set to {v * 100:0}% ({v:0.00})");
        }

        public void SetThrottleAntiDeadZone(double Value)
        {
            if (!SetIfChanged(SettingsManager.ThrottleAntiDeadZone, Value, out double v)) return;
            SettingsManager.ThrottleAntiDeadZone = v;
            DiagnosticsLog.Write($"Throttle anti-deadzone set to {v * 100:0}% ({v:0.00})");
        }

        public void SetBrakeAntiDeadZone(double Value)
        {
            if (!SetIfChanged(SettingsManager.BrakeAntiDeadZone, Value, out double v)) return;
            SettingsManager.BrakeAntiDeadZone = v;
            DiagnosticsLog.Write($"Brake anti-deadzone set to {v * 100:0}% ({v:0.00})");
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
                VJoyAxisWarning = VJoyOutput.Connected ? VJoyOutput.AxisWarning : "",
                VJoyPedalAxisMode = (int)SettingsManager.VJoyPedalAxisMode,
                VJoyInvertThrottle = SettingsManager.VJoyInvertThrottle,
                VJoyInvertBrake = SettingsManager.VJoyInvertBrake,
                SteeringAntiDeadZone = SettingsManager.SteeringAntiDeadZone,
                RotationDegrees = SettingsManager.RotationDegrees,
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
                Source = (int)SettingsManager.PedalSource,
                ThrottleAntiDeadZone = SettingsManager.ThrottleAntiDeadZone,
                BrakeAntiDeadZone = SettingsManager.BrakeAntiDeadZone,
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
            DiagnosticsLog.Write($"Pedals: enabled={Enabled} device='{DeviceKey}' axis={Axis} swap={Swap} deadzone={DeadZone:0.00}");
        }

        public string CalibratePedalsCenter() => PedalsManager.CalibrateCenter();

        public void SetPedalSource(int Source)
        {
            SettingsManager.PedalSource = (PedalSourceMode)Source;
            DiagnosticsLog.Write("Throttle/brake source set to " + SettingsManager.PedalSource);
        }

        public void SetPedalsRange(double ThrottleRange, double BrakeRange)
        {
            SettingsManager.ThrottleRange = ThrottleRange;
            SettingsManager.BrakeRange = BrakeRange;
        }

        public void SetVJoy(bool Enabled, int DeviceId)
        {
            SettingsManager.VJoyDeviceId = DeviceId;
            SettingsManager.VJoyEnabled = Enabled;
            DiagnosticsLog.Write($"vJoy wheel {(Enabled ? "enabled" : "disabled")} (device {SettingsManager.VJoyDeviceId})");
            InjectionManager.ReconfigureOutputs();
        }

        public void SetVJoyPedalAxes(int Mode, bool InvertThrottle, bool InvertBrake)
        {
            SettingsManager.VJoyPedalAxisMode = (PedalAxisMode)Mode;
            SettingsManager.VJoyInvertThrottle = InvertThrottle;
            SettingsManager.VJoyInvertBrake = InvertBrake;
            DiagnosticsLog.Write($"vJoy pedal axes: {SettingsManager.VJoyPedalAxisMode}, invert throttle={InvertThrottle}, invert brake={InvertBrake}");
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

            double adjusted = InjectionManager.ApplySensitivity(reading.Steering);

            return new WheelReadingSnapshot
            {
                HasWheel = true,
                Wheel = reading.Steering,
                WheelAdjusted = adjusted,
                WheelGameOutput = InjectionManager.ApplySteeringAntiDeadZone(adjusted),
                Throttle = reading.Throttle,
                Brake = reading.Brake,
                ThrottleGameOutput = InjectionManager.ApplyThrottleAntiDeadZone(reading.Throttle),
                BrakeGameOutput = InjectionManager.ApplyBrakeAntiDeadZone(reading.Brake),
                TriggerThrottle = reading.TriggerThrottle,
                TriggerBrake = reading.TriggerBrake,
                PedalThrottle = reading.PedalThrottle,
                PedalBrake = reading.PedalBrake,
                PedalSource = (int)SettingsManager.PedalSource,
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
