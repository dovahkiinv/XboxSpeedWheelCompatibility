using System;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Windows.Gaming.Input;

namespace XboxWheelCompatibility.WheelTransformer
{
    /// <summary>
    /// Virtual Xbox 360 controller through the ViGEmBus driver. Unlike the InputInjector gamepad, this
    /// device is visible to XInput and DirectInput games (CarX, older F1 titles, ...) as well as to
    /// Windows.Gaming.Input / GameInput games (F1 25).
    /// It is a wired device, so WheelManager never picks it up as an input source.
    /// </summary>
    public static class ViGEmOutput
    {
        private static ViGEmClient? Client;
        private static IXbox360Controller? Controller;

        public static bool Connected => Controller != null;
        public static string? Error { get; private set; }

        public static bool TryConnect()
        {
            if (Controller != null) return true;

            try
            {
                Client = new ViGEmClient();
                var controller = Client.CreateXbox360Controller();
                controller.AutoSubmitReport = false;
                controller.Connect();
                Controller = controller;
                Error = null;
                DiagnosticsLog.Write("ViGEm virtual Xbox 360 controller connected.");
                return true;
            }
            catch (Exception ex)
            {
                Error = ex.GetType().Name == "VigemBusNotFoundException"
                    ? "ViGEmBus driver not found. Install it from github.com/nefarius/ViGEmBus/releases and reboot."
                    : ex.GetType().Name + ": " + ex.Message;
                DiagnosticsLog.Write("ViGEm connect failed: " + Error);
                Disconnect();
                return false;
            }
        }

        public static void Disconnect()
        {
            try { Controller?.Disconnect(); } catch { }
            try { Client?.Dispose(); } catch { }
            if (Controller != null) DiagnosticsLog.Write("ViGEm virtual controller disconnected.");
            Controller = null;
            Client = null;
        }

        private static short ToThumb(double v) => (short)Math.Round(Math.Clamp(v, -1.0, 1.0) * 32767.0);
        private static byte ToTrigger(double v) => (byte)Math.Round(Math.Clamp(v, 0.0, 1.0) * 255.0);

        public static void Submit(GamepadButtons buttons, double leftTrigger, double rightTrigger, double leftX)
        {
            var c = Controller;
            if (c == null) return;

            c.SetButtonState(Xbox360Button.Up, buttons.HasFlag(GamepadButtons.DPadUp));
            c.SetButtonState(Xbox360Button.Down, buttons.HasFlag(GamepadButtons.DPadDown));
            c.SetButtonState(Xbox360Button.Left, buttons.HasFlag(GamepadButtons.DPadLeft));
            c.SetButtonState(Xbox360Button.Right, buttons.HasFlag(GamepadButtons.DPadRight));
            c.SetButtonState(Xbox360Button.Start, buttons.HasFlag(GamepadButtons.Menu));
            c.SetButtonState(Xbox360Button.Back, buttons.HasFlag(GamepadButtons.View));
            c.SetButtonState(Xbox360Button.LeftThumb, buttons.HasFlag(GamepadButtons.LeftThumbstick));
            c.SetButtonState(Xbox360Button.RightThumb, buttons.HasFlag(GamepadButtons.RightThumbstick));
            c.SetButtonState(Xbox360Button.LeftShoulder, buttons.HasFlag(GamepadButtons.LeftShoulder));
            c.SetButtonState(Xbox360Button.RightShoulder, buttons.HasFlag(GamepadButtons.RightShoulder));
            c.SetButtonState(Xbox360Button.A, buttons.HasFlag(GamepadButtons.A));
            c.SetButtonState(Xbox360Button.B, buttons.HasFlag(GamepadButtons.B));
            c.SetButtonState(Xbox360Button.X, buttons.HasFlag(GamepadButtons.X));
            c.SetButtonState(Xbox360Button.Y, buttons.HasFlag(GamepadButtons.Y));

            c.SetSliderValue(Xbox360Slider.LeftTrigger, ToTrigger(leftTrigger));
            c.SetSliderValue(Xbox360Slider.RightTrigger, ToTrigger(rightTrigger));
            c.SetAxisValue(Xbox360Axis.LeftThumbX, ToThumb(leftX));
            c.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
            c.SetAxisValue(Xbox360Axis.RightThumbX, 0);
            c.SetAxisValue(Xbox360Axis.RightThumbY, 0);

            c.SubmitReport();
        }
    }
}
