using System;

namespace XboxWheelCompatibility.CommunicationInterface
{
    [Serializable]
    public class WheelReadingSnapshot
    {
        public bool HasWheel { get; set; }
        public double Wheel { get; set; }
        public double WheelAdjusted { get; set; }
        public double Throttle { get; set; }
        public double Brake { get; set; }
        public double Clutch { get; set; }
        public double Handbrake { get; set; }
        public int Buttons { get; set; }
    }
}
