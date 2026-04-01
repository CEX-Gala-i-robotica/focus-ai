using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Windows;
using System.Windows.Threading;

namespace focus_ai
{
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

        public event Action<int, int, int, int, bool>? SampleReceived;
        public event Action? TouchDetected;

        private SerialPort? _serial;
        private bool _streaming;
        private readonly Dispatcher _ui = Application.Current.Dispatcher;

        private BioCollector() { }

        public bool TryOpen(string portName)
        {
            if (_serial?.IsOpen == true) return true;
            if (string.IsNullOrWhiteSpace(portName)) return false;

            try
            {
                _serial = new SerialPort(portName, 115200)
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

            if (line == "TOUCH_DETECTED")
            {
                TouchDetected?.Invoke();
                return;
            }

            if (line.StartsWith("DATA,"))
            {
                var parts = line.Split(',');
                if (parts.Length < 6) return;

                if (!int.TryParse(parts[1], out int ecgDr)) return;
                if (!int.TryParse(parts[2], out int ecgSt)) return;
                if (!int.TryParse(parts[3], out int hr)) return;
                if (!int.TryParse(parts[4], out int spo2)) return;
                if (!int.TryParse(parts[5], out int distInt)) return;

                bool distFlag = distInt != 0;

                LiveEcgDr = ecgDr;
                LiveEcgSt = ecgSt;
                LiveHr = hr;
                LiveSpo2 = spo2;
                LiveDist = distFlag;

                if (_streaming)
                {
                    Ecg.Add(new EcgSample { EcgDreapta = ecgDr, EcgStanga = ecgSt });
                    HeartRate.Add(hr);
                    SpO2.Add(spo2);
                    Distance.Add(distFlag);
                }

                SampleReceived?.Invoke(ecgDr, ecgSt, hr, spo2, distFlag);
            }
        }
    }
}