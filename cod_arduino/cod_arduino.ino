#include <Wire.h>
#include <string.h>
#include "MAX30105.h"
#include "spo2_algorithm.h"

// -------------------- CONFIGURARE HARDWARE --------------------

#define ECG_PRIMARY_PIN     A0
#define ECG_SECONDARY_PIN   A1

// 0 = un singur canal ECG pe A0
// 1 = două canale independente pe A0 și A1
#define USE_TWO_ECG_CHANNELS 0

#define FINGER_IR_THRESHOLD 10000UL

#define SPO2_BUFFER_SIZE 50
#define SPO2_RECALCULATE_SAMPLES 12

#define DATA_INTERVAL_MS 10

// -------------------- FILTRARE PULSOXIMETRU --------------------

// Ponderea noii măsurători.
// 25% valoare nouă + 75% valoare anterioară.
#define FILTER_NEW_WEIGHT 1
#define FILTER_OLD_WEIGHT 3

// Păstrăm ultima valoare validă maximum 2 secunde.
#define OXIMETER_HOLD_MS 2000UL

// -------------------- OBIECTE SENZORI --------------------

MAX30105 maxSensor;
bool maxSensorReady = false;

#if defined(__AVR_ATmega328P__) || defined(__AVR_ATmega168__)
typedef uint16_t MaxSample;
#else
typedef uint32_t MaxSample;
#endif

MaxSample irBuffer[SPO2_BUFFER_SIZE];
MaxSample redBuffer[SPO2_BUFFER_SIZE];

uint8_t maxBufferCount = 0;
uint8_t samplesSinceCalculation = 0;

int32_t heartRate = -1;
int32_t spo2 = -1;

int8_t validHeartRate = 0;
int8_t validSpO2 = 0;

uint32_t lastValidHeartRateTime = 0;
uint32_t lastValidSpO2Time = 0;

bool isCollecting = false;
uint32_t lastDataTime = 0;

// -------------------- COMENZI SERIALE --------------------

char commandBuffer[32];
uint8_t commandLength = 0;

// -------------------- RESET PULSOXIMETRU --------------------

void clearOximeterMeasurements() {
    maxBufferCount = 0;
    samplesSinceCalculation = 0;

    heartRate = -1;
    spo2 = -1;

    validHeartRate = 0;
    validSpO2 = 0;

    lastValidHeartRateTime = 0;
    lastValidSpO2Time = 0;
}

// -------------------- FILTRARE HR ȘI SPO2 --------------------

void calculateOximeterMeasurements() {

    int32_t calculatedHeartRate = -1;
    int32_t calculatedSpO2 = -1;

    int8_t calculatedValidHeartRate = 0;
    int8_t calculatedValidSpO2 = 0;

    maxim_heart_rate_and_oxygen_saturation(
        irBuffer,
        SPO2_BUFFER_SIZE,
        redBuffer,
        &calculatedSpO2,
        &calculatedValidSpO2,
        &calculatedHeartRate,
        &calculatedValidHeartRate
    );

    uint32_t now = millis();

    // ---------------- HR ----------------

    if (calculatedValidHeartRate &&
        calculatedHeartRate >= 30 &&
        calculatedHeartRate <= 220) {

        bool hasRecentHeartRate =
            validHeartRate &&
            (uint32_t)(now - lastValidHeartRateTime)
                <= OXIMETER_HOLD_MS;

        if (hasRecentHeartRate) {
            heartRate =
                (heartRate * FILTER_OLD_WEIGHT +
                 calculatedHeartRate * FILTER_NEW_WEIGHT + 2)
                / (FILTER_OLD_WEIGHT + FILTER_NEW_WEIGHT);
        }
        else {
            // Prima valoare sau valoare după o pauză.
            heartRate = calculatedHeartRate;
        }

        validHeartRate = 1;
        lastValidHeartRateTime = now;
    }

    // ---------------- SPO2 ----------------

    if (calculatedValidSpO2 &&
        calculatedSpO2 >= 70 &&
        calculatedSpO2 <= 100) {

        bool hasRecentSpO2 =
            validSpO2 &&
            (uint32_t)(now - lastValidSpO2Time)
                <= OXIMETER_HOLD_MS;

        if (hasRecentSpO2) {
            spo2 =
                (spo2 * FILTER_OLD_WEIGHT +
                 calculatedSpO2 * FILTER_NEW_WEIGHT + 2)
                / (FILTER_OLD_WEIGHT + FILTER_NEW_WEIGHT);
        }
        else {
            spo2 = calculatedSpO2;
        }

        validSpO2 = 1;
        lastValidSpO2Time = now;
    }
}

// -------------------- ADAUGARE MOSTRĂ MAX30102 --------------------

void addOximeterSample(uint32_t ir, uint32_t red) {

    // Dacă nu există deget pe senzor, resetăm măsurătorile.
    if (ir < FINGER_IR_THRESHOLD) {
        clearOximeterMeasurements();
        return;
    }

    if (maxBufferCount < SPO2_BUFFER_SIZE) {

        irBuffer[maxBufferCount] = (MaxSample)ir;
        redBuffer[maxBufferCount] = (MaxSample)red;

        maxBufferCount++;

        if (maxBufferCount == SPO2_BUFFER_SIZE) {
            calculateOximeterMeasurements();
            samplesSinceCalculation = 0;
        }

        return;
    }

    // Deplasare buffer.
    for (uint8_t i = 0; i < SPO2_BUFFER_SIZE - 1; i++) {
        irBuffer[i] = irBuffer[i + 1];
        redBuffer[i] = redBuffer[i + 1];
    }

    irBuffer[SPO2_BUFFER_SIZE - 1] = (MaxSample)ir;
    redBuffer[SPO2_BUFFER_SIZE - 1] = (MaxSample)red;

    samplesSinceCalculation++;

    if (samplesSinceCalculation >= SPO2_RECALCULATE_SAMPLES) {
        samplesSinceCalculation = 0;
        calculateOximeterMeasurements();
    }
}

// -------------------- CITIRE MAX30102 --------------------

void serviceMAX30102() {

    if (!maxSensorReady)
        return;

    maxSensor.check();

    while (maxSensor.available()) {

        uint32_t ir = maxSensor.getIR();
        uint32_t red = maxSensor.getRed();

        maxSensor.nextSample();

        addOximeterSample(ir, red);
    }
}

// -------------------- COMENZI SERIAL --------------------

void processCommand(const char *command) {

    if (strcmp(command, "START_TEST") == 0) {

        isCollecting = true;
        lastDataTime = millis();

        Serial.println(F("TEST_STARTED"));
    }
    else if (strcmp(command, "STOP_TEST") == 0) {

        isCollecting = false;

        Serial.println(F("TEST_STOPPED"));
    }
}

void serviceSerialCommands() {

    while (Serial.available() > 0) {

        char c = (char)Serial.read();

        if (c == '\n' || c == '\r') {

            if (commandLength > 0) {

                commandBuffer[commandLength] = '\0';

                processCommand(commandBuffer);

                commandLength = 0;
            }
        }
        else if (commandLength < sizeof(commandBuffer) - 1) {

            commandBuffer[commandLength++] = c;
        }
    }
}

// -------------------- TRANSMITERE DATE --------------------

void sendDataFrame() {

    uint32_t now = millis();

    if (!isCollecting ||
        now - lastDataTime < DATA_INTERVAL_MS) {
        return;
    }

    lastDataTime = now;

    // ECG.
    int ecgDr = analogRead(ECG_PRIMARY_PIN);
    int ecgSt;

#if USE_TWO_ECG_CHANNELS
    ecgSt = analogRead(ECG_SECONDARY_PIN);
#else
    ecgSt = ecgDr;
#endif

    // HR și SpO2.
    // Păstrăm ultima măsurătoare validă maximum 2 secunde.
    int32_t outputHeartRate = -1;
    int32_t outputSpO2 = -1;

    if (validHeartRate &&
        (uint32_t)(now - lastValidHeartRateTime)
            <= OXIMETER_HOLD_MS) {

        outputHeartRate = heartRate;
    }

    if (validSpO2 &&
        (uint32_t)(now - lastValidSpO2Time)
            <= OXIMETER_HOLD_MS) {

        outputSpO2 = spo2;
    }

    // Format:
    // DATA,ECG_DREAPTA,ECG_STANGA,HR,SPO2,DISTANCE

    Serial.print(F("DATA,"));

    Serial.print(ecgDr);
    Serial.print(',');

    Serial.print(ecgSt);
    Serial.print(',');

    Serial.print(outputHeartRate);
    Serial.print(',');

    Serial.print(outputSpO2);

    // Distanța este permanent 1.
    Serial.println(F(",1"));
}

// -------------------- SETUP --------------------

void setup() {

    Serial.begin(115200);

    Wire.begin();
    Wire.setClock(400000UL);

    maxSensorReady = maxSensor.begin(
        Wire,
        I2C_SPEED_FAST
    );

    if (maxSensorReady) {

        maxSensor.setup(
            0x3F,
            4,
            2,
            100,
            411,
            4096
        );

        maxSensor.setPulseAmplitudeRed(0x1F);
        maxSensor.setPulseAmplitudeIR(0x1F);
        maxSensor.setPulseAmplitudeGreen(0);

    }
    else {

        Serial.println(F("MAX30102_ERROR,NOT_FOUND"));
    }

    Serial.println(F("READY"));
}

// -------------------- LOOP --------------------

void loop() {

    serviceSerialCommands();

    serviceMAX30102();

    sendDataFrame();
}