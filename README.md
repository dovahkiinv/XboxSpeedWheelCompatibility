# Xbox Wheel Compatibility

Convert a compatible racing wheel into virtual Xbox controller input on Windows, with adjustable steering sensitivity and a live input tester.

This project builds on [Camren Mumme's XboxWheelCompatibility](https://github.com/camren-m/XboxWheelCompatibility). The original MIT license and Git history are preserved.

## Features

- Wheel steering maps to the left stick; throttle and brake map to the right and left triggers.
- D-pad, gear paddles, and six wheel buttons map to Xbox controller buttons.
- Adjustable steering sensitivity from 0.01 to 3.00 and a steering dead zone, saved across service restarts.
- Side-by-side rotating wheel displays compare raw input and adjusted output.
- Live pedal bars, button indicators, connection status, and injection diagnostics.
- .NET 8 service and WPF configurator, with service-offline handling and reduced polling overhead.
- **Xbox 360 Wireless Speed Wheel support** (via the Xbox 360 Wireless Receiver for Windows), with automatic device selection: a real RacingWheel is used first, otherwise the Speed Wheel.
- Raw-axis diagnostics, selectable steering axis, invert option, and a diagnostic log file for checking your hardware.

## Requirements

- Windows 10 22H2 or Windows 11, x64.
- The installer includes the .NET runtime. Developers need the .NET 8 SDK.
- A wheel recognized by Windows.Gaming.Input.RacingWheel. The original project lists the Thrustmaster Ferrari 458 Spider Racing Wheel as tested.
- Xbox Accessory Management Service (XboxGipSvc) available and enabled.

Compatibility depends on the wheel's drivers and the game. Clutch and handbrake are displayed in the tester but are not mapped to gamepad output.

## Xbox 360 Wireless Speed Wheel

Windows does not expose the Speed Wheel as `Windows.Gaming.Input.RacingWheel`; it shows up as
`Controller (Xbox 360 Wireless Receiver for Windows)`. The service now finds devices in this order
(**Device mode = Auto**):

1. a real wheel from `RacingWheel.RacingWheels` (original behaviour, unchanged),
2. an XInput device whose sub type is *Wheel* (`XINPUT_DEVSUBTYPE_WHEEL`) – the Speed Wheel,
3. a wireless / Xbox 360 receiver controller from `Gamepad.Gamepads` (fallback). Wired and virtual
   controllers are ignored so the virtual controller created by this app is never read back.

Speed Wheel mapping sent to the virtual Xbox controller:

| Speed Wheel | Virtual controller |
| --- | --- |
| Steering (LeftThumbstickX by default, selectable) | Left stick X (with sensitivity curve) |
| Right trigger | Right trigger (throttle) |
| Left trigger | Left trigger (brake) |
| LB / RB | LB / RB (gear down / up) |
| A / B / X / Y, D-pad, Start, Back | same buttons (Start = Menu, Back = View) |

XInput is used instead of DirectInput/Raw Input: the joy.cpl axes *Z axis / X rotation / Y rotation*
are just the legacy DirectInput view of the same XInput data (Z = both triggers combined).

### Checking the axes on your hardware

The Speed Wheel support has **not been tested on physical hardware by the author of this change**.
Use the built-in diagnostics:

1. Open the configurator. The header shows e.g. `Speed Wheel connected: Xbox 360 Wireless Speed Wheel (XInput slot 0)`.
2. In **Diagnostics (raw axes)** turn the wheel fully left and right and watch `LX / LY / RX / RY`.
3. *Steering axis = Auto* uses LeftThumbstickX; if LX never moves but another axis does, Auto switches to it.
   You can also pick the axis manually, and tick **Invert steering** if left/right are swapped.
4. *Detected devices* lists every RacingWheel, XInput slot (with sub type) and Gamepad Windows reports.
5. The service writes `Output.log` next to `WheelCompatibilityService.exe` (fresh on every start).
6. Everything is also written to `%ProgramData%\XboxWheelCompatibility\diagnostics.log`
   (device changes and axis values every 2 s when they change). Attach this file when reporting problems.

### F1 25 notes

- The game also sees the original Speed Wheel as a normal Xbox controller, so it may receive input from
  both the real and the virtual controller. In the game choose the virtual controller's profile, or
  unbind the duplicated controls, if input looks doubled.
- The virtual controller is a gamepad, so use the game's gamepad presets and adjust steering
  linearity/saturation there if needed.

## Build

From PowerShell in this folder:

```powershell
# Compile the service and configurator
.\build.ps1

# Create runnable Windows x64 builds
.\build.ps1 -Publish
```

Requirements for building: Windows 10/11 x64 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
(Visual Studio 2022 17.8+ also works – open `XboxWheelCompatibility.sln`). Manual equivalent:

```powershell
dotnet publish WheelCompatibilityService/WheelCompatibilityService.csproj -c Release -r win-x64 --self-contained false -o publish/Service
dotnet publish WheelCompatibilityConfigurator/WheelCompatibilityConfigurator.csproj -c Release -r win-x64 --self-contained false -o publish/Configurator
```

To install a self-built service (Administrator PowerShell):

```powershell
sc.exe create WheelCompatibilityService binPath= "$PWD\publish\Service\WheelCompatibilityService.exe" start= auto
sc.exe start WheelCompatibilityService
```

To test without installing, run `publish\Service\WheelCompatibilityService.exe` from an Administrator terminal
and then start `publish\Configurator\WheelCompatibilityConfigurator.exe`. If an older version is installed,
stop it first (`sc.exe stop WheelCompatibilityService`) – both use TCP port 16581, and the service and
configurator must come from the same build.

Published files appear in `publish/Service` and `publish/Configurator`. These builds require the .NET 8 Desktop Runtime; keep each output folder's files together.

The GitHub Actions workflow builds both apps and uploads the published folders as a downloadable artifact. The legacy WiX installer projects remain in the solution for reference; the build script deliberately builds the application projects directly because the installers still reference .NET 6 prerequisites.

## Install and run

1. Download the `.msi` installer from the [latest release](https://github.com/AbhiPoluri/XboxWheelCompatibility/releases/latest).
2. Double-click it, follow the setup wizard, and approve the Windows administrator prompt.
3. Open **Xbox Wheel Compatibility** from the Start menu and connect your wheel.

Setup includes the .NET runtime, registers and starts the wheel service, and creates a Start menu shortcut. No terminal commands or separate runtime installation are needed. Future MSI versions upgrade the installed application. Close the configurator before upgrading.

The installer uses Windows Installer's service management and rollback support. Existing settings are preserved. The installer is currently unsigned.

To build the installer from source, run `./build-installer.ps1` in PowerShell. This installs a pinned WiX build tool under the ignored `artifacts` directory and writes the MSI there.

## Steering sensitivity and dead zone

- Steering range (`20 %`–`100 %`, default `100 %`): how much of the wheel's physical travel gives full lock. At `60 %` the game gets full lock when the wheel is turned 60 % of the way. The Speed Wheel has no fixed rotation angle (it is a motion-sensing wheel), so this is the equivalent of a "maximum rotation" setting.
- Dead zone (`0.00`–`0.50`, default `0.05`): steering inside it is sent as 0, the rest is rescaled so full lock is still 100 %. The Speed Wheel drifts about ±0.07 at rest, so `0.08` is a good start.

- `1.00`: linear steering.
- Below `1.00`: gentler steering near the center.
- Above `1.00`: stronger steering near the center.

The output curve is `sign(input) * abs(input)^(1 / sensitivity)`. Changes are sent to the running service and saved in `%ProgramData%\XboxWheelCompatibility\settings.json`. Wheel display rotation is a visualization, not hardware steering-angle calibration.

## Troubleshooting

**Service unreachable:** Check that WheelCompatibilityService is running and that the service and configurator were published from the same version. Communication uses TCP port 16581 on the local machine.

**Wheel connected but no controller input:** Check the injection status. If error `0x80070422` appears, open Windows **Services**, find **Xbox Accessory Management Service**, set its startup type to **Manual**, and start it. Then restart **Wheel Compatibility Service** using the same window.

**No wheel connected:** Check USB connectivity and driver support. The app sees wheels exposed through Windows.Gaming.Input.RacingWheel, XInput devices with the *Wheel* sub type (Xbox 360 Speed Wheel), and wireless Xbox 360 controllers. Check *Detected devices* and *Device mode* in the configurator.

**Speed Wheel steers the wrong way / not at all:** Change *Steering axis* or tick *Invert steering*, and check `diagnostics.log`.

## Uninstall

Open **Windows Settings > Apps > Installed apps**, select **Xbox Wheel Compatibility**, and choose **Uninstall**. Setup removes the service, application files, and shortcut. Settings remain in `%ProgramData%\XboxWheelCompatibility`.

## Project layout

| Folder | Purpose |
| --- | --- |
| WheelTransformer | Device detection (RacingWheel / XInput Speed Wheel / Gamepad), sensitivity curve, gamepad injection, diagnostics log |
| WheelCompatibilityService | Windows service and configuration endpoint |
| WheelCompatibilityConfigurator | Desktop controls and live tester |
| CommunicationEnums | Shared service contracts and diagnostic snapshots |
| WheelCompatibilityInstaller / WheelCompatibilitySetup | Legacy WiX installer sources |

## License

[MIT](LICENSE). Original copyright: Camren Mumme.
