using System;
using System.Runtime.InteropServices;

namespace XboxWheelCompatibility.WheelTransformer
{
    /// <summary>
    /// Minimal XInput P/Invoke layer. The Xbox 360 Wireless Receiver for Windows is an XInput device,
    /// so XInput reads the Speed Wheel directly (all axes, triggers and buttons) and also reports the
    /// device sub type (XINPUT_DEVSUBTYPE_WHEEL), which lets us tell a Speed Wheel apart from a normal pad.
    /// This replaces the need for DirectInput/Raw Input for this receiver: the joy.cpl axes
    /// "Z axis / X rotation / Y rotation" are the legacy DirectInput view of the same XInput data.
    /// </summary>
    public static class XInputNative
    {
        public const int MaxControllers = 4;
        public const uint ErrorSuccess = 0;
        public const uint ErrorDeviceNotConnected = 1167;

        public const byte DevTypeGamepad = 0x01;
        public const byte SubTypeGamepad = 0x01;
        public const byte SubTypeWheel = 0x02;
        public const byte SubTypeArcadeStick = 0x03;
        public const byte SubTypeFlightStick = 0x04;
        public const byte SubTypeDancePad = 0x05;
        public const byte SubTypeGuitar = 0x06;
        public const byte SubTypeGuitarAlternate = 0x07;
        public const byte SubTypeDrumKit = 0x08;
        public const byte SubTypeGuitarBass = 0x0B;
        public const byte SubTypeArcadePad = 0x13;

        // XINPUT_GAMEPAD button bits
        public const ushort DPadUp = 0x0001;
        public const ushort DPadDown = 0x0002;
        public const ushort DPadLeft = 0x0004;
        public const ushort DPadRight = 0x0008;
        public const ushort Start = 0x0010;
        public const ushort Back = 0x0020;
        public const ushort LeftThumb = 0x0040;
        public const ushort RightThumb = 0x0080;
        public const ushort LeftShoulder = 0x0100;
        public const ushort RightShoulder = 0x0200;
        public const ushort ButtonA = 0x1000;
        public const ushort ButtonB = 0x2000;
        public const ushort ButtonX = 0x4000;
        public const ushort ButtonY = 0x8000;

        [StructLayout(LayoutKind.Sequential)]
        public struct XInputGamepad
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XInputState
        {
            public uint dwPacketNumber;
            public XInputGamepad Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XInputVibration
        {
            public ushort wLeftMotorSpeed;
            public ushort wRightMotorSpeed;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XInputCapabilities
        {
            public byte Type;
            public byte SubType;
            public ushort Flags;
            public XInputGamepad Gamepad;
            public XInputVibration Vibration;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState14(uint dwUserIndex, out XInputState pState);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetCapabilities")]
        private static extern uint XInputGetCapabilities14(uint dwUserIndex, uint dwFlags, out XInputCapabilities pCapabilities);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState910(uint dwUserIndex, out XInputState pState);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetCapabilities")]
        private static extern uint XInputGetCapabilities910(uint dwUserIndex, uint dwFlags, out XInputCapabilities pCapabilities);

        private static bool _useLegacy = false;
        private static bool _unavailable = false;
        public static string? LoadError { get; private set; }
        public static bool Available => !_unavailable;

        public static bool TryGetState(int index, out XInputState state)
        {
            state = default;
            if (_unavailable) return false;
            try
            {
                uint result = _useLegacy ? XInputGetState910((uint)index, out state) : XInputGetState14((uint)index, out state);
                return result == ErrorSuccess;
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                if (!_useLegacy) { _useLegacy = true; return TryGetState(index, out state); }
                _unavailable = true;
                LoadError = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public static bool TryGetCapabilities(int index, out XInputCapabilities caps)
        {
            caps = default;
            if (_unavailable) return false;
            try
            {
                uint result = _useLegacy ? XInputGetCapabilities910((uint)index, 0, out caps) : XInputGetCapabilities14((uint)index, 0, out caps);
                return result == ErrorSuccess;
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                if (!_useLegacy) { _useLegacy = true; return TryGetCapabilities(index, out caps); }
                _unavailable = true;
                LoadError = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public static string SubTypeName(byte subType) => subType switch
        {
            SubTypeGamepad => "Gamepad",
            SubTypeWheel => "Wheel",
            SubTypeArcadeStick => "Arcade stick",
            SubTypeFlightStick => "Flight stick",
            SubTypeDancePad => "Dance pad",
            SubTypeGuitar or SubTypeGuitarAlternate or SubTypeGuitarBass => "Guitar",
            SubTypeDrumKit => "Drum kit",
            SubTypeArcadePad => "Arcade pad",
            _ => $"Unknown (0x{subType:X2})",
        };

        public static double NormalizeThumb(short value) => value < 0 ? value / 32768.0 : value / 32767.0;
        public static double NormalizeTrigger(byte value) => value / 255.0;
    }
}
