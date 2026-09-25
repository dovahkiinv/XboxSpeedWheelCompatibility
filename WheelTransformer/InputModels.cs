using Windows.Gaming.Input;

namespace XboxWheelCompatibility.WheelTransformer
{
    /// <summary>Which kind of physical device is currently driving the virtual controller.</summary>
    public enum InputDeviceKind
    {
        None = 0,
        /// <summary>A real wheel exposed through Windows.Gaming.Input.RacingWheel (original behaviour).</summary>
        RacingWheel = 1,
        /// <summary>An XInput device reporting XINPUT_DEVSUBTYPE_WHEEL (Xbox 360 Wireless Speed Wheel).</summary>
        XInputSpeedWheel = 2,
        /// <summary>A Windows.Gaming.Input.Gamepad fallback (Speed Wheel seen as a normal controller).</summary>
        Gamepad = 3,
    }

    /// <summary>Device selection policy.</summary>
    public enum DeviceSelectionMode
    {
        /// <summary>Real RacingWheel first, then Xbox 360 Speed Wheel (XInput), then gamepad fallback.</summary>
        Auto = 0,
        RacingWheelOnly = 1,
        SpeedWheelOnly = 2,
    }

    /// <summary>Which source axis is used as steering for Speed Wheel / gamepad devices.</summary>
    public enum SteeringAxisSource
    {
        /// <summary>LeftThumbstickX unless another stick axis is clearly the one that moves.</summary>
        Auto = 0,
        LeftX = 1,
        RightX = 2,
        LeftY = 3,
        RightY = 4,
    }

    /// <summary>One normalised reading from any supported device, plus the raw data for diagnostics.</summary>
    public sealed class UnifiedReading
    {
        public InputDeviceKind Kind { get; init; }
        public string DeviceName { get; init; } = "";

        /// <summary>Raw steering, -1..1, before sensitivity.</summary>
        public double Steering { get; init; }
        public double Throttle { get; init; }
        public double Brake { get; init; }
        public double Clutch { get; init; }
        public double Handbrake { get; init; }

        /// <summary>Buttons that will be injected into the virtual Xbox controller.</summary>
        public GamepadButtons OutputButtons { get; init; }

        /// <summary>Original RacingWheel buttons (only for RacingWheel devices).</summary>
        public RacingWheelButtons WheelButtons { get; init; }

        // Raw axes (Speed Wheel / gamepad) for diagnostics.
        public double LeftX { get; init; }
        public double LeftY { get; init; }
        public double RightX { get; init; }
        public double RightY { get; init; }
        public double LeftTrigger { get; init; }
        public double RightTrigger { get; init; }
        public int RawButtons { get; init; }
        public SteeringAxisSource SteeringAxisUsed { get; init; }
    }
}
