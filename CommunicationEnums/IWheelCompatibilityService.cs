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
        public double GetDeadZone();
        public void SetDeadZone(double DeadZone);
        public double GetRotationDegrees();
        public void SetRotationDegrees(double Degrees);
        public double GetPhysicalDegrees();
        public void SetPhysicalDegrees(double Degrees);
        public WheelReadingSnapshot GetReadingSnapshot();
        public InjectionDiagnostics GetInjectionDiagnostics();

        // Speed Wheel / gamepad support
        public DeviceStatus GetDeviceStatus();
        public void SetDeviceMode(int Mode);
        public void SetSteeringAxis(int Axis);
        public void SetInvertSteering(bool Invert);
        public void SetOutputMode(int Mode);
        /// <summary>Hide/unhide the real Xbox 360 receiver from games (HidHide). Returns a status message.</summary>
        public string SetHideRealDevice(bool Hide);
    }
}
