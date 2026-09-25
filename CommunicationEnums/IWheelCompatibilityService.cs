using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XboxWheelCompatibility.CommunicationInterface
{
    public interface IWheelCompatibilityService
    {
        /// <summary>Index of the active RacingWheel, 0 for a Speed Wheel / gamepad, -1 if nothing is active.</summary>
        public int GetMainWheelIndex();
        public void Stop();
        public void Start();
        public double GetSensitivity();
        public void SetSensitivity(double Sensitivity);
        public WheelReadingSnapshot GetReadingSnapshot();
        public InjectionDiagnostics GetInjectionDiagnostics();

        // Speed Wheel / gamepad support
        public DeviceStatus GetDeviceStatus();
        public void SetDeviceMode(int Mode);
        public void SetSteeringAxis(int Axis);
        public void SetInvertSteering(bool Invert);
    }
}
