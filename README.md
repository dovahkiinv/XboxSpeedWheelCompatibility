# Xbox Wheel Compatibility

**English** | [Polski](README.pl.md)

Turn a racing wheel – including the **Microsoft Xbox 360 Wireless Speed Wheel** – into a virtual Xbox
controller or a virtual DirectInput wheel on Windows, with rotation angle, dead zone, sensitivity,
separate pedals support and live diagnostics.

This project builds on [Camren Mumme's XboxWheelCompatibility](https://github.com/camren-m/XboxWheelCompatibility).
The original MIT license and Git history are preserved.

## Features

- **Input devices** (automatic selection):
  1. real wheels exposed by Windows as `RacingWheel` (original behaviour),
  2. **Xbox 360 Wireless Speed Wheel** via XInput (Xbox 360 Wireless Receiver for Windows),
  3. wireless Xbox 360 controllers as a fallback.
- **Virtual outputs**:
  - **ViGEm** virtual Xbox 360 controller – seen by XInput, DirectInput and modern games,
  - **InputInjector** (built into Windows) – only modern Windows.Gaming.Input / GameInput games,
  - **vJoy** virtual DirectInput wheel – X = steering, Y = throttle, Z = brake, 14 buttons.
- **Steering**: rotation angle 90°–1080° (like a real wheel), physical-turn calibration, dead zone,
  sensitivity curve 0.01–3.00, invert, selectable steering axis.
- **Anti-deadzone** (0–40 %) for steering, throttle and brake – lifts the signal above a game's own
  dead zone (needed for F1 25, which ignores about 20 % of the axis).
- **Separate pedals** (any extra DirectInput joystick, e.g. an old wheel's pedal unit): combined
  single-axis pedals, dead zone, "100 % at X % travel" per pedal, center calibration, swap, and a
  selectable throttle/brake source (wheel triggers / separate pedals / both).
- **vJoy pedal layout**: separate axes (Y = throttle, Z = brake) or one centered axis
  (Y: up = throttle, down = brake), with per-pedal inversion.
- **Hide the real wheel from games** with HidHide, so games only see the virtual device.
- **Diagnostics**: live wheel/pedal/button tester, raw axes, detected devices list, `Output.log`.
- Settings are saved in `%ProgramData%\XboxWheelCompatibility\settings.json`.

## Requirements

| Component | Needed for |
| --- | --- |
| Windows 10 22H2 / Windows 11, x64 | always |
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or newer SDK) | building |
| [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) | running published builds |
| Xbox 360 Wireless Receiver for Windows | Speed Wheel |
| [ViGEmBus](https://github.com/nefarius/ViGEmBus/releases) | ViGEm output (recommended) |
| [HidHide](https://github.com/nefarius/HidHide/releases) | hiding the real wheel (optional) |
| [vJoy](https://github.com/BrunnerInnovation/vJoy/releases) | virtual wheel output (optional) |

Reboot after installing ViGEmBus, HidHide or vJoy.

## Build

In PowerShell, in the repository folder:

```powershell
git clone https://github.com/dovahkiinv/XboxSpeedWheelCompatibility.git
cd XboxSpeedWheelCompatibility
.\build.ps1 -Publish
```

Output: `publish\Service` and `publish\Configurator`. Close the running service and configurator
before building – otherwise Windows locks the `.exe` (the script warns about this).

Manual equivalent:

```powershell
dotnet publish WheelCompatibilityService/WheelCompatibilityService.csproj -c Release -r win-x64 --self-contained false -o publish/Service
dotnet publish WheelCompatibilityConfigurator/WheelCompatibilityConfigurator.csproj -c Release -r win-x64 --self-contained false -o publish/Configurator
```

## Run

1. If an older version is installed as a Windows service, stop it (both use TCP port 16581):
   ```powershell
   Get-Service *Wheel* | Stop-Service -Force
   Get-Service *Wheel* | Set-Service -StartupType Manual
   ```
2. Start the service from an **Administrator** PowerShell and keep the window open:
   ```powershell
   .\publish\Service\WheelCompatibilityService.exe
   ```
3. Start the configurator in a second window:
   ```powershell
   .\publish\Configurator\WheelCompatibilityConfigurator.exe
   ```
4. Stop the service with **Ctrl+C** (not the window's X) – this also un-hides the real wheel.

Optional – install as a Windows service (Administrator):

```powershell
sc.exe create WheelCompatibilityService binPath= "$PWD\publish\Service\WheelCompatibilityService.exe" start= auto
sc.exe start WheelCompatibilityService
```

> The legacy MSI installer (`build-installer.ps1`, WiX projects) is kept for reference only and does
> not include the Speed Wheel features.

## Configurator tabs

### Main

| Setting | Description |
| --- | --- |
| Steering sensitivity | `1.00` = linear, below = gentler near center, above = twitchier. Curve: `sign(x)·abs(x)^(1/s)` |
| Steering dead zone | 0.00–0.50. Speed Wheel drifts about ±0.07 at rest → **0.08** recommended |
| Rotation angle | 90°–1080° lock to lock. Full lock in the game at half of it per side (180° → full lock at 90°) |
| Calibration | how many degrees you physically turn the wheel when input shows ±1.00 (Speed Wheel ≈ 90°) |
| Steering anti-deadzone | 0–40 %, step 1 %, default 0 (off). Pushes every non-zero steering value above the game's own dead zone – see [F1 25](#f1-25-steering-dead-zone) |
| Device mode | Auto (RacingWheel first, then Speed Wheel) / RacingWheel only / Speed Wheel only |
| Steering axis | Auto (LeftThumbstickX, detects others) / LX / RX / LY / RY |
| Invert steering | swap left/right |
| Virtual output | Auto (ViGEm if installed, else InputInjector) / InputInjector / ViGEm / Both / None (vJoy only) |
| Hide real Speed Wheel | hides the real receiver devices from games via HidHide |
| Also hide XInput interface | untick if a game hangs on start while hiding is on |

Processing order: dead zone → rotation angle → sensitivity → **anti-deadzone** (last, just before the
value is sent to vJoy / ViGEm / InputInjector).

The anti-deadzone formula is `sign(x) · (ad + (1 − ad) · |x|)` for `|x| ≥ 0.001`, and `0` below that
(for pedals `x` is 0..1 and there is no sign). `ad = 0` leaves the value exactly as it was, so old
`settings.json` files keep behaving the same.

The **Live input tester** shows both values: *Wheel input* (raw) and *Game output* (after dead zone,
rotation angle, sensitivity and the anti-deadzone – exactly what the game receives). Below the
throttle/brake bars it lists throttle and brake separately: from the wheel triggers, from the pedal
set, and the value finally sent.

Speed Wheel → virtual controller mapping:

| Speed Wheel | Output |
| --- | --- |
| Steering | Left stick X / vJoy X |
| Right trigger | Right trigger (throttle) / vJoy Y |
| Left trigger | Left trigger (brake) / vJoy Z |
| LB / RB | LB / RB (gear down / up) / vJoy buttons 5 / 6 |
| A, B, X, Y, D-pad, Start, Back | same buttons (Start = Menu, Back = View) |

### Pedals

For an extra pedal set (e.g. shown as "Steering Wheel" in joy.cpl):

1. Press each pedal and watch the raw axes – the one that moves is your pedal axis (usually **Y**).
2. Select the device and axis, release both pedals, click **Calibrate center**.
3. Tick **Use separate pedals**; tick **Swap throttle / brake** if reversed.
4. Set **Pedal dead zone** and **Throttle / Brake: 100 % at pedal travel** (e.g. 50 % = full at half press).
5. Choose **Throttle/brake source**:

| Option | Meaning |
| --- | --- |
| Wheel triggers | only the Speed Wheel triggers (RT = throttle, LT = brake) |
| Separate pedals | only the external pedal set |
| Both (max) | both, whichever is pressed harder wins – **default, the old behaviour** |

6. If the game ignores the first part of the pedal axis, raise **Throttle anti-deadzone** /
   **Brake anti-deadzone** (0–40 %, default 0).

> **Combined pedals limitation:** many older pedal units report both pedals on **one axis** (throttle up,
> brake down). Pressing both at once cancels out in the hardware – this cannot be fixed in software.

### Wheel emulation (vJoy)

1. Install vJoy, reboot, open **Configure vJoy** → device 1: axes **X, Y, Z**, **Buttons 16**, **POVs 0**, Apply.
2. Tick **Enable virtual wheel (vJoy)**.
3. Main tab: *Virtual output* = **None (vJoy wheel only)** and tick *Hide real Speed Wheel*.
4. In the game bind: steering = X, throttle = Y, brake = Z, gears = buttons 5/6.

**Pedal axes** – the vJoy pedal layout is selectable:

| Layout | vJoy axes | Bind in the game |
| --- | --- | --- |
| Separate axes *(default)* | Y = throttle, Z = brake, rest = axis minimum | two axes |
| Combined axis | Y centered: middle = nothing, **up** = throttle, **down** = brake; Z stays at 0 | one axis |

*Combined axis* is for games that only accept a single pedal axis (or that treat a pedal axis as a
stick). **Invert throttle axis** / **Invert brake axis** reverse a pedal: in *Separate axes* the axis
becomes `1 − value`; in *Combined axis* the pedal moves to the opposite end of Y.

**Missing axes warning** – on connect the app calls `GetVJDAxisExist` for X, Y and Z. If one of them
is not enabled on the vJoy device, the Wheel emulation tab shows a red warning
(*"vJoy device 1 is missing axis(es): Y, Z. Enable axes X, Y, Z in 'Configure vJoy'…"*), the same
message is written to `Output.log`, and the missing axes are simply not written until you fix the
vJoy configuration and reconnect.

vJoy buttons: 1 A, 2 B, 3 X, 4 Y, 5 LB, 6 RB, 7 Back, 8 Start, 9 LS, 10 RS, 11–14 D-pad.

> Games with a fixed list of supported wheels (e.g. official F1 titles) identify wheels by hardware
> VID/PID and may not accept vJoy as a wheel. Use the ViGEm output for those games – rotation angle,
> dead zone, sensitivity and the anti-deadzone still apply.

## Recommended settings (Speed Wheel)

| Where | Setting | Value |
| --- | --- | --- |
| App | Sensitivity | 1.00 |
| App | Dead zone | 0.08 |
| App | Rotation angle | 180° |
| App | Calibration | 90° |
| App | Steering anti-deadzone | 0 % (15–20 % for F1 25) |
| Game | Steering dead zone / saturation / linearity | 0 |

## F1 25 steering dead zone

F1 25 has its own built-in steering dead zone. With the vJoy wheel the car does not react to small
steering inputs – roughly up to ±20 % of the axis – and setting **Steering Linearity** to 0 in the
game does **not** remove it.

Fix it from this app instead of the game:

1. Main tab → **Steering anti-deadzone = 15–20 %** (start with 15 %, raise it until the car reacts
   immediately to the smallest turn).
2. In the game set **Steering Linearity = 0** and **Steering wheel range / rotation = 100 %**.
3. Keep the game's own steering dead zone at 0.
4. Check the Main tab tester: with the anti-deadzone at 20 %, a small turn that reads `+0.10` under
   *Wheel input* must show about `+0.28` under *Game output*. `Output.log` prints the same numbers
   every 2 s as `steer raw=… proc=… sent=…`.

The same sliders exist for the pedals (**Throttle / Brake anti-deadzone** on the Pedals tab) if the
game also swallows the first part of the throttle or brake axis.

> Anti-deadzone is applied **last** (after dead zone, rotation angle and sensitivity) and only to
> values that are not zero, so the wheel still returns exactly to centre. It is used for the vJoy
> wheel, ViGEm and InputInjector alike.

## Game notes

| Game | Recommended output | Notes |
| --- | --- | --- |
| F1 25 | ViGEm (or InputInjector) | Choose the Xbox controller profile, game dead zone 0, and set **Steering anti-deadzone 15–20 %** – see above |
| F1 2018 / older F1 | ViGEm | vJoy is not recognised as a wheel |
| CarX Drift Racing Online | ViGEm or vJoy | Works with HidHide hiding and with vJoy. The freezes/hangs that were seen while loading were caused by the **Kino mod (`kino.dll`)**, not by HidHide or vJoy – remove/disable that mod if the game hangs |

If the game sees both the real and the virtual controller, input can be doubled – enable HidHide
hiding or choose the virtual controller in the game's settings.

## Diagnostics

- The service writes `Output.log` next to `WheelCompatibilityService.exe` (recreated on every start):
  detected devices, XInput slots, chosen device, outputs, HidHide/vJoy/pedal actions, every settings
  change, and the axis values every 2 s – raw → processed → sent, with the trigger / pedal / combined
  throttle and brake values and the active anti-deadzone settings:
  `Axes [XInputSpeedWheel] steer raw=+0.42 proc=+0.35 sent=+0.48 | thr trig=0.00 ped=0.60 out=0.60 sent=0.60 | brk … | src=BothMax adz steer=0.20 thr=0.00 brk=0.00 | LX=… buttons=0x0000`.
- A rolling copy: `%ProgramData%\XboxWheelCompatibility\diagnostics.log`.
- The Speed Wheel reports an unusual XInput sub type (`0x52`) – it is detected as a wireless
  non-gamepad XInput device.

## Troubleshooting

| Problem | Fix |
| --- | --- |
| `Service unreachable` | Start the service as Administrator; service and configurator must come from the same build |
| `SocketException 10048` in `Output.log` | Port 16581 is used by an older copy: `Get-Service *Wheel* \| Stop-Service -Force` |
| Build: `Access to ... WheelCompatibilityService.exe is denied` | The service is still running – stop it with Ctrl+C |
| "No wheel connected" | Check *Detected devices* and *Device mode*; send `Output.log` |
| Wrong steering direction | Tick *Invert steering* or change *Steering axis* |
| Game hangs on load | Disable hiding: `& "C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe" --cloak-off` |
| Wheel/pedals invisible after a crash | Same command as above, or untick devices in HidHide Configuration Client |
| Injection error `0x80070422` (InputInjector) | Services → *Xbox Accessory Management Service* → Manual → Start |

## Testing status

Tested by the user with an Xbox 360 Wireless Speed Wheel on Windows 11: Speed Wheel detection,
axes, ViGEm output, HidHide hiding and vJoy output work (X = steering, Y = throttle, Z = brake).
F1 2018 does not accept vJoy as a wheel.

**Not yet verified on hardware:** anti-deadzone (steering / throttle / brake), the throttle/brake
source selector, the combined vJoy pedal axis and the pedal inversions, and the missing-axis warning.
All of them default to the previous behaviour (anti-deadzone 0 %, source *Both (max)*, *Separate
axes*, no inversion), so an existing setup keeps working unchanged. Please report issues together
with `Output.log`.

## Project layout

| Folder | Purpose |
| --- | --- |
| WheelTransformer | Device detection, XInput, pedals, steering curve, ViGEm/InputInjector/vJoy output, HidHide, logging |
| WheelCompatibilityService | Service host and TCP configuration endpoint (port 16581) |
| WheelCompatibilityConfigurator | WPF configurator (Main, Pedals, Wheel emulation tabs) |
| CommunicationEnums | Shared service contract and status objects |
| WheelCompatibilityInstaller / WheelCompatibilitySetup | Legacy WiX installer sources |

## License

[MIT](LICENSE). Original copyright: Camren Mumme.
