using System;

namespace XboxWheelCompatibility.CommunicationInterface
{
    [Serializable]
    public class WheelReadingSnapshot
    {
        public bool HasWheel { get; set; }
        /// <summary>Raw steering from the device (-1..1).</summary>
        public double Wheel { get; set; }
        /// <summary>Steering as the game sees it: dead zone + rotation + sensitivity + anti-deadzone.</summary>
        public double WheelAdjusted { get; set; }
        /// <summary>Throttle sent to the virtual device (after source selection and anti-deadzone).</summary>
        public double Throttle { get; set; }
        /// <summary>Brake sent to the virtual device (after source selection and anti-deadzone).</summary>
        public double Brake { get; set; }
        public double Clutch { get; set; }
        public double Handbrake { get; set; }

        /// <summary>Throttle straight from the wheel/device triggers (before merging).</summary>
        public double TriggerThrottle { get; set; }
        /// <summary>Brake straight from the wheel/device triggers (before merging).</summary>
        public double TriggerBrake { get; set; }
        /// <summary>Throttle from the separate pedal set (before merging).</summary>
        public double PedalThrottle { get; set; }
        /// <summary>Brake from the separate pedal set (before merging).</summary>
        public double PedalBrake { get; set; }
        /// <summary>True when the separate pedal set is enabled and connected.</summary>
        public bool PedalsConnected { get; set; }
        /// <summary>Throttle/brake source: 0 = wheel triggers, 1 = separate pedals, 2 = both (max).</summary>
        public int PedalSource { get; set; } = 2;

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
