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
- **Anti-deadzone** (0–40 %) for steering, throttle and brake — applied last, so games with a built-in
  dead zone (F1 25) react to small inputs without any extra in-game tweaking. 0 % = off (default).
- **Throttle/brake source**: wheel triggers, separate pedals, or both (the stronger one wins; default).
- **Separate pedals** (any extra DirectInput joystick, e.g. an old wheel's pedal unit): combined
  single-axis pedals, dead zone, "100 % at X % travel" per pedal, center calibration, swap.
- **vJoy pedal mapping**: separate Y/Z axes (default) or one centred Y axis
  (up = throttle, down = brake), with independent throttle/brake axis inversion.
- **Hide the real wheel from games** with HidHide, so games only see the virtual device.
- **One click to joy.cpl** (the Windows *Game Controllers* panel) to test the real wheel, the pedals
  and the vJoy device.
- **Diagnostics**: live wheel/pedal/button tester (trigger source, pedal source and the final values),
  raw axes, detected devices list, `Output.log`, plus a warning when a vJoy axis is not enabled.
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
| Steering anti-deadzone | 0–40 % (step 1 %, default 0 = off). Applied last: `\|x\| < 0.001 → 0`, otherwise `sign(x)·(ad + (1 − ad)·\|x\|)`. Compensates the dead zone a game has built in — F1 25 needs about **15–20 %** |
| Rotation angle | 90°–1080° lock to lock. Full lock in the game at half of it per side (180° → full lock at 90°) |
| Calibration | how many degrees you physically turn the wheel when input shows ±1.00 (Speed Wheel ≈ 90°) |
| Device mode | Auto (RacingWheel first, then Speed Wheel) / RacingWheel only / Speed Wheel only |
| Steering axis | Auto (LeftThumbstickX, detects others) / LX / RX / LY / RY |
| Invert steering | swap left/right |
| Virtual output | Auto (ViGEm if installed, else InputInjector) / InputInjector / ViGEm / Both / None (vJoy only) |
| Hide real Speed Wheel | hides the real receiver devices from games via HidHide |
| Also hide XInput interface | untick if a game hangs on start while hiding is on |
| Open joy.cpl | opens the Windows *Game Controllers* panel: check the real wheel, the pedals and the vJoy axes there |

Processing order (steering): dead zone → rotation angle → sensitivity → **anti-deadzone**.
Processing order (throttle/brake): pedal source selection → pedal dead zone → "100 % at X % travel"
→ **anti-deadzone**. The anti-deadzone is applied to everything that leaves the app (vJoy, ViGEm and
InputInjector), at `0 %` the values are exactly as before.

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
5. Choose the **Throttle/brake source**:
   - **Wheel triggers** – only the Speed Wheel's RT/LT,
   - **Separate pedals only** – only the pedal set,
   - **Both (stronger one wins)** – default, the previous behaviour.
6. Set **Throttle / Brake anti-deadzone** (0–40 %) if the game ignores the first part of the pedal
   travel. The value is applied to the final, merged throttle/brake, right before it is sent out.

> **Combined pedals limitation:** many older pedal units report both pedals on **one axis** (throttle up,
> brake down). Pressing both at once cancels out in the hardware – this cannot be fixed in software.

### Wheel emulation (vJoy)

1. Install vJoy, reboot, open **Configure vJoy** → device 1: axes **X, Y, Z**, **Buttons 16**, **POVs 0**, Apply.
2. Tick **Enable virtual wheel (vJoy)**.
3. Click **Open joy.cpl** – the vJoy device must be listed and its axes must move. If an axis is
   missing, a red warning above the status box (and a line in `Output.log`) tells you exactly which one
   to enable in *Configure vJoy* (`włącz oś X/Y/Z w Configure vJoy`). The check runs at every connect
   and then every 5 s.
4. Main tab: *Virtual output* = **None (vJoy wheel only)** and tick *Hide real Speed Wheel*.
5. In the game bind: steering = X, throttle = Y, brake = Z, gears = buttons 5/6.

**Pedal axes in vJoy** (vJoy tab):

| Mode | vJoy mapping | Bind in the game |
| --- | --- | --- |
| **Separate axes** (default) | Y = throttle, Z = brake, both rest at the axis minimum | throttle = Y, brake = Z |
| **Combined axis** | Y centred: middle = nothing, up = throttle, down = brake, Z = 0 | throttle and brake = Y (nothing on Z) |

**Invert throttle axis** / **Invert brake axis** flip the corresponding axis when a pedal works the
other way round (the axis then rests at its maximum). In *Combined axis* mode the value is inverted
before the two pedals are combined.

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
| App | Steering anti-deadzone | 0 % (F1 25: 15–20 %) |
| App | Throttle / brake anti-deadzone | 0 % (F1 25: 10–20 % if the pedals feel late) |
| Game | Steering dead zone / saturation / linearity | 0 |

## Game notes

| Game | Recommended output | Notes |
| --- | --- | --- |
| F1 25 | ViGEm or vJoy | Car still does not react to small steering inputs? Use the **steering anti-deadzone** – see [F1 25](#f1-25) |
| F1 2018 / older F1 | ViGEm | vJoy is not recognised as a wheel |
| CarX Drift Racing Online | ViGEm or vJoy | Works together with HidHide. If it hangs on load, look at your mods – a known culprit was the **Kino** mod (`kino.dll`), not HidHide or vJoy |

If the game sees both the real and the virtual controller, input can be doubled – enable HidHide
hiding or choose the virtual controller in the game's settings.

### F1 25

F1 25 (and the F1 series in general) has a **built-in steering dead zone**: through vJoy the game only
starts reacting at roughly ±20 % of the axis travel, even with *Linearity* and *Dead Zone* set to 0 in
the game. Instead of changing the wheel, compensate it in the app:

| Where | Setting | Value |
| --- | --- | --- |
| App | Steering anti-deadzone | **15–20 %** (start at 15, raise until the car reacts immediately) |
| App | Steering dead zone | 0.06–0.08 (just enough to hide the Speed Wheel drift at rest) |
| App | Rotation angle | 180° (or what you use in other games) |
| App | Calibration | 90° |
| Game, *Calibration* screen | **Linearity** | **0** |
| Game, *Calibration* screen | **Range of movement** / steering lock | **100** |
| Game, *Calibration* screen | Dead zone / saturation | **0** |

How to verify it works:

1. Open the F1 25 **calibration** screen (it draws the steering input as a bar).
2. Turn the Speed Wheel about 10–15° – the bar must move away from the centre immediately.
3. Still nothing? Raise the anti-deadzone by 5 % and repeat. Too twitchy around the centre? Lower it.
4. The same symptoms on the pedals (throttle/brake only reacting after a long press) are fixed with
   **Throttle/Brake anti-deadzone** on the Pedals tab.

> Note: the anti-deadzone shifts the *bottom* of the axis; it does not reduce the maximum. Full lock
> is still reached at exactly the same wheel angle, which is why *Linearity 0* and *Range 100* are
> recommended together with it.

> **Important:** with the anti-deadzone on, the steering dead zone must **not** be 0. The anti-deadzone
> never sends less than its own value, so the Speed Wheel drift at rest (±0.07) would become about
> ±0.29 of steering and the car would pull to one side. Keep *Steering dead zone* at **0.06–0.08**
> (the configurator shows a yellow warning when the combination is risky).

## Diagnostics

- The service writes `Output.log` next to `WheelCompatibilityService.exe` (recreated on every start):
  detected devices, XInput slots, chosen device, outputs, HidHide/vJoy/pedal actions.
- Every settings change is logged with its value (anti-deadzone, pedal source, vJoy pedal mode,
  axis inversion, ranges, …).
- While wheel input is changing, a line is written every 3 s (when nothing moves: every 15 s) with the
  whole chain: `raw … | triggers … | pedals … source=… | processed: deadzone … sensitivity …
  anti-deadzone … | out: steer … thr … brk … | vJoy X= Y= Z= (mode=…)`.
- A rolling copy: `%ProgramData%\XboxWheelCompatibility\diagnostics.log`.
- The **live input tester** (Main tab) shows the raw wheel value, the value sent to the game, both
  raw throttle/brake sources (triggers and separate pedals) and the final throttle/brake bars, plus
  the active pedal source. **Open joy.cpl** starts the Windows *Game Controllers* panel.
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
| Game reacts only to large steering inputs (F1 25) | Raise *Steering anti-deadzone* (Main tab) to 15–20 %; in the game set *Linearity* 0, *Range of movement* 100, dead zone 0 |
| Car pulls to one side with the anti-deadzone on | *Steering dead zone* is too low (0) — set 0.06–0.08, the anti-deadzone amplifies the wheel's rest drift otherwise |
| Pedals react late | Raise *Throttle/Brake anti-deadzone* (Pedals tab); check the source selection |
| Throttle/brake axes swapped or reversed in the game | Swap them in the game, or use *Swap throttle / brake* (Pedals) / *Invert throttle/brake axis* (vJoy tab) |
| Red "missing axis" warning on the vJoy tab | Enable that axis for the device in *Configure vJoy*, then click Apply — the app re-checks every 5 s |
| Wheel/pedals not listed in joy.cpl | They are hidden by HidHide: use *Hide real Speed Wheel* to unhide, or run `HidHideCLI.exe --cloak-off` |
| Game hangs on load | Disable hiding: `& "C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe" --cloak-off`. If it still hangs, check the mods you installed (a known case: the Kino mod, `kino.dll` — not HidHide/vJoy) |
| Wheel/pedals invisible after a crash | Same command as above, or untick devices in HidHide Configuration Client |
| Injection error `0x80070422` (InputInjector) | Services → *Xbox Accessory Management Service* → Manual → Start |

## Testing status

Tested by the user with an Xbox 360 Wireless Speed Wheel on Windows 11: Speed Wheel detection,
axes, ViGEm output, HidHide hiding and vJoy output work. F1 2018 does not accept vJoy as a wheel.
The developer did not test the code on physical hardware – please report issues with `Output.log`.

The options added in this revision (steering/throttle/brake anti-deadzone, throttle-brake source,
vJoy pedal axis mode, vJoy axis inversion, the vJoy axis check and the joy.cpl button) compile with the
same toolchain as the rest of the project but were **not** verified on real hardware by the author –
an `Output.log` line starting with `Axes [` shows the whole chain and makes checking easy.

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
