using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Windows.Gaming.Input;
using XboxWheelCompatibility.CommunicationInterface;

namespace WheelCompatibilityConfigurator
{
    public partial class MainWindow : Window
    {
        private readonly Communicator ServiceCommunicator = new();
        private readonly CancellationTokenSource StatusLoopCancellation = new();
        private bool SuppressSliderEvent = false;
        private bool SuppressDeviceEvents = false;
        private bool DeviceSettingsLoaded = false;

        private static readonly Brush ButtonIdleBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        private static readonly Brush ButtonActiveBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0xA8, 0x4F));

        public MainWindow()
        {
            InitializeComponent();

            ButtonIdleBrush.Freeze();
            ButtonActiveBrush.Freeze();

            Closed += (_, _) => StatusLoopCancellation.Cancel();

            _ = InitializeSensitivityAsync();
            _ = StatusLoop(StatusLoopCancellation.Token);
            _ = TesterLoop(StatusLoopCancellation.Token);
            _ = DiagnosticsLoop(StatusLoopCancellation.Token);
            _ = DeviceStatusLoop(StatusLoopCancellation.Token);
        }

        private static string DeviceKindLabel(int kind) => kind switch
        {
            1 => "Racing wheel",
            2 => "Speed Wheel",
            3 => "Controller (gamepad mode)",
            _ => "No device",
        };

        private async Task DeviceStatusLoop(CancellationToken Cancellation)
        {
            while (!Cancellation.IsCancellationRequested)
            {
                DeviceStatus? status = await Task.Run(() => ServiceCommunicator.TryGetDeviceStatus());
                if (Cancellation.IsCancellationRequested) return;

                if (status != null)
                {
                    if (!DeviceSettingsLoaded)
                    {
                        SuppressDeviceEvents = true;
                        DeviceModeCombo.SelectedIndex = Math.Clamp(status.DeviceMode, 0, 2);
                        SteeringAxisCombo.SelectedIndex = Math.Clamp(status.SteeringAxis, 0, 4);
                        InvertSteeringCheck.IsChecked = status.InvertSteering;
                        SuppressDeviceEvents = false;
                        DeviceSettingsLoaded = true;
                    }

                    ScanText.Text = status.ScanLines.Length == 0
                        ? "No RacingWheel, XInput or Gamepad devices found."
                        : string.Join(Environment.NewLine, status.ScanLines);
                    LogPathText.Text = "Log file: " + status.LogPath;
                    LogText.Text = string.Join(Environment.NewLine, status.RecentLog);
                    LogText.ScrollToEnd();
                }

                try { await Task.Delay(1000, Cancellation); }
                catch (TaskCanceledException) { return; }
            }
        }

        private void DeviceModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SuppressDeviceEvents || DeviceModeCombo.SelectedIndex < 0) return;
            int mode = DeviceModeCombo.SelectedIndex;
            _ = Task.Run(() => ServiceCommunicator.TrySetDeviceMode(mode));
        }

        private bool OutputModeLoaded = false;

        private void OutputModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SuppressDeviceEvents || OutputModeCombo.SelectedIndex < 0) return;
            int mode = OutputModeCombo.SelectedIndex;
            _ = Task.Run(() => ServiceCommunicator.TrySetOutputMode(mode));
        }

        private void SteeringAxisCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SuppressDeviceEvents || SteeringAxisCombo.SelectedIndex < 0) return;
            int axis = SteeringAxisCombo.SelectedIndex;
            _ = Task.Run(() => ServiceCommunicator.TrySetSteeringAxis(axis));
        }

        private void InvertSteeringCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (SuppressDeviceEvents) return;
            bool invert = InvertSteeringCheck.IsChecked == true;
            _ = Task.Run(() => ServiceCommunicator.TrySetInvertSteering(invert));
        }

        private async Task DiagnosticsLoop(CancellationToken Cancellation)
        {
            while (!Cancellation.IsCancellationRequested)
            {
                InjectionDiagnostics? diag = await Task.Run(() => ServiceCommunicator.TryGetInjectionDiagnostics());
                if (Cancellation.IsCancellationRequested) return;
                UpdateInjectionStatus(diag);

                try { await Task.Delay(1000, Cancellation); }
                catch (TaskCanceledException) { return; }
            }
        }

        private void UpdateInjectionStatus(InjectionDiagnostics? diag)
        {
            if (diag == null)
            {
                InjectionStatusBorder.Visibility = Visibility.Collapsed;
                return;
            }

            if (!OutputModeLoaded)
            {
                SuppressDeviceEvents = true;
                OutputModeCombo.SelectedIndex = Math.Clamp(diag.OutputMode, 0, 3);
                SuppressDeviceEvents = false;
                OutputModeLoaded = true;
            }

            if (!diag.InjectorAvailable)
            {
                InjectionStatusBorder.Background = new SolidColorBrush(Color.FromRgb(0xFD, 0xE0, 0xE0));
                InjectionStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD6, 0x28, 0x28));
                InjectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x1A, 0x1A));
                InjectionStatusText.Text = "Gamepad injection unavailable. " + (diag.InjectorErrorMessage ?? "Unknown reason.");
                InjectionStatusBorder.Visibility = Visibility.Visible;
                return;
            }

            if (!string.IsNullOrEmpty(diag.LastInjectError))
            {
                InjectionStatusBorder.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xE1));
                InjectionStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0xC7, 0x66));
                InjectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x54, 0x00));
                InjectionStatusText.Text = $"Injection error ({diag.InjectSuccesses}/{diag.InjectAttempts} succeeded): {diag.LastInjectError}";
                InjectionStatusBorder.Visibility = Visibility.Visible;
                return;
            }

            if (diag.InjectAttempts == 0)
            {
                InjectionStatusBorder.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
                InjectionStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0));
                InjectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
                InjectionStatusText.Text = "Injector created. No injection attempts yet (waiting on wheel input).";
                InjectionStatusBorder.Visibility = Visibility.Visible;
                return;
            }

            InjectionStatusBorder.Background = new SolidColorBrush(Color.FromRgb(0xE0, 0xF4, 0xE6));
            InjectionStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0xA8, 0x4F));
            InjectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x5E, 0x2D));
            InjectionStatusText.Text = $"Injection OK via {diag.ActiveOutputs}: {diag.InjectSuccesses}/{diag.InjectAttempts} succeeded."
                + (!diag.ViGEmActive && !string.IsNullOrEmpty(diag.ViGEmError) ? " ViGEm: " + diag.ViGEmError : "");
            InjectionStatusBorder.Visibility = Visibility.Visible;
        }

        private async Task InitializeSensitivityAsync()
        {
            double? deadZone = await Task.Run(() => ServiceCommunicator.TryGetDeadZone());
            if (deadZone.HasValue)
            {
                SuppressSliderEvent = true;
                DeadZoneSlider.Value = deadZone.Value;
                DeadZoneValueLabel.Content = deadZone.Value.ToString("0.00", CultureInfo.InvariantCulture);
                SuppressSliderEvent = false;
            }

            double? rotation = await Task.Run(() => ServiceCommunicator.TryGetRotationDegrees());
            double? physical = await Task.Run(() => ServiceCommunicator.TryGetPhysicalDegrees());
            SuppressSliderEvent = true;
            if (rotation.HasValue) { RotationSlider.Value = rotation.Value; CurrentRotation = rotation.Value; }
            if (physical.HasValue) { PhysicalSlider.Value = physical.Value; CurrentPhysical = physical.Value; }
            RotationValueLabel.Content = $"{RotationSlider.Value:0}°";
            PhysicalValueLabel.Content = $"{PhysicalSlider.Value:0}°";
            SuppressSliderEvent = false;

            double? sensitivity = await Task.Run(() => ServiceCommunicator.TryGetSensitivity());
            if (!sensitivity.HasValue) return;

            SuppressSliderEvent = true;
            SensitivitySlider.Value = sensitivity.Value;
            SensitivityValueLabel.Content = sensitivity.Value.ToString("0.00", CultureInfo.InvariantCulture);
            SuppressSliderEvent = false;
        }

        private async Task StatusLoop(CancellationToken Cancellation)
        {
            while (!Cancellation.IsCancellationRequested)
            {
                int? index = await Task.Run(() => ServiceCommunicator.TryGetMainWheelIndex());
                DeviceStatus? status = index.HasValue ? await Task.Run(() => ServiceCommunicator.TryGetDeviceStatus()) : null;

                if (Cancellation.IsCancellationRequested) return;

                if (!index.HasValue)
                {
                    ServiceStatusIndicator.Content = "Service unreachable";
                }
                else if (status != null && status.DeviceKind != 0)
                {
                    string label = DeviceKindLabel(status.DeviceKind) + " connected";
                    ServiceStatusIndicator.Content = string.IsNullOrWhiteSpace(status.DeviceName)
                        ? label
                        : label + ": " + status.DeviceName;
                }
                else if (index.Value == -1)
                {
                    ServiceStatusIndicator.Content = "No wheel or Speed Wheel connected";
                }
                else
                {
                    ServiceStatusIndicator.Content = "Wheel connected";
                }

                try { await Task.Delay(500, Cancellation); }
                catch (TaskCanceledException) { return; }
            }
        }

        private async Task TesterLoop(CancellationToken Cancellation)
        {
            while (!Cancellation.IsCancellationRequested)
            {
                WheelReadingSnapshot? snapshot = await Task.Run(() => ServiceCommunicator.TryGetReadingSnapshot());

                if (Cancellation.IsCancellationRequested) return;

                UpdateTesterUI(snapshot);

                try { await Task.Delay(33, Cancellation); }
                catch (TaskCanceledException) { return; }
            }
        }

        private void UpdateTesterUI(WheelReadingSnapshot? snapshot)
        {
            bool active = snapshot != null && snapshot.HasWheel;

            double wheel = active ? snapshot!.Wheel : 0;
            double wheelAdjusted = active ? snapshot!.WheelAdjusted : 0;
            double throttle = active ? snapshot!.Throttle : 0;
            double brake = active ? snapshot!.Brake : 0;
            double clutch = active ? snapshot!.Clutch : 0;
            double handbrake = active ? snapshot!.Handbrake : 0;
            GamepadButtons buttons = active ? (GamepadButtons)snapshot!.OutputButtons : GamepadButtons.None;

            if (!active)
            {
                RawAxesText.Text = snapshot == null ? "Service unreachable" : "No active device";
            }
            else if (snapshot!.DeviceKind == 1)
            {
                RawAxesText.Text = string.Format(CultureInfo.InvariantCulture,
                    "RacingWheel  wheel={0:+0.000;-0.000; 0.000}  thr={1:0.00}  brk={2:0.00}  raw buttons=0x{3:X}",
                    snapshot.Wheel, snapshot.Throttle, snapshot.Brake, snapshot.Buttons);
            }
            else
            {
                RawAxesText.Text = string.Format(CultureInfo.InvariantCulture,
                    "LX={0:+0.000;-0.000; 0.000}  LY={1:+0.000;-0.000; 0.000}\nRX={2:+0.000;-0.000; 0.000}  RY={3:+0.000;-0.000; 0.000}\nLT={4:0.000}  RT={5:0.000}  buttons=0x{6:X4}\nSteering uses: {7}",
                    snapshot.LeftX, snapshot.LeftY, snapshot.RightX, snapshot.RightY,
                    snapshot.LeftTrigger, snapshot.RightTrigger, snapshot.Buttons, snapshot.SteeringAxisUsed);
            }

            // Steering wheel icons in real degrees: input = physical turn, output = virtual wheel angle.
            double inputDeg = Math.Clamp(wheel, -1.0, 1.0) * CurrentPhysical;
            double outputDeg = Math.Clamp(wheelAdjusted, -1.0, 1.0) * CurrentRotation / 2.0;
            WheelInputRotation.Angle = inputDeg;
            WheelOutputRotation.Angle = outputDeg;

            WheelValueLabel.Text = wheel.ToString("+0.00;-0.00; 0.00", CultureInfo.InvariantCulture)
                + "  (" + inputDeg.ToString("+0;-0;0", CultureInfo.InvariantCulture) + "°)";
            WheelAdjustedLabel.Text = wheelAdjusted.ToString("+0.00;-0.00; 0.00", CultureInfo.InvariantCulture)
                + "  (" + outputDeg.ToString("+0;-0;0", CultureInfo.InvariantCulture) + "°)";

            ThrottleBar.Value = Math.Clamp(throttle, 0, 1);
            ThrottleValueLabel.Text = ((int)Math.Round(throttle * 100)).ToString() + "%";

            BrakeBar.Value = Math.Clamp(brake, 0, 1);
            BrakeValueLabel.Text = ((int)Math.Round(brake * 100)).ToString() + "%";

            ClutchBar.Value = Math.Clamp(clutch, 0, 1);
            ClutchValueLabel.Text = ((int)Math.Round(clutch * 100)).ToString() + "%";

            HandbrakeBar.Value = Math.Clamp(handbrake, 0, 1);
            HandbrakeValueLabel.Text = ((int)Math.Round(handbrake * 100)).ToString() + "%";

            SetButton(DotDPadUp, buttons.HasFlag(GamepadButtons.DPadUp));
            SetButton(DotDPadDown, buttons.HasFlag(GamepadButtons.DPadDown));
            SetButton(DotDPadLeft, buttons.HasFlag(GamepadButtons.DPadLeft));
            SetButton(DotDPadRight, buttons.HasFlag(GamepadButtons.DPadRight));
            SetButton(DotPrevGear, buttons.HasFlag(GamepadButtons.LeftShoulder));
            SetButton(DotNextGear, buttons.HasFlag(GamepadButtons.RightShoulder));
            SetButton(DotB1, buttons.HasFlag(GamepadButtons.Menu));
            SetButton(DotB2, buttons.HasFlag(GamepadButtons.View));
            SetButton(DotB3, buttons.HasFlag(GamepadButtons.A));
            SetButton(DotB4, buttons.HasFlag(GamepadButtons.B));
            SetButton(DotB5, buttons.HasFlag(GamepadButtons.X));
            SetButton(DotB6, buttons.HasFlag(GamepadButtons.Y));
        }

        private static void SetButton(System.Windows.Controls.Border dot, bool pressed)
        {
            dot.Background = pressed ? ButtonActiveBrush : ButtonIdleBrush;
        }

        private void SensitivitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SuppressSliderEvent) return;

            double value = Math.Round(e.NewValue, 2);
            SensitivityValueLabel.Content = value.ToString("0.00", CultureInfo.InvariantCulture);

            _ = Task.Run(() => ServiceCommunicator.TrySetSensitivity(value));
        }

        private void DeadZoneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DeadZoneValueLabel == null) return;
            double value = Math.Round(e.NewValue, 2);
            DeadZoneValueLabel.Content = value.ToString("0.00", CultureInfo.InvariantCulture);
            if (SuppressSliderEvent || !IsLoaded) return;

            _ = Task.Run(() => ServiceCommunicator.TrySetDeadZone(value));
        }

        private double CurrentRotation = 180;
        private double CurrentPhysical = 90;

        private void RotationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (RotationValueLabel == null) return;
            double value = Math.Round(e.NewValue);
            CurrentRotation = value;
            RotationValueLabel.Content = $"{value:0}°";
            if (SuppressSliderEvent || !IsLoaded) return;

            _ = Task.Run(() => ServiceCommunicator.TrySetRotationDegrees(value));
        }

        private void RotationPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && double.TryParse(b.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double deg))
            {
                RotationSlider.Value = deg;
            }
        }

        private void PhysicalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PhysicalValueLabel == null) return;
            double value = Math.Round(e.NewValue);
            CurrentPhysical = value;
            PhysicalValueLabel.Content = $"{value:0}°";
            if (SuppressSliderEvent || !IsLoaded) return;

            _ = Task.Run(() => ServiceCommunicator.TrySetPhysicalDegrees(value));
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            SensitivitySlider.Value = 1.0;
        }
    }
}
