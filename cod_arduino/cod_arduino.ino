#include <Wire.h>
#include <MAX30100_PulseOximeter.h>

const int TOUCH_PIN  = 2;
const int BUZZER_PIN = 3;
const int TRIG_PIN   = 4;
const int ECHO_PIN   = 5;

const int ECG_DR = A0;
const int ECG_ST = A1;

#define REPORTING_PERIOD_MS 1000

PulseOximeter pox;

bool isCollecting = false;
bool lastTouchState = LOW;
uint32_t lastDebounceTime = 0;
uint32_t tsLastReport = 0;

void beep(int freq, int durationMs) {
    tone(BUZZER_PIN, freq, durationMs);
}

void setup() {
    Serial.begin(115200);

    pinMode(TOUCH_PIN, INPUT);
    pinMode(BUZZER_PIN, OUTPUT);
    pinMode(TRIG_PIN, OUTPUT);
    pinMode(ECHO_PIN, INPUT);

    Wire.begin();

    if (pox.begin()) {
        pox.setIRLedCurrent(MAX30100_LED_CURR_7_6MA);
    }

    analogReadResolution(12);

    Serial.println("READY");
}

long readDistanceCm() {
    digitalWrite(TRIG_PIN, LOW);
    delayMicroseconds(2);

    digitalWrite(TRIG_PIN, HIGH);
    delayMicroseconds(10);
    digitalWrite(TRIG_PIN, LOW);

    long duration = pulseIn(ECHO_PIN, HIGH, 30000); // timeout 30ms

    if (duration == 0) return -1; // nimic detectat

    long distance = duration * 0.034 / 2;
    return distance;
}

void loop() {

    // ===== COMENZI DIN PC =====
    if (Serial.available() > 0) {
        String cmd = Serial.readStringUntil('\n');
        cmd.trim();

        if (cmd == "START_TEST") {
            isCollecting = true;
            tsLastReport = millis();
        }
        else if (cmd == "STOP_TEST") {
            isCollecting = false;
        }
        else if (cmd == "BEEP") {
            beep(2000, 150);   // 🔊 DOAR aici sună
        }
    }

    pox.update();

    // ===== TOUCH =====
    bool curTouch = digitalRead(TOUCH_PIN);

    if (curTouch == HIGH && lastTouchState == LOW) {
        if (millis() - lastDebounceTime > 50) {
            lastDebounceTime = millis();
            Serial.println("TOUCH_DETECTED");
        }
    }

    lastTouchState = curTouch;

    // ===== DATA =====
    if (isCollecting) {
        uint32_t now = millis();

        if (now - tsLastReport >= REPORTING_PERIOD_MS) {
            tsLastReport = now;

            int ecgDr = analogRead(ECG_DR);
            int ecgSt = analogRead(ECG_ST);

            uint8_t hr = (uint8_t)pox.getHeartRate();
            uint8_t spo2 = (uint8_t)pox.getSpO2();

            long distance = readDistanceCm();

            // transformăm în flag (0 / 1)
            int distFlag = 0;
            if (distance > 0 && distance < 30) { // sub 30 cm = aproape
                distFlag = 1;
            }

            Serial.print("DATA,");
            Serial.print(ecgDr); Serial.print(",");
            Serial.print(ecgSt); Serial.print(",");
            Serial.print(hr); Serial.print(",");
            Serial.print(spo2); Serial.print(",");
            Serial.println(distFlag);
        }
    }
}