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

        /// <summary>Raw device buttons (RacingWheelButtons for wheels, GamepadButtons / XInput bits otherwise).</summary>
        public int Buttons { get; set; }

        /// <summary>Windows.Gaming.Input.GamepadButtons sent to the virtual controller.</summary>
        public int OutputButtons { get; set; }

        /// <summary>0 = none, 1 = RacingWheel, 2 = Xbox 360 Speed Wheel (XInput), 3 = Gamepad.</summary>
        public int DeviceKind { get; set; }
        public string DeviceName { get; set; } = "";

        // Raw axes for Speed Wheel / gamepad diagnostics
        public double LeftX { get; set; }
        public double LeftY { get; set; }
        public double RightX { get; set; }
        public double RightY { get; set; }
        public double LeftTrigger { get; set; }
        public double RightTrigger { get; set; }

        /// <summary>Name of the axis actually used for steering (LeftX, RightX, ...).</summary>
        public string SteeringAxisUsed { get; set; } = "";
    }
}
