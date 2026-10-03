using System;
using System.Diagnostics;
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

            for (int i = 1; i <= 16; i++) VJoyDeviceCombo.Items.Add(i.ToString(CultureInfo.InvariantCulture));

            ButtonIdleBrush.Freeze();
            ButtonActiveBrush.Freeze();

            Closed += (_, _) => StatusLoopCancellation.Cancel();
            Loaded += (_, _) => { UpdateAntiDeadzoneWarning(); UpdateAntiDeadzoneFloor(); };

            _ = InitializeSensitivityAsync();
            _ = StatusLoop(StatusLoopCancellation.Token);
            _ = TesterLoop(StatusLoopCancellation.Token);
            _ = DiagnosticsLoop(StatusLoopCancellation.Token);
            _ = DeviceStatusLoop(StatusLoopCancellation.Token);
            _ = PedalsLoop(StatusLoopCancellation.Token);
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
                        HideRealDeviceCheck.IsChecked = status.HideRealDevice;
                        HideXInputCheck.IsChecked = status.HideXInputInterface;
                        VJoyEnabledCheck.IsChecked = status.VJoyEnabled;
                        VJoyDeviceCombo.SelectedIndex = Math.Clamp(status.VJoyDeviceId, 1, 16) - 1;
                        VJoyPedalModeCombo.SelectedIndex = Math.Clamp(status.VJoyPedalAxisMode, 0, 1);
                        VJoyInvertThrottleCheck.IsChecked = status.VJoyInvertThrottle;
                        VJoyInvertBrakeCheck.IsChecked = status.VJoyInvertBrake;
                        SteeringAntiDeadzoneSlider.Value = Math.Round(Math.Clamp(status.SteeringAntiDeadzone, 0.0, 0.40) * 100.0);
                        SteeringAntiDeadzoneLabel.Content = $"{SteeringAntiDeadzoneSlider.Value:0}%";
                        UpdateAntiDeadzoneFloor();
                        UpdateAntiDeadzoneWarning();
                        SuppressDeviceEvents = false;
                        DeviceSettingsLoaded = true;
                    }

                    VJoyAxisWarningBorder.Visibility = status.VJoyAxisWarning ? Visibility.Visible : Visibility.Collapsed;
                    VJoyAxisWarningText.Text = status.VJoyAxisMessage;

                    ScanText.Text = status.ScanLines.Length == 0
                        ? "No RacingWheel, XInput or Gamepad devices found."
                        : string.Join(Environment.NewLine, status.ScanLines);
                    LogPathText.Text = "Log file: " + status.LogPath;
                    HidHideStatusText.Text = status.HidHideStatus;
                    VJoyStatusText.Text = status.VJoyEnabled ? status.VJoyStatus : "Off";
                    VJoyRotationText.Text = $"Current: {status.RotationDegrees:0}° (full lock at {status.RotationDegrees / 2:0}° to each side)";
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

        /// <summary>Opens the Windows "Game Controllers" panel so the wheel, pedals and vJoy can be tested.</summary>
        private void JoyCplButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("joy.cpl") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not open joy.cpl: " + ex.Message + Environment.NewLine +
                    "Open it manually with Win+R and typing: joy.cpl",
                    "Xbox Wheel Compatibility", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SteeringAntiDeadzoneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SteeringAntiDeadzoneLabel == null) return;
            double percent = Math.Round(e.NewValue);
            SteeringAntiDeadzoneLabel.Content = $"{percent:0}%";
            UpdateAntiDeadzoneWarning();
            UpdateAntiDeadzoneFloor();
            if (SuppressDeviceEvents || !DeviceSettingsLoaded) return;

            double value = Math.Round(percent / 100.0, 2);
            _ = Task.Run(() => ServiceCommunicator.TrySetSteeringAntiDeadzone(value));
        }

        /// <summary>
        /// Shows the smallest steering value the game can receive while the anti-deadzone is on:
        /// as soon as the input leaves the dead zone the game gets at least the anti-deadzone value.
        /// It has to be larger than the game's own dead zone or the game still shows nothing.
        /// </summary>
        private void UpdateAntiDeadzoneFloor()
        {
            if (AntiDeadzoneFloorText == null) return;

            double percent = Math.Round(SteeringAntiDeadzoneSlider.Value);
            AntiDeadzoneFloorText.Text = percent <= 0
                ? "Game output: 0% at rest, exactly what the wheel does up to 100%."
                : $"Game output: as soon as the wheel leaves the dead zone the game receives at least {percent:0}% "
                  + $"of steering (0 to {percent:0}% is never sent). This floor must be larger than the game's own "
                  + "dead zone - F1 25 ignores about 20%, so use 20-25% there.";
        }

        /// <summary>
        /// Warns when the anti-deadzone is on but the steering dead zone is (almost) zero: the Speed
        /// Wheel drifts a little at rest and the anti-deadzone would turn that drift into real steering.
        /// </summary>
        private void UpdateAntiDeadzoneWarning()
        {
            if (AntiDeadzoneWarningBorder == null) return;

            bool risky = DeadZoneSlider.Value < 0.02 && SteeringAntiDeadzoneSlider.Value >= 1.0;
            AntiDeadzoneWarningBorder.Visibility = risky ? Visibility.Visible : Visibility.Collapsed;
            if (!risky) return;

            AntiDeadzoneWarningText.Text =
                $"Careful: steering dead zone is {DeadZoneSlider.Value:0.00} while the anti-deadzone is "
                + $"{SteeringAntiDeadzoneSlider.Value:0}%. The Speed Wheel drifts about ±0.07 at rest and the "
                + "anti-deadzone multiplies that drift into a large steering value (the car pulls to one side). "
                + "Set 'Steering dead zone' to about 0.06-0.08.";
        }

        private void VJoyPedalModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SuppressDeviceEvents || !DeviceSettingsLoaded || VJoyPedalModeCombo.SelectedIndex < 0) return;
            int mode = Math.Clamp(VJoyPedalModeCombo.SelectedIndex, 0, 1);
            _ = Task.Run(() => ServiceCommunicator.TrySetVJoyPedalAxisMode(mode));
        }

        private void VJoyAxisInvert_Changed(object sender, RoutedEventArgs e)
        {
            if (SuppressDeviceEvents || !DeviceSettingsLoaded) return;
            bool invertThrottle = VJoyInvertThrottleCheck.IsChecked == true;
            bool invertBrake = VJoyInvertBrakeCheck.IsChecked == true;
            _ = Task.Run(() => ServiceCommunicator.TrySetVJoyAxisInvert(invertThrottle, invertBrake));
        }

        private async void HideRealDeviceCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (SuppressDeviceEvents) return;
            bool hide = HideRealDeviceCheck.IsChecked == true;
            HideRealDeviceCheck.IsEnabled = false;
            HidHideStatusText.Text = hide ? "Hiding real receiver..." : "Unhiding real receiver...";
            string? result = await Task.Run(() => ServiceCommunicator.TrySetHideRealDevice(hide));
            HidHideStatusText.Text = result ?? "Service unreachable";
            HideRealDeviceCheck.IsEnabled = true;
        }

        // ---------------- Pedals tab ----------------
        private bool PedalsLoaded = false;
        private bool SuppressPedalsEvents = false;
        private string PedalsDeviceListSignature = "";

        private async Task PedalsLoop(CancellationToken Cancellation)
        {
            while (!Cancellation.IsCancellationRequested)
            {
                PedalsStatus? st = await Task.Run(() => ServiceCommunicator.TryGetPedalsStatus());
                if (Cancellation.IsCancellationRequested) return;
                if (st != null) UpdatePedalsUI(st);
                else PedalsStatusText.Text = "Service unreachable";

                try { await Task.Delay(100, Cancellation); }
                catch (TaskCanceledException) { return; }
            }
        }

        private void UpdatePedalsUI(PedalsStatus st)
        {
            SuppressPedalsEvents = true;
            try
            {
                string sig = string.Join(";", st.Devices);
                if (sig != PedalsDeviceListSignature || !PedalsLoaded)
                {
                    PedalsDeviceListSignature = sig;
                    PedalsDeviceCombo.Items.Clear();
                    PedalsDeviceCombo.Items.Add(new ComboBoxItem { Content = "Auto (first non-Xbox joystick)", Tag = "" });
                    foreach (var d in st.Devices)
                    {
                        int bar = d.IndexOf('|');
                        string key = bar > 0 ? d.Substring(0, bar) : d;
                        string text = bar > 0 ? d.Substring(bar + 1) : d;
                        PedalsDeviceCombo.Items.Add(new ComboBoxItem { Content = text, Tag = key });
                    }
                    int sel = 0;
                    for (int i = 0; i < PedalsDeviceCombo.Items.Count; i++)
                    {
                        if (PedalsDeviceCombo.Items[i] is ComboBoxItem it && string.Equals(it.Tag as string, st.DeviceKey, StringComparison.OrdinalIgnoreCase)) sel = i;
                    }
                    PedalsDeviceCombo.SelectedIndex = sel;
                }

                if (!PedalsLoaded)
                {
                    PedalsEnabledCheck.IsChecked = st.Enabled;
                    PedalsAxisCombo.SelectedIndex = Math.Clamp(st.Axis, 0, 5);
                    PedalsSwapCheck.IsChecked = st.Swap;
                    PedalsDeadZoneSlider.Value = st.DeadZone;
                    PedalsDeadZoneLabel.Text = st.DeadZone.ToString("0.00", CultureInfo.InvariantCulture);
                    ThrottleRangeSlider.Value = st.ThrottleRange;
                    BrakeRangeSlider.Value = st.BrakeRange;
                    ThrottleRangeLabel.Text = $"{st.ThrottleRange * 100:0}%";
                    BrakeRangeLabel.Text = $"{st.BrakeRange * 100:0}%";
                    PedalsSourceCombo.SelectedIndex = Math.Clamp(st.Source, 0, 2);
                    ThrottleAntiDeadzoneSlider.Value = Math.Round(Math.Clamp(st.ThrottleAntiDeadzone, 0.0, 0.40) * 100.0);
                    BrakeAntiDeadzoneSlider.Value = Math.Round(Math.Clamp(st.BrakeAntiDeadzone, 0.0, 0.40) * 100.0);
                    ThrottleAntiDeadzoneLabel.Text = $"{ThrottleAntiDeadzoneSlider.Value:0}%";
                    BrakeAntiDeadzoneLabel.Text = $"{BrakeAntiDeadzoneSlider.Value:0}%";
                    PedalsLoaded = true;
                }
            }
            finally
            {
                SuppressPedalsEvents = false;
            }

            PedalsCenterText.Text = "Center: " + st.Center.ToString("0.000", CultureInfo.InvariantCulture);
            PedalThrottleBar.Value = Math.Clamp(st.Throttle, 0, 1);
            PedalBrakeBar.Value = Math.Clamp(st.Brake, 0, 1);
            PedalThrottleText.Text = ((int)Math.Round(st.Throttle * 100)).ToString() + "%";
            PedalBrakeText.Text = ((int)Math.Round(st.Brake * 100)).ToString() + "%";

            var r = st.RawAxes.Length >= 6 ? st.RawAxes : new double[6];
            PedalsRawText.Text = string.Format(CultureInfo.InvariantCulture,
                "X={0:0.000}  Y={1:0.000}  Z={2:0.000}\nR={3:0.000}  U={4:0.000}  V={5:0.000}", r[0], r[1], r[2], r[3], r[4], r[5]);

            PedalsStatusText.Text = !st.Connected
                ? "No pedal device found. Check joy.cpl (the pedals must be visible there and not hidden by HidHide)."
                : (st.Enabled ? "Active: " : "Found (not enabled): ") + st.ActiveDevice;
        }

        private void SendPedalsSettings()
        {
            if (SuppressPedalsEvents || !PedalsLoaded) return;
            bool enabled = PedalsEnabledCheck.IsChecked == true;
            string key = (PedalsDeviceCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            int axis = PedalsAxisCombo.SelectedIndex < 0 ? 1 : PedalsAxisCombo.SelectedIndex;
            bool swap = PedalsSwapCheck.IsChecked == true;
            double dz = Math.Round(PedalsDeadZoneSlider.Value, 2);
            _ = Task.Run(() => ServiceCommunicator.TrySetPedals(enabled, key, axis, swap, dz));
        }

        private void PedalsSettings_Changed(object sender, RoutedEventArgs e) => SendPedalsSettings();

        private void PedalsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => SendPedalsSettings();

        private void PedalsSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SuppressPedalsEvents || !PedalsLoaded || PedalsSourceCombo.SelectedIndex < 0) return;
            int source = Math.Clamp(PedalsSourceCombo.SelectedIndex, 0, 2);
            _ = Task.Run(() => ServiceCommunicator.TrySetPedalsSource(source));
        }

        private void ThrottleAntiDeadzoneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ThrottleAntiDeadzoneLabel == null) return;
            double percent = Math.Round(e.NewValue);
            ThrottleAntiDeadzoneLabel.Text = $"{percent:0}%";
            if (SuppressPedalsEvents || !PedalsLoaded) return;

            double value = Math.Round(percent / 100.0, 2);
            _ = Task.Run(() => ServiceCommunicator.TrySetThrottleAntiDeadzone(value));
        }

        private void BrakeAntiDeadzoneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BrakeAntiDeadzoneLabel == null) return;
            double percent = Math.Round(e.NewValue);
            BrakeAntiDeadzoneLabel.Text = $"{percent:0}%";
            if (SuppressPedalsEvents || !PedalsLoaded) return;

            double value = Math.Round(percent / 100.0, 2);
            _ = Task.Run(() => ServiceCommunicator.TrySetBrakeAntiDeadzone(value));
        }

        private void PedalsDeadZoneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PedalsDeadZoneLabel == null) return;
            PedalsDeadZoneLabel.Text = Math.Round(e.NewValue, 2).ToString("0.00", CultureInfo.InvariantCulture);
            SendPedalsSettings();
        }

        private void PedalRangeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ThrottleRangeLabel == null || BrakeRangeLabel == null || ThrottleRangeSlider == null || BrakeRangeSlider == null) return;
            ThrottleRangeLabel.Text = $"{ThrottleRangeSlider.Value * 100:0}%";
            BrakeRangeLabel.Text = $"{BrakeRangeSlider.Value * 100:0}%";
            if (SuppressPedalsEvents || !PedalsLoaded) return;
            double t = Math.Round(ThrottleRangeSlider.Value, 2), b = Math.Round(BrakeRangeSlider.Value, 2);
            _ = Task.Run(() => ServiceCommunicator.TrySetPedalsRange(t, b));
        }

        private void PedalRangePreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && double.TryParse(btn.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                ThrottleRangeSlider.Value = v;
                BrakeRangeSlider.Value = v;
            }
        }

        private async void PedalsCalibrateButton_Click(object sender, RoutedEventArgs e)
        {
            string? msg = await Task.Run(() => ServiceCommunicator.TryCalibratePedalsCenter());
            PedalsStatusText.Text = msg ?? "Service unreachable";
        }

        private void VJoySettings_Changed(object sender, RoutedEventArgs e) => SendVJoySettings();

        private void VJoyDeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => SendVJoySettings();

        private void SendVJoySettings()
        {
            if (SuppressDeviceEvents || !DeviceSettingsLoaded) return;
            bool enabled = VJoyEnabledCheck.IsChecked == true;
            int id = VJoyDeviceCombo.SelectedIndex < 0 ? 1 : VJoyDeviceCombo.SelectedIndex + 1;
            VJoyStatusText.Text = enabled ? "Connecting to vJoy..." : "Off";
            _ = Task.Run(() => ServiceCommunicator.TrySetVJoy(enabled, id));
        }

        private async void HideXInputCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (SuppressDeviceEvents || !DeviceSettingsLoaded) return;
            bool hide = HideXInputCheck.IsChecked == true;
            HideXInputCheck.IsEnabled = false;
            HidHideStatusText.Text = "Re-applying hiding...";
            string? result = await Task.Run(() => ServiceCommunicator.TrySetHideXInputInterface(hide));
            HidHideStatusText.Text = result ?? "Service unreachable";
            HideXInputCheck.IsEnabled = true;
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
                OutputModeCombo.SelectedIndex = Math.Clamp(diag.OutputMode, 0, 4);
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
            double triggerThrottle = active ? snapshot!.TriggerThrottle : 0;
            double triggerBrake = active ? snapshot!.TriggerBrake : 0;
            double pedalThrottle = active ? snapshot!.PedalThrottle : 0;
            double pedalBrake = active ? snapshot!.PedalBrake : 0;
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
                    snapshot.Wheel, snapshot.TriggerThrottle, snapshot.TriggerBrake, snapshot.Buttons);
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

            // Final values sent to the game (after source selection and anti-deadzone).
            ThrottleBar.Value = Math.Clamp(throttle, 0, 1);
            ThrottleValueLabel.Text = ((int)Math.Round(throttle * 100)).ToString() + "%";
            BrakeBar.Value = Math.Clamp(brake, 0, 1);
            BrakeValueLabel.Text = ((int)Math.Round(brake * 100)).ToString() + "%";

            // Raw sources: wheel/device triggers and the separate pedal set.
            ThrottleTriggerBar.Value = Math.Clamp(triggerThrottle, 0, 1);
            ThrottleTriggerValueLabel.Text = ((int)Math.Round(triggerThrottle * 100)).ToString() + "%";
            ThrottlePedalBar.Value = Math.Clamp(pedalThrottle, 0, 1);
            ThrottlePedalValueLabel.Text = ((int)Math.Round(pedalThrottle * 100)).ToString() + "%";
            BrakeTriggerBar.Value = Math.Clamp(triggerBrake, 0, 1);
            BrakeTriggerValueLabel.Text = ((int)Math.Round(triggerBrake * 100)).ToString() + "%";
            BrakePedalBar.Value = Math.Clamp(pedalBrake, 0, 1);
            BrakePedalValueLabel.Text = ((int)Math.Round(pedalBrake * 100)).ToString() + "%";

            int source = active ? snapshot!.PedalSource : 2;
            string pedalState = !active ? "no device" : (snapshot!.PedalsConnected ? "pedals connected" : "pedals not connected");
            PedalSourceHint.Text = $"Source: {PedalSourceLabel(source)} ({pedalState}). Bold bars = final values sent to the game "
                + "(after anti-deadzone); the thin bars show the raw triggers and pedals. Change the source on the Pedals tab.";

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

        private static string PedalSourceLabel(int source) => source switch
        {
            0 => "Wheel triggers",
            1 => "Separate pedals",
            _ => "Both (max)",
        };

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
            UpdateAntiDeadzoneWarning();
            UpdateAntiDeadzoneFloor();
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
