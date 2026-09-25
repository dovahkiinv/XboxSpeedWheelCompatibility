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
            InjectionStatusText.Text = $"Injection OK: {diag.InjectSuccesses}/{diag.InjectAttempts} succeeded.";
            InjectionStatusBorder.Visibility = Visibility.Visible;
        }

        private async Task InitializeSensitivityAsync()
        {
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

                if (Cancellation.IsCancellationRequested) return;

                if (!index.HasValue)
                {
                    ServiceStatusIndicator.Content = "Service unreachable";
                }
                else if (index.Value == -1)
                {
                    ServiceStatusIndicator.Content = "No wheel connected";
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
            RacingWheelButtons buttons = active ? (RacingWheelButtons)snapshot!.Buttons : RacingWheelButtons.None;

            // Steering wheel icons: rotate based on raw and adjusted values.
            // -1..1 is mapped to ±450° so a fully-locked wheel makes 1.25 rotations on screen.
            const double MaxAngleDegrees = 450.0;
            WheelInputRotation.Angle = Math.Clamp(wheel, -1.0, 1.0) * MaxAngleDegrees;
            WheelOutputRotation.Angle = Math.Clamp(wheelAdjusted, -1.0, 1.0) * MaxAngleDegrees;

            WheelValueLabel.Text = wheel.ToString("+0.00;-0.00; 0.00", CultureInfo.InvariantCulture);
            WheelAdjustedLabel.Text = wheelAdjusted.ToString("+0.00;-0.00; 0.00", CultureInfo.InvariantCulture);

            ThrottleBar.Value = Math.Clamp(throttle, 0, 1);
            ThrottleValueLabel.Text = ((int)Math.Round(throttle * 100)).ToString() + "%";

            BrakeBar.Value = Math.Clamp(brake, 0, 1);
            BrakeValueLabel.Text = ((int)Math.Round(brake * 100)).ToString() + "%";

            ClutchBar.Value = Math.Clamp(clutch, 0, 1);
            ClutchValueLabel.Text = ((int)Math.Round(clutch * 100)).ToString() + "%";

            HandbrakeBar.Value = Math.Clamp(handbrake, 0, 1);
            HandbrakeValueLabel.Text = ((int)Math.Round(handbrake * 100)).ToString() + "%";

            SetButton(DotDPadUp, buttons.HasFlag(RacingWheelButtons.DPadUp));
            SetButton(DotDPadDown, buttons.HasFlag(RacingWheelButtons.DPadDown));
            SetButton(DotDPadLeft, buttons.HasFlag(RacingWheelButtons.DPadLeft));
            SetButton(DotDPadRight, buttons.HasFlag(RacingWheelButtons.DPadRight));
            SetButton(DotPrevGear, buttons.HasFlag(RacingWheelButtons.PreviousGear));
            SetButton(DotNextGear, buttons.HasFlag(RacingWheelButtons.NextGear));
            SetButton(DotB1, buttons.HasFlag(RacingWheelButtons.Button1));
            SetButton(DotB2, buttons.HasFlag(RacingWheelButtons.Button2));
            SetButton(DotB3, buttons.HasFlag(RacingWheelButtons.Button3));
            SetButton(DotB4, buttons.HasFlag(RacingWheelButtons.Button4));
            SetButton(DotB5, buttons.HasFlag(RacingWheelButtons.Button5));
            SetButton(DotB6, buttons.HasFlag(RacingWheelButtons.Button6));
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

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            SensitivitySlider.Value = 1.0;
        }
    }
}
