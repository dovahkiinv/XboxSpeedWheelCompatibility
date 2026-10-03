# Xbox Wheel Compatibility

[English](README.md) | **Polski**

Zamienia kierownicę – także **Microsoft Xbox 360 Wireless Speed Wheel** – w wirtualny kontroler Xbox
albo wirtualną kierownicę DirectInput w Windows. Obsługuje kąt obrotu, martwe pole, czułość, osobne
pedały i diagnostykę na żywo.

Projekt bazuje na [XboxWheelCompatibility autorstwa Camrena Mumme](https://github.com/camren-m/XboxWheelCompatibility).
Oryginalna licencja MIT i historia Git zostały zachowane.

## Funkcje

- **Urządzenia wejściowe** (wybór automatyczny):
  1. prawdziwe kierownice widoczne w Windows jako `RacingWheel` (oryginalne działanie),
  2. **Xbox 360 Wireless Speed Wheel** przez XInput (Xbox 360 Wireless Receiver for Windows),
  3. bezprzewodowe pady Xbox 360 jako opcja zapasowa.
- **Wirtualne wyjścia**:
  - **ViGEm** – wirtualny pad Xbox 360, widoczny w grach XInput, DirectInput i nowych,
  - **InputInjector** (wbudowany w Windows) – tylko nowe gry (Windows.Gaming.Input / GameInput),
  - **vJoy** – wirtualna kierownica DirectInput: X = skręt, Y = gaz, Z = hamulec, 14 przycisków.
- **Skręt**: kąt obrotu 90°–1080° (jak w prawdziwej kierownicy), kalibracja fizycznego obrotu,
  martwe pole, krzywa czułości 0,01–3,00, odwrócenie, wybór osi skrętu.
- **Anti-deadzone** (0–40%) dla skrętu, gazu i hamulca – stosowany na końcu, dzięki czemu gry
  z wbudowaną martwą strefą (F1 25) reagują na małe ruchy bez kombinowania w grze. 0% = wyłączony (domyślnie).
- **Źródło gazu/hamulca**: spusty kierownicy, osobne pedały albo oba (wygrywa mocniej wciśnięty; domyślnie).
- **Osobne pedały** (dowolny dodatkowy joystick DirectInput, np. pedały ze starej kierownicy):
  pedały na jednej osi, martwe pole, „100% przy X% wciśnięcia” dla każdego pedału, kalibracja
  środka, zamiana gazu z hamulcem.
- **Mapowanie pedałów w vJoy**: osobne osie Y/Z (domyślnie) albo jedna wycentrowana oś Y
  (góra = gaz, dół = hamulec), z niezależnym odwróceniem osi gazu i hamulca.
- **Ukrywanie prawdziwej kierownicy przed grami** przez HidHide – gra widzi tylko wirtualne urządzenie.
- **Przycisk joy.cpl** – jednym kliknięciem otwiera panel *Urządzenia do gier* w Windows, żeby
  przetestować prawdziwą kierownicę, pedały i urządzenie vJoy.
- **Diagnostyka**: tester kierownicy, pedałów i przycisków (osobno spusty, pedały i wartości końcowe),
  surowe osie, lista urządzeń, `Output.log` oraz ostrzeżenie, gdy w vJoy brakuje osi.
- Ustawienia zapisują się w `%ProgramData%\XboxWheelCompatibility\settings.json`.

## Wymagania

| Składnik | Do czego |
| --- | --- |
| Windows 10 22H2 / Windows 11, x64 | zawsze |
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (lub nowszy SDK) | kompilacja |
| [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) | uruchamianie |
| Xbox 360 Wireless Receiver for Windows | Speed Wheel |
| [ViGEmBus](https://github.com/nefarius/ViGEmBus/releases) | wyjście ViGEm (zalecane) |
| [HidHide](https://github.com/nefarius/HidHide/releases) | ukrywanie prawdziwej kierownicy (opcjonalnie) |
| [vJoy](https://github.com/BrunnerInnovation/vJoy/releases) | wirtualna kierownica (opcjonalnie) |

Po instalacji ViGEmBus, HidHide lub vJoy uruchom komputer ponownie.

## Kompilacja

W PowerShellu, w folderze repozytorium:

```powershell
git clone https://github.com/dovahkiinv/XboxSpeedWheelCompatibility.git
cd XboxSpeedWheelCompatibility
.\build.ps1 -Publish
```

Wynik: `publish\Service` i `publish\Configurator`. Przed kompilacją zamknij działający serwis i okno
programu – inaczej Windows blokuje plik `.exe` (skrypt o tym ostrzeże).

To samo ręcznie:

```powershell
dotnet publish WheelCompatibilityService/WheelCompatibilityService.csproj -c Release -r win-x64 --self-contained false -o publish/Service
dotnet publish WheelCompatibilityConfigurator/WheelCompatibilityConfigurator.csproj -c Release -r win-x64 --self-contained false -o publish/Configurator
```

## Uruchamianie

1. Jeśli masz zainstalowaną starszą wersję jako usługę Windows, zatrzymaj ją (obie używają portu TCP 16581):
   ```powershell
   Get-Service *Wheel* | Stop-Service -Force
   Get-Service *Wheel* | Set-Service -StartupType Manual
   ```
2. Uruchom serwis w PowerShellu **jako administrator** i zostaw okno otwarte:
   ```powershell
   .\publish\Service\WheelCompatibilityService.exe
   ```
3. W drugim oknie uruchom program konfiguracyjny:
   ```powershell
   .\publish\Configurator\WheelCompatibilityConfigurator.exe
   ```
4. Serwis zatrzymuj przez **Ctrl+C** (nie krzyżykiem okna) – wtedy przywraca też widoczność prawdziwej kierownicy.

Opcjonalnie – instalacja jako usługa Windows (jako administrator):

```powershell
sc.exe create WheelCompatibilityService binPath= "$PWD\publish\Service\WheelCompatibilityService.exe" start= auto
sc.exe start WheelCompatibilityService
```

> Stary instalator MSI (`build-installer.ps1`, projekty WiX) został tylko dla porządku i nie zawiera
> funkcji Speed Wheela.

## Zakładki programu

### Main

| Ustawienie | Opis |
| --- | --- |
| Steering sensitivity (czułość) | `1,00` = liniowo, mniej = łagodniej przy środku, więcej = nerwowo. Krzywa: `sign(x)·abs(x)^(1/s)` |
| Steering dead zone (martwe pole) | 0,00–0,50. Speed Wheel w spoczynku waha się ok. ±0,07 → zalecane **0,08** |
| Steering anti-deadzone | 0–40% (krok 1%, domyślnie 0 = wyłączony). Stosowany na końcu: `\|x\| < 0,001 → 0`, inaczej `sign(x)·(ad + (1 − ad)·\|x\|)`. Kompensuje martwą strefę wbudowaną w grę – F1 25 potrzebuje ok. **15–20%** |
| Rotation angle (kąt obrotu) | 90°–1080° od oporu do oporu. Pełny skręt w grze przy połowie w każdą stronę (180° → pełny skręt przy 90°) |
| Calibration (kalibracja) | o ile stopni fizycznie obracasz kierownicę, gdy wejście pokazuje ±1,00 (Speed Wheel ≈ 90°) |
| Device mode | Auto (najpierw RacingWheel, potem Speed Wheel) / tylko RacingWheel / tylko Speed Wheel |
| Steering axis (oś skrętu) | Auto (LeftThumbstickX, wykrywa inne) / LX / RX / LY / RY |
| Invert steering | zamienia lewo z prawem |
| Virtual output (wyjście) | Auto (ViGEm, jeśli jest, inaczej InputInjector) / InputInjector / ViGEm / oba / brak (tylko vJoy) |
| Hide real Speed Wheel | ukrywa urządzenia prawdziwego odbiornika przed grami (HidHide) |
| Also hide XInput interface | odznacz, jeśli gra zawiesza się przy starcie przy włączonym ukrywaniu |
| Open joy.cpl | otwiera panel *Urządzenia do gier* Windows: sprawdzisz tam prawdziwą kierownicę, pedały i osie vJoy |

Kolejność przetwarzania (skręt): martwe pole → kąt obrotu → czułość → **anti-deadzone**.
Kolejność przetwarzania (gaz/hamulec): wybór źródła → martwe pole pedałów → „100% przy X% wciśnięcia”
→ **anti-deadzone**. Anti-deadzone jest stosowany do wszystkiego, co wychodzi z programu (vJoy, ViGEm
i InputInjector); przy 0% wartości są dokładnie takie jak wcześniej.

Mapowanie Speed Wheela na wirtualny kontroler:

| Speed Wheel | Wyjście |
| --- | --- |
| Skręt | lewa gałka X / vJoy X |
| Prawy spust | prawy spust (gaz) / vJoy Y |
| Lewy spust | lewy spust (hamulec) / vJoy Z |
| LB / RB | LB / RB (bieg w dół / w górę) / vJoy przyciski 5 / 6 |
| A, B, X, Y, D-pad, Start, Back | te same przyciski (Start = Menu, Back = View) |

### Pedals (pedały)

Dla dodatkowych pedałów (np. widocznych w joy.cpl jako „Steering Wheel”):

1. Wciśnij każdy pedał i obserwuj surowe osie – ta, która się rusza, to oś pedałów (zwykle **Y**).
2. Wybierz urządzenie i oś, puść oba pedały i kliknij **Calibrate center**.
3. Zaznacz **Use separate pedals**; jeśli gaz i hamulec są zamienione – **Swap throttle / brake**.
4. Ustaw **Pedal dead zone** oraz **Throttle / Brake: 100% at pedal travel**
   (np. 50% = pełny gaz/hamulec przy wciśnięciu do połowy).
5. Wybierz **Throttle/brake source**:
   - **Wheel triggers** – tylko spusty Speed Wheela (RT/LT),
   - **Separate pedals only** – tylko pedały,
   - **Both (stronger one wins)** – domyślnie, zachowanie jak dotychczas.
6. Ustaw **Throttle / Brake anti-deadzone** (0–40%), jeśli gra ignoruje początek ruchu pedału.
   Wartość jest stosowana do końcowego, połączonego gazu/hamulca, tuż przed wysłaniem.

> **Ograniczenie pedałów na jednej osi:** wiele starszych pedałów wysyła oba pedały na **jednej osi**
> (gaz w górę, hamulec w dół). Wciśnięcie obu naraz znosi się już w samym urządzeniu – programowo
> nie da się tego naprawić.

### Wheel emulation (vJoy)

1. Zainstaluj vJoy, uruchom ponownie komputer, otwórz **Configure vJoy** → urządzenie 1:
   osie **X, Y, Z**, **Buttons 16**, **POVs 0**, Apply.
2. Zaznacz **Enable virtual wheel (vJoy)**.
3. Kliknij **Open joy.cpl** – urządzenie vJoy musi być na liście, a jego osie muszą się ruszać.
   Jeśli brakuje osi, czerwone ostrzeżenie nad ramką statusu (i linia w `Output.log`) powie która to oś
   i co włączyć w *Configure vJoy* (`włącz oś X/Y/Z w Configure vJoy`). Sprawdzenie odbywa się przy
   połączeniu i potem co 5 s.
4. Zakładka Main: *Virtual output* = **None (vJoy wheel only)** i zaznacz *Hide real Speed Wheel*.
5. W grze przypisz: skręt = X, gaz = Y, hamulec = Z, biegi = przyciski 5/6.

**Osie pedałów w vJoy** (zakładka vJoy):

| Tryb | Mapowanie vJoy | Co przypisać w grze |
| --- | --- | --- |
| **Separate axes** (domyślnie) | Y = gaz, Z = hamulec, spoczynek = minimum osi | gaz = Y, hamulec = Z |
| **Combined axis** | Y wycentrowana: środek = nic, góra = gaz, dół = hamulec, Z = 0 | gaz i hamulec = Y (nic na Z) |

**Invert throttle axis** / **Invert brake axis** odwracają daną oś, gdy pedał działa w drugą stronę
(oś spoczywa wtedy na maksimum). W trybie *Combined axis* wartość jest odwracana przed połączeniem
obu pedałów.

Przyciski vJoy: 1 A, 2 B, 3 X, 4 Y, 5 LB, 6 RB, 7 Back, 8 Start, 9 LS, 10 RS, 11–14 D-pad.

> Gry z listą obsługiwanych kierownic (np. oficjalne gry F1) rozpoznają kierownice po sprzętowym
> VID/PID i mogą nie przyjąć vJoy jako kierownicy. W takich grach użyj wyjścia ViGEm – kąt obrotu,
> martwe pole, czułość i anti-deadzone nadal działają.

## Zalecane ustawienia (Speed Wheel)

| Gdzie | Ustawienie | Wartość |
| --- | --- | --- |
| Program | Czułość | 1,00 |
| Program | Martwe pole | 0,08 |
| Program | Kąt obrotu | 180° |
| Program | Kalibracja | 90° |
| Program | Steering anti-deadzone | 0% (F1 25: 15–20%) |
| Program | Throttle / brake anti-deadzone | 0% (F1 25: 10–20%, jeśli pedały reagują późno) |
| Gra | Martwa strefa / nasycenie / liniowość skrętu | 0 |

## Uwagi do gier

| Gra | Zalecane wyjście | Uwagi |
| --- | --- | --- |
| F1 25 | ViGEm lub vJoy | Auto nadal nie reaguje na mały skręt? Użyj **steering anti-deadzone** – patrz [F1 25](#f1-25) |
| F1 2018 / starsze F1 | ViGEm | vJoy nie jest rozpoznawany jako kierownica |
| CarX Drift Racing Online | ViGEm lub vJoy | Działa razem z HidHide. Jeśli gra zawiesza się przy wczytywaniu, sprawdź mody – znanym winowajcą był mod **Kino** (`kino.dll`), a nie HidHide ani vJoy |

Jeśli gra widzi jednocześnie prawdziwy i wirtualny kontroler, sterowanie może się dublować – włącz
ukrywanie przez HidHide albo wybierz wirtualny kontroler w ustawieniach gry.

### F1 25

F1 25 (i seria F1 w ogóle) ma **wbudowaną martwą strefę skrętu**: przez vJoy gra zaczyna reagować
dopiero przy ok. ±20% wychylenia osi, nawet gdy w grze *Linearity* i *Dead Zone* są ustawione na 0.
Nie trzeba zmieniać kierownicy – kompensuje to program:

| Gdzie | Ustawienie | Wartość |
| --- | --- | --- |
| Program | Steering anti-deadzone | **15–20%** (zacznij od 15, zwiększaj, aż auto reaguje natychmiast) |
| Program | Steering dead zone | 0,06–0,08 (tyle, by ukryć dryf Speed Wheela w spoczynku) |
| Program | Kąt obrotu | 180° (lub tyle, ile używasz w innych grach) |
| Program | Kalibracja | 90° |
| Gra, ekran *Calibration* | **Linearity** | **0** |
| Gra, ekran *Calibration* | **Range of movement** / blokada skrętu | **100** |
| Gra, ekran *Calibration* | Martwa strefa / nasycenie | **0** |

Jak sprawdzić, że działa:

1. Otwórz w F1 25 ekran **kalibracji** (rysuje wychylenie skrętu jako pasek).
2. Obróć Speed Wheel o ok. 10–15° – pasek musi od razu odejść od środka.
3. Nadal nic? Zwiększ anti-deadzone o 5% i powtórz. Za nerwowo przy środku? Zmniejsz.
4. Te same objawy na pedałach (gaz/hamulec reaguje dopiero po długim wciśnięciu) leczy
   **Throttle/Brake anti-deadzone** na zakładce Pedals.

> Uwaga: anti-deadzone podnosi *dół* charakterystyki; nie zmniejsza maksimum. Pełny skręt osiągasz
> przy dokładnie tym samym kącie kierownicy – dlatego zalecamy go razem z *Linearity 0* i *Range 100*.

> **Ważne:** przy włączonym anti-deadzone martwe pole skrętu **nie może** być zerowe. Anti-deadzone
> nigdy nie wysyła mniej niż swoją wartość, więc dryf Speed Wheela w spoczynku (±0,07) zamieniłby się
> w ok. ±0,29 skrętu i auto zjeżdżałoby na jedną stronę. Zostaw *Steering dead zone* na **0,06–0,08**
> (program pokaże żółte ostrzeżenie, gdy połączenie jest ryzykowne).

## Diagnostyka

- Serwis zapisuje `Output.log` obok `WheelCompatibilityService.exe` (nowy przy każdym starcie):
  wykryte urządzenia, gniazda XInput, wybrane urządzenie, wyjścia, operacje HidHide/vJoy/pedałów.
- Każda zmiana ustawienia jest logowana razem z wartością (anti-deadzone, źródło pedałów, tryb osi
  vJoy, odwrócenie osi, zakresy itd.).
- Gdy wejście się zmienia, co 3 s pojawia się linia z całym łańcuchem (gdy nic się nie dzieje – co 15 s):
  `raw … | triggers … | pedals … source=… | processed: deadzone … sensitivity … anti-deadzone … |
  out: steer … thr … brk … | vJoy X= Y= Z= (mode=…)`.
- Kopia zbiorcza: `%ProgramData%\XboxWheelCompatibility\diagnostics.log`.
- **Live input tester** (zakładka Main) pokazuje surową wartość skrętu, wartość wysyłaną do gry, oba
  źródła gazu/hamulca (spusty i osobne pedały) oraz końcowe paski gazu/hamulca i aktywne źródło.
  **Open joy.cpl** otwiera panel *Urządzenia do gier* Windows.
- Speed Wheel zgłasza nietypowy podtyp XInput (`0x52`) – jest wykrywany jako bezprzewodowe
  urządzenie XInput, które nie jest zwykłym padem.

## Rozwiązywanie problemów

| Problem | Rozwiązanie |
| --- | --- |
| `Service unreachable` | Uruchom serwis jako administrator; serwis i program muszą pochodzić z tej samej kompilacji |
| `SocketException 10048` w `Output.log` | Port 16581 zajmuje starsza wersja: `Get-Service *Wheel* \| Stop-Service -Force` |
| Kompilacja: `Access to ... WheelCompatibilityService.exe is denied` | Serwis nadal działa – zatrzymaj go przez Ctrl+C |
| „No wheel connected” | Sprawdź *Detected devices* i *Device mode*; przyślij `Output.log` |
| Skręt w złą stronę | Zaznacz *Invert steering* lub zmień *Steering axis* |
| Gra reaguje dopiero przy dużym skręcie (F1 25) | Zwiększ *Steering anti-deadzone* (zakładka Main) do 15–20%; w grze ustaw *Linearity* 0, *Range of movement* 100, martwą strefę 0 |
| Auto zjeżdża na jedną stronę przy włączonym anti-deadzone | *Steering dead zone* jest za małe (0) – ustaw 0,06–0,08; inaczej anti-deadzone wzmacnia dryf kierownicy w spoczynku |
| Pedały reagują późno | Zwiększ *Throttle/Brake anti-deadzone* (zakładka Pedals); sprawdź wybór źródła |
| Gaz i hamulec zamienione lub odwrócone w grze | Zamień je w grze albo użyj *Swap throttle / brake* (Pedals) / *Invert throttle/brake axis* (vJoy) |
| Czerwone ostrzeżenie o brakującej osi na zakładce vJoy | Włącz tę oś dla urządzenia w *Configure vJoy* i kliknij Apply – program sprawdza to co 5 s |
| Kierownicy/pedałów nie ma w joy.cpl | Są ukryte przez HidHide: odznacz *Hide real Speed Wheel* albo uruchom `HidHideCLI.exe --cloak-off` |
| Gra nie może się wczytać | Wyłącz ukrywanie: `& "C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe" --cloak-off`. Jeśli nadal się zawiesza, sprawdź zainstalowane mody (znany przypadek: mod Kino, `kino.dll` – nie HidHide/vJoy) |
| Kierownica/pedały zniknęły po awarii | To samo polecenie co wyżej albo odznacz urządzenia w HidHide Configuration Client |
| Błąd wstrzykiwania `0x80070422` (InputInjector) | Usługi → *Xbox Accessory Management Service* → Ręczny → Uruchom |

## Stan testów

Przetestowane przez użytkownika z Xbox 360 Wireless Speed Wheel na Windows 11: wykrywanie Speed
Wheela, osie, wyjście ViGEm, ukrywanie przez HidHide i wyjście vJoy działają. F1 2018 nie przyjmuje
vJoy jako kierownicy. Autor zmian nie testował kodu na fizycznym sprzęcie – problemy zgłaszaj
razem z plikiem `Output.log`.

Opcje dodane w tej wersji (anti-deadzone skrętu/gazu/hamulca, źródło gazu-hamulca, tryb osi pedałów
w vJoy, odwracanie osi vJoy, sprawdzanie osi vJoy i przycisk joy.cpl) kompilują się tym samym
łańcuchem narzędzi co reszta projektu, ale **nie zostały sprawdzone na prawdziwym sprzęcie** przez
autora – linia w `Output.log` zaczynająca się od `Axes [` pokazuje cały łańcuch i bardzo to ułatwia.

## Struktura projektu

| Folder | Zawartość |
| --- | --- |
| WheelTransformer | Wykrywanie urządzeń, XInput, pedały, krzywa skrętu, wyjścia ViGEm/InputInjector/vJoy, HidHide, logi |
| WheelCompatibilityService | Serwis i punkt konfiguracji TCP (port 16581) |
| WheelCompatibilityConfigurator | Program WPF (zakładki Main, Pedals, Wheel emulation) |
| CommunicationEnums | Wspólny kontrakt serwisu i obiekty statusu |
| WheelCompatibilityInstaller / WheelCompatibilitySetup | Stare źródła instalatora WiX |

## Licencja

[MIT](LICENSE). Oryginalne prawa autorskie: Camren Mumme.
