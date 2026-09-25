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
            return RacingWheel.RacingWheels.ToList().IndexOf(WheelManager.MainWheel);
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
            var wheel = WheelManager.MainWheel;
            if (wheel == null)
            {
                return new WheelReadingSnapshot { HasWheel = false };
            }

            try
            {
                var reading = wheel.GetCurrentReading();
                return new WheelReadingSnapshot
                {
                    HasWheel = true,
                    Wheel = reading.Wheel,
                    WheelAdjusted = InjectionManager.ApplySensitivity(reading.Wheel),
                    Throttle = reading.Throttle,
                    Brake = reading.Brake,
                    Clutch = reading.Clutch,
                    Handbrake = reading.Handbrake,
                    Buttons = (int)reading.Buttons,
                };
            }
            catch
            {
                return new WheelReadingSnapshot { HasWheel = false };
            }
        }

        public WheelCompatibilityWorker(ILogger<WheelCompatibilityWorker> logger)
        {
            Logger = logger;
            TCPHost = new TcpHost(16581);
        }

        protected override Task ExecuteAsync(CancellationToken Cancellation)
        {
            TCPHost.AddService<IWheelCompatibilityService>(this);

            WheelInputTransformer.Start();

            TCPHost.Open();

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
