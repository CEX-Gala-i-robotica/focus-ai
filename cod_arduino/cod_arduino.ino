#include <Wire.h>
#include <math.h>
#include <string.h>
#include "MAX30105.h"
#include "spo2_algorithm.h"
#include "heartRate.h"

// Focus AI - Arduino Uno R3 firmware
// Protocol v2:
//   PING,FOCUS_AI                    -> PONG,FOCUS_AI,2
//   START_TEST / STOP_TEST           -> TEST_STARTED / TEST_STOPPED
//   BEEP                             -> BEEP_STARTED,millis
//   TOUCH_DETECTED,millis            (debounced rising edge)
//   DATA,millis,ANALOG_A0,ANALOG_A1,HR,SpO2,FINGER,PPG_READY,DISTANCE_CM,DISTANCE_UPDATED
//   PPG_RR,millis,interval_ms,IR_amplitude (PPG-derived PRV, not ECG RR)
// ANALOG_A0 is the raw MyoWare analog output. It is not labelled as clinical
// ECG by this firmware. ANALOG_A1 is -1 unless a second independent channel is wired.
// MAX30102 produces PPG pulse intervals (PRV), not ECG-derived RR intervals.

#define MYOWARE_OUTPUT_PIN        A0
#define SECONDARY_ANALOG_PIN      A1
#define USE_SECONDARY_ANALOG_CHANNEL 0
#define TOUCH_PIN                 2
#define BUZZER_PIN                3
#define DISTANCE_TRIG_PIN         4
#define DISTANCE_ECHO_PIN         5

#define FINGER_IR_THRESHOLD       10000UL
#define TOUCH_DEBOUNCE_MS         45UL
#define DISTANCE_INTERVAL_MS      250UL
#define DISTANCE_PULSE_TIMEOUT_US 6000UL

// PPG beat detection runs at approximately 100 samples/s (400 Hz sensor
// rate divided by hardware averaging=4). For SpO2, four consecutive samples
// are averaged in software to 25 samples/s, matching Maxim's 100-sample/4s
// algorithm window.
#define PPG_SAMPLE_INTERVAL_US   10000UL
#define SPO2_DECIMATION_FACTOR   4
#define SPO2_BUFFER_SIZE         100
#define SPO2_RECALCULATE_SAMPLES 25
#define DATA_INTERVAL_MS         10UL

#define FILTER_NEW_WEIGHT        1
#define FILTER_OLD_WEIGHT        3
#define OXIMETER_HOLD_MS         2000UL

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
uint8_t spo2DecimationCount = 0;
uint32_t spo2IrAccumulator = 0;
uint32_t spo2RedAccumulator = 0;
uint32_t ppgSampleTimestampMicros = 0;
uint32_t ppgSampleTimestampMillis = 0;
bool ppgSampleClockInitialized = false;

int32_t heartRateBpm = -1;
int32_t spo2Percent = -1;
int8_t validHeartRate = 0;
int8_t validSpO2 = 0;
uint32_t lastValidHeartRateTime = 0;
uint32_t lastValidSpO2Time = 0;

bool isCollecting = false;
uint32_t lastDataTime = 0;
uint32_t lastBeatTimeMicros = 0;

bool touchStableState = LOW;
bool touchLastReading = LOW;
uint32_t touchLastChangeTime = 0;

int distanceCm = -1;
bool distanceAvailable = false;
bool distanceUpdated = false;
uint32_t lastDistanceTime = 0;

char commandBuffer[32];
uint8_t commandLength = 0;

void clearOximeterMeasurements() {
    maxBufferCount = 0;
    samplesSinceCalculation = 0;
    spo2DecimationCount = 0;
    spo2IrAccumulator = 0;
    spo2RedAccumulator = 0;
    heartRateBpm = -1;
    spo2Percent = -1;
    validHeartRate = 0;
    validSpO2 = 0;
    lastValidHeartRateTime = 0;
    lastValidSpO2Time = 0;
    lastBeatTimeMicros = 0;
}

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

    const uint32_t now = millis();

    if (calculatedValidHeartRate && calculatedHeartRate >= 30 && calculatedHeartRate <= 220) {
        const bool hasRecent = validHeartRate && (uint32_t)(now - lastValidHeartRateTime) <= OXIMETER_HOLD_MS;
        heartRateBpm = hasRecent
            ? (heartRateBpm * FILTER_OLD_WEIGHT + calculatedHeartRate * FILTER_NEW_WEIGHT + 2) /
              (FILTER_OLD_WEIGHT + FILTER_NEW_WEIGHT)
            : calculatedHeartRate;
        validHeartRate = 1;
        lastValidHeartRateTime = now;
    }

    if (calculatedValidSpO2 && calculatedSpO2 >= 70 && calculatedSpO2 <= 100) {
        const bool hasRecent = validSpO2 && (uint32_t)(now - lastValidSpO2Time) <= OXIMETER_HOLD_MS;
        spo2Percent = hasRecent
            ? (spo2Percent * FILTER_OLD_WEIGHT + calculatedSpO2 * FILTER_NEW_WEIGHT + 2) /
              (FILTER_OLD_WEIGHT + FILTER_NEW_WEIGHT)
            : calculatedSpO2;
        validSpO2 = 1;
        lastValidSpO2Time = now;
    }
}

void emitPpgInterval(uint32_t beatMicros, uint32_t beatMillis, uint32_t irAmplitude) {
    if (lastBeatTimeMicros != 0) {
        const uint32_t intervalMicros = beatMicros - lastBeatTimeMicros;
        const float intervalMs = intervalMicros / 1000.0f;

        // Physiological plausibility gate: 30-200 BPM. The sample is still
        // identified as PPG-derived PRV by the PPG_RR protocol tag.
        if (isCollecting && intervalMs >= 300.0f && intervalMs <= 2000.0f) {
            Serial.print(F("PPG_RR,"));
            Serial.print(beatMillis);
            Serial.print(',');
            Serial.print(intervalMs, 3);
            Serial.print(',');
            Serial.println(irAmplitude);
        }
    }
    lastBeatTimeMicros = beatMicros;
}

void addOximeterSample(uint32_t ir, uint32_t red) {
    // Establish a sample clock from the first observed FIFO item, then use
    // the configured 100 Hz period. This avoids interval jitter when the loop
    // drains more than one buffered FIFO sample after a short blocking call.
    if (!ppgSampleClockInitialized) {
        ppgSampleTimestampMicros = micros();
        ppgSampleTimestampMillis = millis();
        ppgSampleClockInitialized = true;
    } else {
        ppgSampleTimestampMicros += PPG_SAMPLE_INTERVAL_US;
        ppgSampleTimestampMillis += (PPG_SAMPLE_INTERVAL_US / 1000UL);
    }

    if (ir < FINGER_IR_THRESHOLD) {
        clearOximeterMeasurements();
        return;
    }

    // Beat detection is run at approximately 100 Hz for better timing
    // resolution. Intervals are PPG-derived PRV, not ECG RR intervals.
    if (checkForBeat((int32_t)ir)) {
        emitPpgInterval(ppgSampleTimestampMicros, ppgSampleTimestampMillis, ir);
    }

    // Maxim's SpO2 implementation expects 25 Hz. Low-pass by averaging four
    // consecutive 100 Hz FIFO samples before filling its 4-second window.
    spo2IrAccumulator += ir;
    spo2RedAccumulator += red;
    spo2DecimationCount++;
    if (spo2DecimationCount < SPO2_DECIMATION_FACTOR)
        return;

    const uint32_t ir25 = (spo2IrAccumulator + 2UL) / SPO2_DECIMATION_FACTOR;
    const uint32_t red25 = (spo2RedAccumulator + 2UL) / SPO2_DECIMATION_FACTOR;
    spo2IrAccumulator = 0;
    spo2RedAccumulator = 0;
    spo2DecimationCount = 0;

    if (maxBufferCount < SPO2_BUFFER_SIZE) {
        irBuffer[maxBufferCount] = (MaxSample)ir25;
        redBuffer[maxBufferCount] = (MaxSample)red25;
        maxBufferCount++;

        if (maxBufferCount == SPO2_BUFFER_SIZE) {
            calculateOximeterMeasurements();
            samplesSinceCalculation = 0;
        }
        return;
    }

    for (uint8_t i = 0; i < SPO2_BUFFER_SIZE - 1; i++) {
        irBuffer[i] = irBuffer[i + 1];
        redBuffer[i] = redBuffer[i + 1];
    }

    irBuffer[SPO2_BUFFER_SIZE - 1] = (MaxSample)ir25;
    redBuffer[SPO2_BUFFER_SIZE - 1] = (MaxSample)red25;

    samplesSinceCalculation++;
    if (samplesSinceCalculation >= SPO2_RECALCULATE_SAMPLES) {
        samplesSinceCalculation = 0;
        calculateOximeterMeasurements();
    }
}

void serviceMAX30102() {
    if (!maxSensorReady)
        return;

    maxSensor.check();
    while (maxSensor.available()) {
        const uint32_t ir = maxSensor.getIR();
        const uint32_t red = maxSensor.getRed();
        maxSensor.nextSample();
        addOximeterSample(ir, red);
    }
}

void processCommand(const char *command) {
    if (strcmp(command, "PING,FOCUS_AI") == 0) {
        Serial.println(F("PONG,FOCUS_AI,2"));
    }
    else if (strcmp(command, "START_TEST") == 0) {
        isCollecting = true;
        lastDataTime = millis();
        lastBeatTimeMicros = 0; // discard an interval that began before the test
        Serial.println(F("TEST_STARTED"));
    }
    else if (strcmp(command, "STOP_TEST") == 0) {
        isCollecting = false;
        Serial.println(F("TEST_STOPPED"));
    }
    else if (strcmp(command, "BEEP") == 0) {
        tone(BUZZER_PIN, 2000, 150);
        Serial.print(F("BEEP_STARTED,"));
        Serial.println(millis());
    }
}

void serviceSerialCommands() {
    while (Serial.available() > 0) {
        const char c = (char)Serial.read();
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

void serviceTouchSensor() {
    const uint32_t now = millis();
    const bool reading = digitalRead(TOUCH_PIN) == HIGH;

    if (reading != touchLastReading) {
        touchLastReading = reading;
        touchLastChangeTime = now;
    }

    if ((uint32_t)(now - touchLastChangeTime) >= TOUCH_DEBOUNCE_MS &&
        reading != touchStableState) {
        touchStableState = reading;
        if (touchStableState && isCollecting) {
            // Report the first candidate rising-edge time after it has passed
            // debounce, so reaction time does not include the 45 ms debounce.
            Serial.print(F("TOUCH_DETECTED,"));
            Serial.println(touchLastChangeTime);
        }
    }
}

void serviceDistanceSensor() {
    const uint32_t now = millis();
    if (!isCollecting || (uint32_t)(now - lastDistanceTime) < DISTANCE_INTERVAL_MS)
        return;

    lastDistanceTime = now;
    digitalWrite(DISTANCE_TRIG_PIN, LOW);
    delayMicroseconds(2);
    digitalWrite(DISTANCE_TRIG_PIN, HIGH);
    delayMicroseconds(10);
    digitalWrite(DISTANCE_TRIG_PIN, LOW);

    const unsigned long duration = pulseIn(DISTANCE_ECHO_PIN, HIGH, DISTANCE_PULSE_TIMEOUT_US);
    if (duration == 0) {
        distanceCm = -1;
        distanceAvailable = false;
    }
    else {
        const long measured = lroundf(duration * 0.0343f / 2.0f);
        if (measured >= 2 && measured <= 400) {
            distanceCm = (int)measured;
            distanceAvailable = true;
        }
        else {
            distanceCm = -1;
            distanceAvailable = false;
        }
    }
    distanceUpdated = true;
}

void sendDataFrame() {
    const uint32_t now = millis();
    if (!isCollecting || (uint32_t)(now - lastDataTime) < DATA_INTERVAL_MS)
        return;
    lastDataTime = now;

    serviceDistanceSensor();

    const int analogA0 = analogRead(MYOWARE_OUTPUT_PIN);
    int analogA1 = -1;
#if USE_SECONDARY_ANALOG_CHANNEL
    analogA1 = analogRead(SECONDARY_ANALOG_PIN);
#endif

    int32_t outputHeartRate = -1;
    int32_t outputSpO2 = -1;

    if (validHeartRate && (uint32_t)(now - lastValidHeartRateTime) <= OXIMETER_HOLD_MS)
        outputHeartRate = heartRateBpm;
    if (validSpO2 && (uint32_t)(now - lastValidSpO2Time) <= OXIMETER_HOLD_MS)
        outputSpO2 = spo2Percent;

    const bool finger = maxSensorReady && maxSensor.getIR() >= FINGER_IR_THRESHOLD;

    Serial.print(F("DATA,"));
    Serial.print(now);
    Serial.print(',');
    Serial.print(analogA0);
    Serial.print(',');
    Serial.print(analogA1);
    Serial.print(',');
    Serial.print(outputHeartRate);
    Serial.print(',');
    Serial.print(outputSpO2);
    Serial.print(',');
    Serial.print(finger ? 1 : 0);
    Serial.print(',');
    Serial.print(maxSensorReady ? 1 : 0);
    Serial.print(',');
    Serial.print(distanceAvailable ? distanceCm : -1);
    Serial.print(',');
    Serial.println(distanceUpdated ? 1 : 0);
    distanceUpdated = false;
}

void setup() {
    Serial.begin(115200);
    pinMode(TOUCH_PIN, INPUT);
    pinMode(BUZZER_PIN, OUTPUT);
    pinMode(DISTANCE_TRIG_PIN, OUTPUT);
    pinMode(DISTANCE_ECHO_PIN, INPUT);
    digitalWrite(DISTANCE_TRIG_PIN, LOW);
    touchStableState = digitalRead(TOUCH_PIN) == HIGH;
    touchLastReading = touchStableState;
    Wire.begin();
    Wire.setClock(400000UL);

    maxSensorReady = maxSensor.begin(Wire, I2C_SPEED_FAST);
    if (maxSensorReady) {
        // sampleAverage=4, sampleRate=400 Hz -> about 100 FIFO samples/sec.
        // Software averaging decimates this to 25 Hz for SpO2 only.
        maxSensor.setup(0x3F, 4, 2, 400, 411, 4096);
        maxSensor.setPulseAmplitudeRed(0x1F);
        maxSensor.setPulseAmplitudeIR(0x1F);
        maxSensor.setPulseAmplitudeGreen(0);
    }

    Serial.println(F("READY,FOCUS_AI,2"));
}

void loop() {
    serviceSerialCommands();
    serviceTouchSensor();
    serviceMAX30102();
    sendDataFrame();
}
