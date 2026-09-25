using System;

namespace XboxWheelCompatibility.CommunicationInterface
{
    /// <summary>Which device is active and how the service is configured to pick/map it.</summary>
    [Serializable]
    public class DeviceStatus
    {
        /// <summary>0 = none, 1 = RacingWheel, 2 = Xbox 360 Speed Wheel (XInput), 3 = Gamepad.</summary>
        public int DeviceKind { get; set; }
        public string DeviceName { get; set; } = "";

        /// <summary>0 = Auto, 1 = RacingWheel only, 2 = Speed Wheel / gamepad only.</summary>
        public int DeviceMode { get; set; }

        /// <summary>0 = Auto, 1 = LeftX, 2 = RightX, 3 = LeftY, 4 = RightY.</summary>
        public int SteeringAxis { get; set; }
        public bool InvertSteering { get; set; }

        /// <summary>Real Xbox 360 receiver hidden from games via HidHide.</summary>
        public bool HideRealDevice { get; set; }
        public string HidHideStatus { get; set; } = "";

        /// <summary>Everything seen in the last device scan (RacingWheels, XInput slots, Gamepads).</summary>
        public string[] ScanLines { get; set; } = Array.Empty<string>();

        /// <summary>Most recent diagnostic log lines.</summary>
        public string[] RecentLog { get; set; } = Array.Empty<string>();
        public string LogPath { get; set; } = "";
    }
}
