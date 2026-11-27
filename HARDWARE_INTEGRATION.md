# Focus AI – ghid de integrare hardware/software

Acest document descrie firmware-ul și protocolul incluse în această arhivă. Este important să nu amestecați sketch-ul vechi MAX30100 din documentația anterioară cu firmware-ul curent MAX30102.

## 1. Componente și cablare

| Modul | Arduino Uno R3 | Observații |
|---|---|---|
| MAX30102 PPG | SDA → A4, SCL → A5, GND comun, alimentare conform breakout-ului | Unele plăci breakout au regulator și translatoare de nivel, altele nu. Verificați specificația modulului; nu aplicați 5 V unui modul 3,3 V-only. Achiziția FIFO este configurată la aproximativ 100 Hz. |
| Ieșire analogică MyoWare | SIG → A0, GND comun, alimentare conform modulului | Se salvează ADC brut 0–1023. Nu este numit ECG și nu trebuie folosit ca ECG clinic. |
| Canal analogic secundar (opțional) | SIG → A1 | Dezactivat implicit (`USE_SECONDARY_ANALOG_CHANNEL 0`). Activați numai dacă aveți un al doilea senzor și cablare validă. |
| Senzor touch digital | OUT → D2, GND comun, VCC conform modulului | Firmware-ul detectează frontul HIGH cu debounce de 45 ms. Pinul este `INPUT`; modulul trebuie să furnizeze nivel logic stabil. |
| Buzzer pasiv | + → D3, − → GND | Comanda `BEEP` produce un ton de aproximativ 2 kHz / 150 ms. Pentru buzzer/modul care cere driver, folosiți etajul de comandă potrivit. |
| Ultrasonic HC-SR04 compatibil | TRIG → D4, ECHO → D5, VCC → 5 V, GND comun | Opțional. Măsurare la aproximativ 250 ms; timeout 6 ms. Verificați tensiunea ECHO pentru modulele diferite de HC-SR04. |
| PC Windows | USB Arduino | Serial 115200 baud. Numărul COM este descoperit automat prin handshake; nu trebuie configurat COM5 manual. |

Toate modulele trebuie să aibă masă comună. Nu conectați simultan două ieșiri care conduc același pin. Verificați pinout-ul fiecărei plăci reale înainte de alimentare.

## 2. Pregătirea și încărcarea firmware-ului

1. Instalați Arduino IDE 2.x și selectați **Arduino Uno** ca placă.
2. Din Library Manager instalați **SparkFun MAX3010x Pulse and Proximity Sensor Library**. Aceasta furnizează `MAX30105.h` și `heartRate.h`.
3. Deschideți `cod_arduino/cod_arduino.ino`. Fișierele `spo2_algorithm.cpp` și `spo2_algorithm.h` din același folder sunt incluse în arhivă; nu le mutați separat.
4. Selectați portul plăcii în Arduino IDE și încărcați sketch-ul.
5. Deschideți Serial Monitor la 115200 baud pentru diagnoză. După pornire trebuie să apară `READY,FOCUS_AI,2`.
6. În aplicația WPF apăsați/porniți fluxul de test. Aplicația caută toate porturile COM și validează dispozitivul cu `PING,FOCUS_AI` → `PONG,FOCUS_AI,2`.

Dacă aplicația pornește înaintea plăcii, reconectarea automată continuă să caute Arduino. Dacă schimbați firmware-ul, verificați că răspunsul de handshake se termină cu versiunea `2`.

## 3. Protocol serial v2

Toate liniile sunt terminate cu newline, iar comunicația este la 115200 baud.

| Direcție | Linie | Semnificație |
|---|---|---|
| PC → Arduino | `PING,FOCUS_AI` | Handshake pentru identificarea plăcii. |
| Arduino → PC | `PONG,FOCUS_AI,2` | Confirmă că portul aparține firmware-ului Focus AI v2. |
| PC → Arduino | `START_TEST` / `STOP_TEST` | Pornește/oprește colectarea de date de sesiune. |
| PC → Arduino | `BEEP` | Pornește buzzerul hardware. |
| Arduino → PC | `BEEP_STARTED,millis` | Timestamp-ul comenzii de buzzer pe ceasul Arduino. |
| Arduino → PC | `TOUCH_DETECTED,millis` | Eveniment touch cu timestamp pe ceasul Arduino. |
| Arduino → PC | `DATA,millis,A0,A1,HR,SpO2,FINGER,PPG_READY,DISTANCE_CM,DISTANCE_UPDATED` | Eșantion analogic, status PPG și măsurare ultrasonică. `A1=-1` dacă al doilea canal este dezactivat; distanța este `-1` când nu există măsurare validă. |
| Arduino → PC | `PPG_RR,millis,interval_ms,IR_amplitude` | Interval pulse-to-pulse detectat din PPG; este PRV, nu RR ECG. |

Aplicația acceptă datele PPG doar prin mesajul explicit `PPG_RR`; nu acceptă vechiul mesaj necalificat `RR,<valoare>`. Timestamp-urile Arduino sunt `millis()` și se pot reseta la reboot; pentru durate scurte diferența unsigned este tratată wrap-safe.

## 4. Ce măsoară fiecare senzor

- **MAX30102:** semnal PPG optic, BPM, SpO₂ estimat și intervale între pulsuri. FIFO-ul este citit la aproximativ 100 Hz pentru detecția pulsului; pentru algoritmul SpO₂, patru eșantioane sunt mediate software la 25 Hz. Intervalele sunt salvate drept PRV. PPG/PRV nu este echivalent cu ECG/RR și depinde de mișcare, perfuzie, contact și calitatea semnalului.
- **MyoWare A0:** eșantion analogic brut. Nu este transformat în ECG clinic și nu este folosit ca atare pentru HRV.
- **Touch + buzzer:** timpul de reacție hardware se calculează din timestamp-urile `BEEP_STARTED` și `TOUCH_DETECTED`; tastatura Space rămâne fallback când nu există confirmare hardware.
- **Ultrasonic:** distanță brută în centimetri, doar pentru eșantioanele valide. Aplicația păstrează și indicatorul istoric `dist` (sub pragul de 30 cm), dar salvează și `dist_cm`.
- **Eye tracker:** `gaze_samples.csv` păstrează eșantioane la aproximativ 10 Hz, inclusiv pierderile de tracking. În faza cu punct portocaliu, coordonatele privirii sunt comparate cu AOI. Măsura este o aproximare bazată pe calibrare, nu eye-tracking clinic.

## 5. Pupilometrie, NFC și NumPad – limitări explicite

- Pipeline-ul curent MediaPipe Face Mesh nu măsoară diametrul pupilei. Iris landmarks nu sunt convertiți într-o măsură de dilatare. Firebase marchează `pupil_measurement_available=false`; nu se generează valori artificiale.
- În arhiva-sursă nu există un model identificabil pentru cititorul NFC, keypad-ul/NumPad-ul, schema de pini sau protocolul lor. Există doar parsare generică pentru un UID NFC în `BioCollector`, dar firmware-ul v2 nu emite UID-uri. Prin urmare autentificarea NFC + PIN nu este implementată și nu trebuie prezentată ca funcționalitate activă.

## 6. Validare și depanare

- **Arduino nu apare în aplicație:** confirmați că în Serial Monitor apare `READY,FOCUS_AI,2`, închideți Serial Monitor (un singur proces poate deține portul) și reconectați USB.
- **COM găsit, dar handshake eșuat:** verificați viteza 115200, sketch-ul v2 și faptul că nu este încărcat firmware-ul MAX30100 vechi.
- **PPG absent:** verificați alimentarea specifică breakout-ului, I²C A4/A5, contactul cu degetul și dacă `PPG_READY` este 1.
- **Touch fără reacție:** verificați OUT → D2, GND comun și ieșirea logică HIGH a modulului.
- **Distanța lipsește:** verificați TRIG D4, ECHO D5, VCC și GND. Un cadru cu `DISTANCE_CM=-1` înseamnă măsurare invalidă/lipsă; aplicația nu inventează o distanță.
- **Eye tracker:** instalați dependențele din `EyeTracker-main/Webcam3DTracker/requirements.txt` într-un mediu Python disponibil în PATH sau în folderul `eyetracker_env`/`eyetracker_env312`.

## 7. Build și nivel de validare

Aplicația WPF țintește `net9.0-windows`; testele xUnit sunt în `focus-ai/tests/FocusAI.Tests`. Rulați pe Windows cu .NET 9 SDK:

```powershell
dotnet restore .\focus-ai.sln
dotnet build .\focus-ai.sln -c Release
dotnet test .\focus-ai\tests\FocusAI.Tests\FocusAI.Tests.csproj -c Release
```

Firmware-ul se verifică prin Arduino IDE cu placa și biblioteca SparkFun instalate. În mediul de pregătire al acestei arhive nu au fost disponibile .NET SDK, Arduino CLI sau placa fizică; de aceea nu se declară aici un build sau test hardware trecut. Testarea pe placa și senzorii reali rămâne obligatorie înainte de o demonstrație sau utilizare experimentală.
