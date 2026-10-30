
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace focus_ai
{
    public readonly struct BioDataFrame
    {
        public BioDataFrame(
            int ecgDreapta,
            int ecgStanga,
            int heartRate,
            int spo2,
            bool distance)
        {
            EcgDreapta = ecgDreapta;
            EcgStanga = ecgStanga;
            HeartRate = heartRate;
            SpO2 = spo2;
            Distance = distance;
        }

        public int EcgDreapta { get; }
        public int EcgStanga { get; }
        public int HeartRate { get; }
        public int SpO2 { get; }
        public bool Distance { get; }
    }

    public struct EcgSample
    {
        public int EcgDreapta { get; set; }
        public int EcgStanga { get; set; }
    }

    public sealed class BioCollector
    {
        private static BioCollector? _instance;

        public static BioCollector Instance =>
            _instance ??= new BioCollector();

        private const string ArduinoPortName = "COM5";
        private const int ArduinoBaudRate = 115200;

        public List<EcgSample> Ecg { get; } = new();
        public List<int> HeartRate { get; } = new();
        public List<int> SpO2 { get; } = new();
        public List<bool> Distance { get; } = new();

        public int LiveHr { get; private set; }
        public int LiveSpo2 { get; private set; }
        public int LiveEcgDr { get; private set; }
        public int LiveEcgSt { get; private set; }

        public bool LiveDist { get; private set; } = true;

        public string LastNfcUid { get; private set; } = "";
        public string ConnectedPortName => _serial?.PortName ?? "";
        public string LastDetectedPortName { get; private set; } = "";

        public event Action<int, int, int, int, bool>? SampleReceived;
        public event Action<string>? NfcUidReceived;

        private SerialPort? _serial;
        private bool _streaming;

        private readonly Dispatcher _ui =
            Application.Current.Dispatcher;

        private BioCollector()
        {
        }

        // Deschide exclusiv COM5.
        public bool TryOpen()
        {
            if (_serial?.IsOpen == true)
                return true;

            if (TryOpenPort(ArduinoPortName, out Exception? error))
                return true;

            MessageBox.Show(
                $"Nu pot conecta Arduino pe {ArduinoPortName}.\n" +
                $"{error?.Message ?? "Portul serial nu este disponibil."}\n\n" +
                "Verifică dacă Arduino este conectat la COM5.",
                "Focus AI - Serial",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            _serial = null;
            return false;
        }

        public void RememberDetectedPort(string portName)
        {
            if (!string.IsNullOrWhiteSpace(portName))
                LastDetectedPortName = portName;
        }

        private bool TryOpenPort(string portName, out Exception? error)
        {
            error = null;
            SerialPort? serial = null;

            try
            {
                serial = new SerialPort(portName, ArduinoBaudRate)
                {
                    ReadTimeout = 250,
                    WriteTimeout = 500,
                    NewLine = "\n",
                    DtrEnable = true,
                    RtsEnable = true
                };

                serial.Open();

                // Arduino se poate reseta la deschiderea portului.
                Thread.Sleep(2500);

                if (!WaitForArduinoReady(
                    serial,
                    TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException(
                        $"{portName} nu a transmis READY, DATA sau NFC.");
                }

                serial.DiscardInBuffer();
                serial.DataReceived += OnDataReceived;

                _serial = serial;

                return true;
            }
            catch (Exception ex)
            {
                error = ex;

                try
                {
                    if (serial?.IsOpen == true)
                        serial.Close();
                }
                catch
                {
                }

                try
                {
                    serial?.Dispose();
                }
                catch
                {
                }

                _serial = null;
                return false;
            }
        }

        public void Close()
        {
            try
            {
                Send("STOP_TEST");
            }
            catch
            {
            }

            try
            {
                if (_serial != null)
                {
                    _serial.DataReceived -= OnDataReceived;

                    if (_serial.IsOpen)
                        _serial.Close();

                    _serial.Dispose();
                }
            }
            catch
            {
            }

            _serial = null;
            _streaming = false;
        }

        public void StartStreaming(bool reset = false)
        {
            if (reset)
            {
                Ecg.Clear();
                HeartRate.Clear();
                SpO2.Clear();
                Distance.Clear();
            }

            _streaming = true;
            Send("START_TEST");
        }

        public void StopStreaming()
        {
            _streaming = false;
            Send("STOP_TEST");
        }

        public void Send(string cmd)
        {
            try
            {
                if (_serial?.IsOpen == true)
                    _serial.WriteLine(cmd);
            }
            catch
            {
            }
        }

        public void ClearLastNfcUid()
        {
            LastNfcUid = "";
        }

        private void OnDataReceived(
            object sender,
            SerialDataReceivedEventArgs e)
        {
            try
            {
                SerialPort? serial = _serial;

                if (serial == null || !serial.IsOpen)
                    return;

                while (serial.BytesToRead > 0)
                {
                    string line = serial.ReadLine().Trim();

                    _ui.InvokeAsync(() => HandleLine(line));
                }
            }
            catch
            {
            }
        }

        private void HandleLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            if (line == "READY" || line == "TEST_STARTED")
                return;

            // Citire NFC.
            string nfcUid = ExtractNfcUid(line);

            if (!string.IsNullOrWhiteSpace(nfcUid))
            {
                LastNfcUid = nfcUid;
                NfcUidReceived?.Invoke(nfcUid);
                return;
            }

            // Citire date senzori.
            if (line.StartsWith(
                "DATA,",
                StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseDataFrame(line, out var frame))
                    return;

                LiveEcgDr = frame.EcgDreapta;
                LiveEcgSt = frame.EcgStanga;
                LiveHr = frame.HeartRate;
                LiveSpo2 = frame.SpO2;

                // Distanța este permanent 1.
                LiveDist = true;

                if (_streaming)
                {
                    Ecg.Add(new EcgSample
                    {
                        EcgDreapta = frame.EcgDreapta,
                        EcgStanga = frame.EcgStanga
                    });

                    HeartRate.Add(frame.HeartRate);
                    SpO2.Add(frame.SpO2);
                    Distance.Add(true);
                }

                SampleReceived?.Invoke(
                    frame.EcgDreapta,
                    frame.EcgStanga,
                    frame.HeartRate,
                    frame.SpO2,
                    true);
            }
        }

        public static bool TryParseDataFrame(
            string line,
            out BioDataFrame frame)
        {
            frame = default;

            if (string.IsNullOrWhiteSpace(line) ||
                !line.StartsWith(
                    "DATA,",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] parts = line.Split(',');

            if (parts.Length < 6)
                return false;

            if (!TryParseOptionalInt(parts[1], out int ecgDr))
                return false;

            if (!TryParseOptionalInt(parts[2], out int ecgSt))
                return false;

            if (!TryParseOptionalInt(parts[3], out int hr))
                return false;

            if (!TryParseOptionalInt(parts[4], out int spo2))
                return false;

            // Ignorăm valoarea distanței primită de la Arduino.
            // În aplicație aceasta este permanent TRUE.
            frame = new BioDataFrame(
                ecgDr,
                ecgSt,
                hr,
                spo2,
                true);

            return true;
        }

        public static string ExtractNfcUid(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return "";

            const string nfcMarker = "NFC_DETECTED,";

            if (line.StartsWith(
                nfcMarker,
                StringComparison.OrdinalIgnoreCase))
            {
                return line[nfcMarker.Length..].Trim();
            }

            const string uidMarker = "UID:";

            int markerIndex = line.IndexOf(
                uidMarker,
                StringComparison.OrdinalIgnoreCase);

            if (markerIndex < 0)
                return "";

            return line[(markerIndex + uidMarker.Length)..].Trim();
        }

        private static bool TryParseOptionalInt(
            string value,
            out int result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result = 0;
                return true;
            }

            return int.TryParse(value.Trim(), out result);
        }

        private static bool WaitForArduinoReady(
            SerialPort serial,
            TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    string line = serial.ReadLine().Trim();

                    if (IsSupportedArduinoLine(line))
                        return true;
                }
                catch (TimeoutException)
                {
                }
            }

            return false;
        }

        private static bool IsSupportedArduinoLine(string line)
        {
            return line.Equals(
                       "READY",
                       StringComparison.OrdinalIgnoreCase)
                || line.Equals(
                       "TEST_STARTED",
                       StringComparison.OrdinalIgnoreCase)
                || line.StartsWith(
                       "DATA,",
                       StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(ExtractNfcUid(line));
        }
    }
}
