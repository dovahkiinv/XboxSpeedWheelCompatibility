using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Gaming.Input;

namespace XboxWheelCompatibility.CommunicationInterface
{
    public interface IWheelCompatibilityService
    {
        public int GetMainWheelIndex();
        public void Stop();
        public void Start();
        public double GetSensitivity();
        public void SetSensitivity(double Sensitivity);
        public WheelReadingSnapshot GetReadingSnapshot();
        public InjectionDiagnostics GetInjectionDiagnostics();
    }
}
