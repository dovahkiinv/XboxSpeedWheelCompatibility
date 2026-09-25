using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Gaming.Input;
using Windows.UI.Input.Preview.Injection;

namespace XboxWheelCompatibility.WheelTransformer
{
    public class InjectionManager
    {
        private static InputInjector? Injector;
        private static string? InjectorCreationError;

        private static long _injectAttempts;
        private static long _injectSuccesses;
        private static string? _lastInjectError;

        public static bool InjectorAvailable => Injector != null;
        public static string? InjectorErrorMessage => InjectorCreationError;
        public static long InjectAttempts => Interlocked.Read(ref _injectAttempts);
        public static long InjectSuccesses => Interlocked.Read(ref _injectSuccesses);
        public static string? LastInjectError => _lastInjectError;

        private static void EnsureInjector()
        {
            if (Injector != null) return;

            try
            {
                Injector = InputInjector.TryCreate();
                if (Injector == null)
                {
                    InjectorCreationError = "Windows could not create the gamepad injector. Check the Xbox Accessory Management Service (XboxGipSvc), then restart WheelCompatibilityService.";
                }
            }
            catch (Exception ex)
            {
                InjectorCreationError = ex.GetType().Name + ": " + ex.Message;
            }
        }

        public static double ApplySensitivity(double wheelValue)
        {
            double sensitivity = SettingsManager.Sensitivity;
            if (sensitivity <= 0) return wheelValue;
            if (Math.Abs(sensitivity - 1.0) < 0.0001) return wheelValue;

            double sign = wheelValue < 0 ? -1.0 : 1.0;
            double magnitude = Math.Min(1.0, Math.Abs(wheelValue));
            double exponent = 1.0 / sensitivity;
            double curved = Math.Pow(magnitude, exponent);

            return Math.Clamp(sign * curved, -1.0, 1.0);
        }

        private static void InjectCurrentReading()
        {
            if (Injector == null) return;
            if (WheelManager.MainWheel == null) return;

            RacingWheelReading WheelReading = WheelManager.MainWheel.GetCurrentReading();

            double adjustedWheel = ApplySensitivity(WheelReading.Wheel);

            Interlocked.Increment(ref _injectAttempts);

            try
            {
                Injector.InjectGamepadInput(new InjectedInputGamepadInfo(
                        new GamepadReading(
                            (ulong)DateTime.UtcNow.Ticks,
                            (GamepadButtons)(
                                (WheelReading.Buttons.HasFlag(RacingWheelButtons.DPadDown) ? (int)GamepadButtons.DPadDown : 0) |
                                (WheelReading.Buttons.HasFlag(RacingWheelButtons.DPadUp) ? (int)GamepadButtons.DPadUp : 0) |
                                (WheelReading.Buttons.HasFlag(RacingWheelButtons.DPadLeft) ? (int)GamepadButtons.DPadLeft : 0) |
                                (WheelReading.Buttons.HasFlag(RacingWheelButtons.DPadRight) ? (int)GamepadButtons.DPadRight : 0) |
                                (WheelReading.Buttons.HasFlag(RacingWheelButtons.NextGear) ? (int)GamepadButtons.RightShoulder : 0) |
                                (WheelReading.Buttons.HasFlag(RacingWheelButtons.PreviousGear) ? (int)GamepadButtons.LeftShoulder : 0) |
                                (WheelReading.Buttons.HasFlag(RacingWheelButtons.Button1) ? (int)GamepadButtons.Menu : 0) |
                                (WheelReading.Buttons.HasFlag(RacingWheelButtons.Button2) ? (int)GamepadButtons.View : 0) |
                                (WheelReading.Buttons.HasFlag(RacingWheelButtons.Button3) ? (int)GamepadButtons.A : 0) |
                                (WheelReading.Buttons.HasFlag(RacingWheelButtons.Button4) ? (int)GamepadButtons.B : 0) |
                                (WheelReading.Buttons.HasFlag(RacingWheelButtons.Button5) ? (int)GamepadButtons.X : 0) |
                                (WheelReading.Buttons.HasFlag(RacingWheelButtons.Button6) ? (int)GamepadButtons.Y : 0)
                            ),
                            WheelReading.Brake, WheelReading.Throttle,
                            adjustedWheel, 0,
                            0, 0
                        )
                    )
                );
                Interlocked.Increment(ref _injectSuccesses);
                _lastInjectError = null;
            }
            catch (Exception ex)
            {
                _lastInjectError = ex.GetType().Name + ": " + ex.Message;
            }
        }

        public static void Initialize()
        {
            _ = SettingsManager.Sensitivity;
            EnsureInjector();

            LifecycleManager.Tick += (object? Sender, EventArgs Event) =>
            {
                if (RacingWheel.RacingWheels.Count > WheelManager.ActiveWheels.Count)
                {
                    WheelManager.UpdateWheels();
                }

                if (WheelManager.MainWheel == null) return;

                InjectCurrentReading();
            };
        }

        public static void Destroy()
        {
        }
    }
}
