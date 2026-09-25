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

        public DeviceStatus GetDeviceStatus()
        {
            return new DeviceStatus
            {
                DeviceKind = (int)WheelManager.ActiveKind,
                DeviceName = WheelManager.ActiveDeviceName,
                DeviceMode = (int)SettingsManager.DeviceMode,
                SteeringAxis = (int)SettingsManager.SteeringAxis,
                InvertSteering = SettingsManager.InvertSteering,
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

        public InjectionDiagnostics GetInjectionDiagnostics()
        {
            return new InjectionDiagnostics
            {
                InjectorAvailable = InjectionManager.InjectorAvailable,
                InjectorErrorMessage = InjectionManager.InjectorErrorMessage,
                InjectAttempts = InjectionManager.InjectAttempts,
                InjectSuccesses = InjectionManager.InjectSuccesses,
                LastInjectError = InjectionManager.LastInjectError,
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
                WheelAdjusted = InjectionManager.ApplySensitivity(reading.Steering),
                Throttle = reading.Throttle,
                Brake = reading.Brake,
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

            WheelInputTransformer.Stop();

            return Task.CompletedTask;
        }
    }
}
