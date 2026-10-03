using System;

namespace XboxWheelCompatibility.CommunicationInterface
{
    [Serializable]
    public class WheelReadingSnapshot
    {
        public bool HasWheel { get; set; }
        public double Wheel { get; set; }
        /// <summary>Steering after dead zone, rotation angle and sensitivity.</summary>
        public double WheelAdjusted { get; set; }
        /// <summary>Steering actually sent to the game (after the anti-deadzone).</summary>
        public double WheelGameOutput { get; set; }
        /// <summary>Throttle / brake after the pedal source was merged.</summary>
        public double Throttle { get; set; }
        public double Brake { get; set; }
        /// <summary>Throttle / brake actually sent to the game (after the anti-deadzone).</summary>
        public double ThrottleGameOutput { get; set; }
        public double BrakeGameOutput { get; set; }

        /// <summary>Throttle / brake reported by the device itself (wheel triggers).</summary>
        public double TriggerThrottle { get; set; }
        public double TriggerBrake { get; set; }
        /// <summary>Throttle / brake from the separate pedal set (0 when the pedals are off).</summary>
        public double PedalThrottle { get; set; }
        public double PedalBrake { get; set; }
        /// <summary>0 = wheel triggers, 1 = separate pedals, 2 = both (max).</summary>
        public int PedalSource { get; set; }

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
