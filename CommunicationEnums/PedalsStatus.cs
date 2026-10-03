using System;

namespace XboxWheelCompatibility.CommunicationInterface
{
    /// <summary>State of the separate pedal set (combined single-axis pedals).</summary>
    [Serializable]
    public class PedalsStatus
    {
        public bool Enabled { get; set; }
        public bool Connected { get; set; }
        public string ActiveDevice { get; set; } = "";

        /// <summary>Selected device key "VID:PID", empty = auto.</summary>
        public string DeviceKey { get; set; } = "";
        /// <summary>Available devices as "VID:PID|display name".</summary>
        public string[] Devices { get; set; } = Array.Empty<string>();

        /// <summary>0 = X, 1 = Y, 2 = Z, 3 = R, 4 = U, 5 = V.</summary>
        public int Axis { get; set; } = 1;
        public bool Swap { get; set; }
        public double DeadZone { get; set; }
        public double Center { get; set; } = 0.5;
        /// <summary>Pedal travel giving 100 % (0.1..1.0).</summary>
        public double ThrottleRange { get; set; } = 1.0;
        public double BrakeRange { get; set; } = 1.0;

        /// <summary>0 = wheel triggers, 1 = separate pedals, 2 = both (max) - default.</summary>
        public int Source { get; set; } = 2;

        /// <summary>Anti-deadzone applied to the final throttle / brake, 0..0.40.</summary>
        public double ThrottleAntiDeadZone { get; set; }
        public double BrakeAntiDeadZone { get; set; }

        /// <summary>Raw axes 0..1 (X, Y, Z, R, U, V).</summary>
        public double[] RawAxes { get; set; } = new double[6];
        public double Throttle { get; set; }
        public double Brake { get; set; }
    }
}
