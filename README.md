# Xbox Wheel Compatibility

Convert a compatible racing wheel into virtual Xbox controller input on Windows, with adjustable steering sensitivity and a live input tester.

This project builds on [Camren Mumme's XboxWheelCompatibility](https://github.com/camren-m/XboxWheelCompatibility). The original MIT license and Git history are preserved.

## Features

- Wheel steering maps to the left stick; throttle and brake map to the right and left triggers.
- D-pad, gear paddles, and six wheel buttons map to Xbox controller buttons.
- Adjustable steering sensitivity from 0.10 to 3.00, saved across service restarts.
- Side-by-side rotating wheel displays compare raw input and adjusted output.
- Live pedal bars, button indicators, connection status, and injection diagnostics.
- .NET 8 service and WPF configurator, with service-offline handling and reduced polling overhead.

## Requirements

- Windows 10 22H2 or Windows 11, x64.
- The installer includes the .NET runtime. Developers need the .NET 8 SDK.
- A wheel recognized by Windows.Gaming.Input.RacingWheel. The original project lists the Thrustmaster Ferrari 458 Spider Racing Wheel as tested.
- Xbox Accessory Management Service (XboxGipSvc) available and enabled.

Compatibility depends on the wheel's drivers and the game. Clutch and handbrake are displayed in the tester but are not mapped to gamepad output.

## Build

From PowerShell in this folder:

```powershell
# Compile the service and configurator
.\build.ps1

# Create runnable Windows x64 builds
.\build.ps1 -Publish
```

Published files appear in `publish/Service` and `publish/Configurator`. These builds require the .NET 8 Desktop Runtime; keep each output folder's files together.

The GitHub Actions workflow builds both apps and uploads the published folders as a downloadable artifact. The legacy WiX installer projects remain in the solution for reference; the build script deliberately builds the application projects directly because the installers still reference .NET 6 prerequisites.

## Install and run

1. Download the `.msi` installer from the [latest release](https://github.com/AbhiPoluri/XboxWheelCompatibility/releases/latest).
2. Double-click it, follow the setup wizard, and approve the Windows administrator prompt.
3. Open **Xbox Wheel Compatibility** from the Start menu and connect your wheel.

Setup includes the .NET runtime, registers and starts the wheel service, and creates a Start menu shortcut. No terminal commands or separate runtime installation are needed. Future MSI versions upgrade the installed application. Close the configurator before upgrading.

The installer uses Windows Installer's service management and rollback support. Existing settings are preserved. The installer is currently unsigned.

To build the installer from source, run `./build-installer.ps1` in PowerShell. This installs a pinned WiX build tool under the ignored `artifacts` directory and writes the MSI there.

## Steering sensitivity

- `1.00`: linear steering.
- Below `1.00`: gentler steering near the center.
- Above `1.00`: stronger steering near the center.

The output curve is `sign(input) * abs(input)^(1 / sensitivity)`. Changes are sent to the running service and saved in `%ProgramData%\XboxWheelCompatibility\settings.json`. Wheel display rotation is a visualization, not hardware steering-angle calibration.

## Troubleshooting

**Service unreachable:** Check that WheelCompatibilityService is running and that the service and configurator were published from the same version. Communication uses TCP port 16581 on the local machine.

**Wheel connected but no controller input:** Check the injection status. If error `0x80070422` appears, open Windows **Services**, find **Xbox Accessory Management Service**, set its startup type to **Manual**, and start it. Then restart **Wheel Compatibility Service** using the same window.

**No wheel connected:** Check USB connectivity and driver support. The app only sees wheels exposed through Windows.Gaming.Input.RacingWheel.

## Uninstall

Open **Windows Settings > Apps > Installed apps**, select **Xbox Wheel Compatibility**, and choose **Uninstall**. Setup removes the service, application files, and shortcut. Settings remain in `%ProgramData%\XboxWheelCompatibility`.

## Project layout

| Folder | Purpose |
| --- | --- |
| WheelTransformer | Wheel input, sensitivity curve, gamepad injection |
| WheelCompatibilityService | Windows service and configuration endpoint |
| WheelCompatibilityConfigurator | Desktop controls and live tester |
| CommunicationEnums | Shared service contracts and diagnostic snapshots |
| WheelCompatibilityInstaller / WheelCompatibilitySetup | Legacy WiX installer sources |

## License

[MIT](LICENSE). Original copyright: Camren Mumme.
