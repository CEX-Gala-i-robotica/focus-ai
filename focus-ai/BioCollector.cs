using System;
using System.Collections.Generic;
using System.IO.Ports;
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

        public event Action<int, int, int, int, bool>? SampleReceived;
        public event Action? TouchDetected;
        public event Action<string>? NfcUidReceived;

        private SerialPort? _serial;
        private bool _streaming;
        private readonly Dispatcher _ui = Application.Current.Dispatcher;
        private const int ArduinoBaudRate = 9600;

        private BioCollector() { }

        public bool TryOpen(string portName)
        {
            if (_serial?.IsOpen == true) return true;
            if (string.IsNullOrWhiteSpace(portName)) return false;

            try
            {
                _serial = new SerialPort(portName, ArduinoBaudRate)
                {
                    ReadTimeout = 500,
                    WriteTimeout = 500,
                    NewLine = "\n",
                    DtrEnable = true,
                    RtsEnable = true
                };

                _serial.DataReceived += OnDataReceived;
                _serial.Open();
                _serial.DiscardInBuffer();

                System.Threading.Thread.Sleep(1800);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Cannot open serial port {portName}.\n{ex.Message}\n\nTests will run without hardware collection.",
                    "Focus AI – Serial",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

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

            if (line.StartsWith("DATA,"))
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
            const string marker = "UID:";
            int markerIndex = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0) return "";

            return line[(markerIndex + marker.Length)..].Trim();
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

        private static bool IsButtonPressLine(string line)
        {
            return line.StartsWith("Buton ", StringComparison.OrdinalIgnoreCase)
                && line.IndexOf("apasat", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
