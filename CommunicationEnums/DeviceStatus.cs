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
        public bool HideXInputInterface { get; set; } = true;

        /// <summary>Virtual DirectInput wheel (vJoy).</summary>
        public bool VJoyEnabled { get; set; }
        public int VJoyDeviceId { get; set; } = 1;
        public string VJoyStatus { get; set; } = "";
        public double RotationDegrees { get; set; }

        /// <summary>Steering anti-deadzone 0..0.40 (fraction). 0 = off.</summary>
        public double SteeringAntiDeadzone { get; set; }

        /// <summary>vJoy pedal mapping: 0 = separate Y/Z axes, 1 = one centred Y axis.</summary>
        public int VJoyPedalAxisMode { get; set; }
        public bool VJoyInvertThrottle { get; set; }
        public bool VJoyInvertBrake { get; set; }
        /// <summary>True when the vJoy device is missing one of the X/Y/Z axes.</summary>
        public bool VJoyAxisWarning { get; set; }
        /// <summary>What to enable in "Configure vJoy" (empty when all axes exist).</summary>
        public string VJoyAxisMessage { get; set; } = "";

        /// <summary>Everything seen in the last device scan (RacingWheels, XInput slots, Gamepads).</summary>
        public string[] ScanLines { get; set; } = Array.Empty<string>();

        /// <summary>Most recent diagnostic log lines.</summary>
        public string[] RecentLog { get; set; } = Array.Empty<string>();
        public string LogPath { get; set; } = "";
    }
}
