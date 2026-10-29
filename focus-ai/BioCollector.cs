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
        public BioDataFrame(int ecgDreapta, int ecgStanga, int heartRate, int spo2, bool distance)
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
        public static BioCollector Instance => _instance ??= new BioCollector();

        public List<EcgSample> Ecg { get; } = new();
        public List<int> HeartRate { get; } = new();
        public List<int> SpO2 { get; } = new();
        public List<bool> Distance { get; } = new();

        public int LiveHr { get; private set; }
        public int LiveSpo2 { get; private set; }
        public int LiveEcgDr { get; private set; }
        public int LiveEcgSt { get; private set; }
        public bool LiveDist { get; private set; }
        public string LastNfcUid { get; private set; } = "";
        public string ConnectedPortName => _serial?.PortName ?? "";
        public string LastDetectedPortName { get; private set; } = "";

        public event Action<int, int, int, int, bool>? SampleReceived;
        public event Action? TouchDetected;
        public event Action<string>? NfcUidReceived;

        private SerialPort? _serial;
        private bool _streaming;
        private readonly Dispatcher _ui = Application.Current.Dispatcher;
        private const int ArduinoBaudRate = 115200;

        private BioCollector() { }

        public bool TryOpen(string? preferredPortName = null)
        {
            if (_serial?.IsOpen == true) return true;

            string[] availablePorts = SerialPort.GetPortNames();
            string[] candidatePorts = availablePorts
                .Prepend(preferredPortName ?? "")
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Exception? lastError = null;

            if (!string.IsNullOrWhiteSpace(preferredPortName)
                && availablePorts.Contains(preferredPortName, StringComparer.OrdinalIgnoreCase)
                && TryOpenPort(preferredPortName, requireSupportedLine: false, out lastError))
            {
                return true;
            }

            foreach (string candidatePort in candidatePorts)
            {
                if (TryOpenPort(candidatePort, requireSupportedLine: true, out lastError))
                    return true;
            }

            MessageBox.Show(
                $"Cannot find the Arduino automatically.\n{lastError?.Message ?? "No serial ports were available."}\n\nTests will run without hardware collection.",
                "Focus AI – Serial",
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

        private bool TryOpenPort(string portName, bool requireSupportedLine, out Exception? error)
        {
            error = null;

            try
            {
                var serial = new SerialPort(portName, ArduinoBaudRate)
                {
                    ReadTimeout = 250,
                    WriteTimeout = 500,
                    NewLine = "\n",
                    DtrEnable = true,
                    RtsEnable = true
                };

                serial.Open();
                Thread.Sleep(2500);

                if (requireSupportedLine && !WaitForArduinoReady(serial, TimeSpan.FromSeconds(5)))
                {
                    serial.Close();
                    serial.Dispose();
                    error = new TimeoutException($"{portName} did not answer with READY, DATA, NFC, or TOUCH.");
                    return false;
                }

                serial.DiscardInBuffer();
                serial.DataReceived += OnDataReceived;
                _serial = serial;
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                _serial = null;
                return false;
            }
        }

        public void Close()
        {
            try { Send("STOP_TEST"); } catch { }

            try
            {
                if (_serial?.IsOpen == true)
                    _serial.Close();
            }
            catch { }

            try
            {
                _serial?.Dispose();
            }
            catch { }

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
            catch { }
        }

        public void ClearLastNfcUid()
        {
            LastNfcUid = "";
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                while (_serial != null && _serial.BytesToRead > 0)
                {
                    string line = _serial.ReadLine().Trim();
                    _ui.InvokeAsync(() => HandleLine(line));
                }
            }
            catch { }
        }

        private void HandleLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            if (line == "READY" || line == "TEST_STARTED")
                return;

            if (line == "TOUCH_DETECTED" || IsButtonPressLine(line))
            {
                TouchDetected?.Invoke();
                return;
            }

            string nfcUid = ExtractNfcUid(line);
            if (!string.IsNullOrWhiteSpace(nfcUid))
            {
                LastNfcUid = nfcUid;
                NfcUidReceived?.Invoke(nfcUid);
                return;
            }

            if (line.StartsWith("DATA,", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseDataFrame(line, out var frame))
                    return;

                LiveEcgDr = frame.EcgDreapta;
                LiveEcgSt = frame.EcgStanga;
                LiveHr = frame.HeartRate;
                LiveSpo2 = frame.SpO2;
                LiveDist = frame.Distance;

                if (_streaming)
                {
                    Ecg.Add(new EcgSample { EcgDreapta = frame.EcgDreapta, EcgStanga = frame.EcgStanga });
                    HeartRate.Add(frame.HeartRate);
                    SpO2.Add(frame.SpO2);
                    Distance.Add(frame.Distance);
                }

                SampleReceived?.Invoke(frame.EcgDreapta, frame.EcgStanga, frame.HeartRate, frame.SpO2, frame.Distance);
            }
        }

        public static bool TryParseDataFrame(string line, out BioDataFrame frame)
        {
            frame = default;
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("DATA,", StringComparison.OrdinalIgnoreCase))
                return false;

            var parts = line.Split(',');
            if (parts.Length < 6) return false;

            if (!TryParseOptionalInt(parts[1], out int ecgDr)) return false;
            if (!TryParseOptionalInt(parts[2], out int ecgSt)) return false;
            if (!TryParseOptionalInt(parts[3], out int hr)) return false;
            if (!TryParseOptionalInt(parts[4], out int spo2)) return false;
            if (!TryParseOptionalInt(parts[5], out int distInt)) return false;

            frame = new BioDataFrame(ecgDr, ecgSt, hr, spo2, distInt != 0);
            return true;
        }

        public static string ExtractNfcUid(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return "";

            const string nfcMarker = "NFC_DETECTED,";
            if (line.StartsWith(nfcMarker, StringComparison.OrdinalIgnoreCase))
                return line[nfcMarker.Length..].Trim();

            const string uidMarker = "UID:";
            int markerIndex = line.IndexOf(uidMarker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0) return "";

            return line[(markerIndex + uidMarker.Length)..].Trim();
        }

        private static bool TryParseOptionalInt(string value, out int result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result = 0;
                return true;
            }

            return int.TryParse(value.Trim(), out result);
        }

        private static bool WaitForArduinoReady(SerialPort serial, TimeSpan timeout)
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
            return line.Equals("READY", StringComparison.OrdinalIgnoreCase)
                || line.Equals("TEST_STARTED", StringComparison.OrdinalIgnoreCase)
                || line.Equals("TOUCH_DETECTED", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("DATA,", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(ExtractNfcUid(line))
                || IsButtonPressLine(line);
        }

        private static bool IsButtonPressLine(string line)
        {
            return line.StartsWith("Buton ", StringComparison.OrdinalIgnoreCase)
                && line.IndexOf("apasat", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
