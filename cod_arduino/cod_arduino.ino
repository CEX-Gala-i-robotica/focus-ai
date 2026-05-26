#include <Wire.h>
#include <Adafruit_PN532.h>

// ===== SETĂRI NFC (Conectat pe 4 fire: SDA=20, SCL=21) =====
#define PN532_IRQ   (2) // Ignorat fizic de Mega în 4 fire
#define PN532_RESET (3) // Ignorat fizic de Mega în 4 fire
Adafruit_PN532 nfc(PN532_IRQ, PN532_RESET);

// ===== SETĂRI BUTOANE =====
const int buttonPins[] = {9, 10, 11, 12};
const int numButtons = 4;
bool lastButtonStates[] = {HIGH, HIGH, HIGH, HIGH}; 
uint32_t lastDebounceTime = 0;

// ===== VARIABILE DE CONTROL (Comunicare C#) =====
bool isCollecting = false;
uint32_t tsLastReport = 0;
#define REPORTING_PERIOD_MS 1000 // Trimite date o dată pe secundă

void setup() {
    Serial.begin(115200);
    while (!Serial) delay(10);

    // 1. Inițializare Butoane
    for (int i = 0; i < numButtons; i++) {
        pinMode(buttonPins[i], INPUT_PULLUP);
    }

    // 2. Inițializare NFC
    nfc.begin();
    uint32_t versiondata = nfc.getFirmwareVersion();
    if (versiondata) {
        nfc.SAMConfig(); // Setăm NFC-ul să citească tag-uri
    }

    // Spunem aplicației C# că Arduino e gata
    Serial.println("READY");
}

void loop() {
    // ==========================================
    // 1. ASCULTĂM COMENZI DIN APLICAȚIA C#
    // ==========================================
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
    }

    // ==========================================
    // 2. VERIFICARE BUTOANE (Hardware Real)
    // ==========================================
    bool anyButtonPressedNow = false;
    
    for (int i = 0; i < numButtons; i++) {
        bool currentState = digitalRead(buttonPins[i]);
        
        // Verificăm dacă butonul tocmai a fost apăsat (tranziție de la HIGH la LOW)
        if (currentState == LOW && lastButtonStates[i] == HIGH) {
            anyButtonPressedNow = true;
        }
        lastButtonStates[i] = currentState; // Salvăm starea pentru tura viitoare
    }

    // Trimitem semnalul de TOUCH către C# o singură dată per apăsare
    if (anyButtonPressedNow) {
        if (millis() - lastDebounceTime > 50) {
            lastDebounceTime = millis();
            Serial.println("TOUCH_DETECTED"); 
        }
    }

    // ==========================================
    // 3. VERIFICARE NFC (Hardware Real)
    // ==========================================
    uint8_t uid[] = { 0, 0, 0, 0, 0, 0, 0 };  
    uint8_t uidLength;                        
    
    // Citim cardul cu un timeout foarte scurt (50ms) ca să nu blocăm restul codului
    if (nfc.readPassiveTargetID(PN532_MIFARE_ISO14443A, uid, &uidLength, 50)) {
        Serial.print("NFC_DETECTED,");
        for (uint8_t i = 0; i < uidLength; i++) {
            if (uid[i] <= 0x0F) Serial.print("0"); 
            Serial.print(uid[i], HEX);
        }
        Serial.println();
        
        delay(500); // Pauză antiblocaj pentru același card
    }

    // ==========================================
    // 4. TRIMITERE DATE CĂTRE C# (Fără senzor distanță)
    // ==========================================
    if (isCollecting) {
        uint32_t now = millis();

        if (now - tsLastReport >= REPORTING_PERIOD_MS) {
            tsLastReport = now;

            // Generăm date medicale fake
            int fakeEcgDr = random(400, 600);
            int fakeEcgSt = random(400, 600);
            uint8_t fakeHr = random(65, 85);    
            uint8_t fakeSpo2 = random(96, 100); 
            
            // Trimitem 1 constant ca aplicația să creadă că pacientul e mereu la aparat
            int fakeDistFlag = 1; 

            // Formatul trimis: DATA,ecgDr,ecgSt,hr,spo2,distFlag
            Serial.print("DATA,");
            Serial.print(fakeEcgDr); Serial.print(",");
            Serial.print(fakeEcgSt); Serial.print(",");
            Serial.print(fakeHr); Serial.print(",");
            Serial.print(fakeSpo2); Serial.print(",");
            Serial.println(fakeDistFlag);
        }
    }
}