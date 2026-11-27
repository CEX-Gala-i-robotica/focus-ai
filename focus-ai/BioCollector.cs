using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace focus_ai
{
    /// <summary>
    /// A timestamped PPG pulse-to-pulse interval. These are PRV intervals from
    /// MAX30102, not ECG-derived RR intervals.
    /// </summary>
    public readonly record struct PpgRrSample(uint DeviceTimestampMs, double IntervalMs, uint IrAmplitude);

    public readonly struct BioDataFrame
    {
        public BioDataFrame(
            uint deviceTimestampMs,
            int ecgChannel1,
            int ecgChannel2,
            int heartRate,
            int spo2,
            bool fingerDetected,
            bool ppgReady,
            int distanceCm = -1,
            bool distanceUpdated = false,
            bool hasLegacyDistanceFlag = false,
            bool legacyDistanceFlag = false)
        {
            DeviceTimestampMs = deviceTimestampMs;
            EcgDreapta = ecgChannel1;
            EcgStanga = ecgChannel2;
            HeartRate = heartRate;
            SpO2 = spo2;
            FingerDetected = fingerDetected;
            PpgReady = ppgReady;
            DistanceCm = distanceCm;
            DistanceAvailable = distanceUpdated && distanceCm >= 0;
            HasLegacyDistanceFlag = hasLegacyDistanceFlag;
            Distance = DistanceAvailable
                ? distanceCm > 0 && distanceCm < 30
                : hasLegacyDistanceFlag && legacyDistanceFlag;
        }

        public uint DeviceTimestampMs { get; }
        public int EcgDreapta { get; }
        public int EcgStanga { get; }
        public int HeartRate { get; }
        public int SpO2 { get; }
        public bool FingerDetected { get; }
        public bool PpgReady { get; }
        public int DistanceCm { get; }
        public bool DistanceAvailable { get; }
        public bool HasLegacyDistanceFlag { get; }
        public bool Distance { get; }
        public double? RrIntervalMs => null;
    }

    public struct EcgSample
    {
        public uint DeviceTimestampMs { get; set; }
        public int EcgDreapta { get; set; }
        /// <summary>-1 means the optional second MyoWare channel is not wired.</summary>
        public int EcgStanga { get; set; }
    }

    public sealed class BioCollector
    {
        private const int ArduinoBaudRate = 115200;
        private const string ExpectedHandshake = "PONG,FOCUS_AI,2";
        private static readonly TimeSpan PortHandshakeTimeout = TimeSpan.FromMilliseconds(700);
        private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(2);

        private static BioCollector? _instance;
        public static BioCollector Instance => _instance ??= new BioCollector();

        public List<EcgSample> Ecg { get; } = new();
        public List<int> HeartRate { get; } = new();
        public List<int> SpO2 { get; } = new();
        /// <summary>Legacy collection; remains empty because no distance sensor is installed.</summary>
        public List<bool> Distance { get; } = new();
        /// <summary>Valid ultrasonic readings in centimetres (only fresh measurements).</summary>
        public List<int> DistanceCm { get; } = new();
        /// <summary>PPG-derived pulse-to-pulse intervals, milliseconds (PRV).</summary>
        public List<double> RrIntervalsMs { get; } = new();
        public List<PpgRrSample> PpgRrSamples { get; } = new();

        public int LiveHr { get; private set; }
        public int LiveSpo2 { get; private set; }
        public int LiveEcgDr { get; private set; }
        public int LiveEcgSt { get; private set; } = -1;
        public bool LiveDist { get; private set; }
        public bool PpgSensorReady { get; private set; }
        public bool FingerDetected { get; private set; }

        public string LastNfcUid { get; private set; } = "";
        public string ConnectedPortName => _serial?.IsOpen == true ? _serial.PortName : "";
        public string LastDetectedPortName { get; private set; } = "";
        public bool IsConnected => _serial?.IsOpen == true;

        public event Action<int, int, int, int, bool>? SampleReceived;
        public event Action<string>? NfcUidReceived;
        public event Action<uint>? TouchDetected;
        public event Action<uint>? BuzzerStarted;
        public event Action<string, bool>? ConnectionStateChanged;

        private readonly Dispatcher? _ui = Application.Current?.Dispatcher;
        private readonly SemaphoreSlim _connectGate = new(1, 1);
        private readonly object _reconnectSync = new();
        private SerialPort? _serial;
        private volatile bool _streaming;
        private CancellationTokenSource? _reconnectCts;

        private BioCollector() { }

        /// <summary>
        /// Finds the Arduino by protocol handshake. COM numbers are discovered
        /// dynamically; DTR/RTS are disabled so probing does not reset unrelated
        /// serial devices or repeatedly reset the Uno bootloader.
        /// </summary>
        public async Task<bool> TryOpenAsync(CancellationToken cancellationToken = default)
        {
            bool connected = await TryConnectCoreAsync(cancellationToken).ConfigureAwait(false);
            if (!connected)
                EnsureReconnectLoop();
            return connected;
        }

        /// <summary>Compatibility entry point for older synchronous callers.</summary>
        public bool TryOpen()
        {
            try
            {
                return TryOpenAsync().GetAwaiter().GetResult();
            }
            catch
            {
                EnsureReconnectLoop();
                return false;
            }
        }

        public void RememberDetectedPort(string portName)
        {
            if (!string.IsNullOrWhiteSpace(portName))
                LastDetectedPortName = portName.Trim();
        }

        private async Task<bool> TryConnectCoreAsync(CancellationToken cancellationToken)
        {
            await _connectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_serial?.IsOpen == true)
                    return true;

                return await Task.Run(() => DiscoverAndConnect(cancellationToken), cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _connectGate.Release();
            }
        }

        private bool DiscoverAndConnect(CancellationToken cancellationToken)
        {
            string[] ports;
            try
            {
                ports = SerialPort.GetPortNames();
            }
            catch
            {
                return false;
            }

            IEnumerable<string> orderedPorts = ports
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => string.Equals(p, LastDetectedPortName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(GetComPortNumber);

            foreach (string portName in orderedPorts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SerialPort? probe = null;
                try
                {
                    probe = new SerialPort(portName, ArduinoBaudRate)
                    {
                        ReadTimeout = 100,
                        WriteTimeout = 300,
                        NewLine = "\n",
                        DtrEnable = false,
                        RtsEnable = false,
                        Handshake = Handshake.None
                    };
                    probe.Open();
                    probe.DiscardInBuffer();
                    probe.DiscardOutBuffer();
                    probe.WriteLine("PING,FOCUS_AI");

                    DateTime deadline = DateTime.UtcNow + PortHandshakeTimeout;
                    bool matched = false;
                    while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            string response = probe.ReadLine().Trim();
                            if (response.Equals(ExpectedHandshake, StringComparison.OrdinalIgnoreCase))
                            {
                                matched = true;
                                break;
                            }
                        }
                        catch (TimeoutException) { }
                    }

                    if (!matched)
                        continue;

                    probe.DiscardInBuffer();
                    if (_streaming)
                        probe.WriteLine("START_TEST");

                    probe.DataReceived += OnDataReceived;
                    _serial = probe;
                    LastDetectedPortName = portName;
                    RaiseConnectionState(portName, true);
                    probe = null; // ownership transferred to _serial
                    return true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // A non-Focus-AI device, busy port, or transient USB error.
                }
                finally
                {
                    if (probe != null)
                    {
                        try { probe.DataReceived -= OnDataReceived; } catch { }
                        try { if (probe.IsOpen) probe.Close(); } catch { }
                        try { probe.Dispose(); } catch { }
                    }
                }
            }

            return false;
        }

        private static int GetComPortNumber(string portName)
        {
            string digits = new string(portName.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)
                ? n
                : int.MaxValue;
        }

        private void EnsureReconnectLoop()
        {
            lock (_reconnectSync)
            {
                if (_reconnectCts != null)
                    return;

                var cts = new CancellationTokenSource();
                _reconnectCts = cts;
                _ = Task.Run(() => ReconnectLoopAsync(cts));
            }
        }

        private async Task ReconnectLoopAsync(CancellationTokenSource owner)
        {
            try
            {
                while (!owner.IsCancellationRequested && _serial?.IsOpen != true)
                {
                    try
                    {
                        if (await TryConnectCoreAsync(owner.Token).ConfigureAwait(false))
                            return;
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    await Task.Delay(ReconnectInterval, owner.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                lock (_reconnectSync)
                {
                    if (ReferenceEquals(_reconnectCts, owner))
                    {
                        _reconnectCts = null;
                        owner.Dispose();
                    }
                }
            }
        }

        public void StartStreaming(bool reset = false)
        {
            if (reset)
            {
                Ecg.Clear();
                HeartRate.Clear();
                SpO2.Clear();
                Distance.Clear();
                DistanceCm.Clear();
                RrIntervalsMs.Clear();
                PpgRrSamples.Clear();
            }

            _streaming = true;
            if (!Send("START_TEST"))
                EnsureReconnectLoop();
        }

        public void StopStreaming()
        {
            _streaming = false;
            Send("STOP_TEST");
        }

        /// <summary>Requests a short hardware buzzer cue from the Arduino.</summary>
        public bool TriggerBuzzer() => Send("BEEP");

        public bool Send(string command)
        {
            SerialPort? serial = _serial;
            if (serial?.IsOpen != true)
                return false;

            try
            {
                serial.WriteLine(command);
                return true;
            }
            catch
            {
                MarkDisconnected(serial);
                return false;
            }
        }

        public void Close()
        {
            _streaming = false;
            lock (_reconnectSync)
            {
                _reconnectCts?.Cancel();
            }

            SerialPort? serial = _serial;
            _serial = null;
            if (serial != null)
            {
                try { serial.WriteLine("STOP_TEST"); } catch { }
                try { serial.DataReceived -= OnDataReceived; } catch { }
                try { if (serial.IsOpen) serial.Close(); } catch { }
                try { serial.Dispose(); } catch { }
            }
            RaiseConnectionState("", false);
        }

        public void ClearLastNfcUid() => LastNfcUid = "";

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (sender is not SerialPort serial)
                return;

            try
            {
                while (serial.IsOpen && serial.BytesToRead > 0)
                {
                    string line;
                    try { line = serial.ReadLine().Trim(); }
                    catch (TimeoutException) { break; }

                    if (_ui != null && !_ui.CheckAccess())
                        _ui.BeginInvoke(new Action(() => HandleLine(line)));
                    else
                        HandleLine(line);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is UnauthorizedAccessException)
            {
                MarkDisconnected(serial);
            }
        }

        private void MarkDisconnected(SerialPort expected)
        {
            if (!ReferenceEquals(_serial, expected))
                return;

            _serial = null;
            try { expected.DataReceived -= OnDataReceived; } catch { }
            try { if (expected.IsOpen) expected.Close(); } catch { }
            try { expected.Dispose(); } catch { }

            RaiseConnectionState("", false);
            if (_streaming)
                EnsureReconnectLoop();
        }

        private void HandleLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            if (line.Equals("READY,FOCUS_AI,2", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("PONG,FOCUS_AI,2", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("TEST_STARTED", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("TEST_STOPPED", StringComparison.OrdinalIgnoreCase))
                return;

            if (TryParseTimestampedEvent(line, "TOUCH_DETECTED", out uint touchTimestamp))
            {
                if (_streaming)
                    TouchDetected?.Invoke(touchTimestamp);
                return;
            }

            if (TryParseTimestampedEvent(line, "BEEP_STARTED", out uint buzzerTimestamp))
            {
                BuzzerStarted?.Invoke(buzzerTimestamp);
                return;
            }

            string nfcUid = ExtractNfcUid(line);
            if (!string.IsNullOrWhiteSpace(nfcUid))
            {
                LastNfcUid = nfcUid;
                NfcUidReceived?.Invoke(nfcUid);
                return;
            }

            if (TryParsePpgRrLine(line, out PpgRrSample pulseSample))
            {
                if (_streaming)
                {
                    PpgRrSamples.Add(pulseSample);
                    RrIntervalsMs.Add(pulseSample.IntervalMs);
                }
                return;
            }

            if (line.StartsWith("DATA,", StringComparison.OrdinalIgnoreCase) &&
                TryParseDataFrame(line, out BioDataFrame frame))
            {
                LiveEcgDr = frame.EcgDreapta;
                LiveEcgSt = frame.EcgStanga;
                LiveHr = frame.HeartRate;
                LiveSpo2 = frame.SpO2;
                LiveDist = frame.Distance;
                PpgSensorReady = frame.PpgReady;
                FingerDetected = frame.FingerDetected;

                if (_streaming)
                {
                    if (frame.EcgDreapta >= 0)
                    {
                        Ecg.Add(new EcgSample
                        {
                            DeviceTimestampMs = frame.DeviceTimestampMs,
                            EcgDreapta = frame.EcgDreapta,
                            EcgStanga = frame.EcgStanga
                        });
                    }

                    if (frame.HeartRate >= 30 && frame.HeartRate <= 220)
                        HeartRate.Add(frame.HeartRate);
                    if (frame.SpO2 >= 70 && frame.SpO2 <= 100)
                        SpO2.Add(frame.SpO2);
                    if (frame.DistanceAvailable)
                    {
                        Distance.Add(frame.Distance);
                        DistanceCm.Add(frame.DistanceCm);
                    }
                    else if (frame.HasLegacyDistanceFlag)
                    {
                        // Preserve the historic proximity bit, but do not
                        // fabricate a centimetre value from a boolean flag.
                        Distance.Add(frame.Distance);
                    }
                }

                SampleReceived?.Invoke(
                    frame.EcgDreapta,
                    frame.EcgStanga,
                    frame.HeartRate,
                    frame.SpO2,
                    false);
            }
        }

        public static bool TryParseDataFrame(string line, out BioDataFrame frame)
        {
            frame = default;
            if (string.IsNullOrWhiteSpace(line) ||
                !line.StartsWith("DATA,", StringComparison.OrdinalIgnoreCase))
                return false;

            string[] parts = line.Split(',');
            if (parts.Length >= 8)
            {
                if (!uint.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint timestamp) ||
                    !TryParseInt(parts[2], out int a0) ||
                    !TryParseInt(parts[3], out int a1) ||
                    !TryParseInt(parts[4], out int hr) ||
                    !TryParseInt(parts[5], out int spo2))
                    return false;

                bool finger = parts[6] == "1";
                bool ppgReady = parts[7] == "1";
                int distanceCm = -1;
                bool distanceUpdated = false;
                if (parts.Length >= 10)
                {
                    if (!TryParseInt(parts[8], out distanceCm) || !TryParseInt(parts[9], out int updatedFlag))
                        return false;
                    distanceUpdated = updatedFlag == 1;
                }
                else if (parts.Length >= 9)
                {
                    if (!TryParseInt(parts[8], out distanceCm))
                        return false;
                    distanceUpdated = distanceCm >= 0;
                }

                frame = new BioDataFrame(timestamp, a0, a1, hr, spo2, finger, ppgReady,
                    distanceCm, distanceUpdated);
                return true;
            }

            // Compatibility with the old DATA,ECG1,ECG2,HR,SPO2,DISTANCE format.
            if (parts.Length >= 6 &&
                TryParseInt(parts[1], out int oldA0) &&
                TryParseInt(parts[2], out int oldA1) &&
                TryParseInt(parts[3], out int oldHr) &&
                TryParseInt(parts[4], out int oldSpo2))
            {
                bool oldDistance = parts[5] == "1";
                frame = new BioDataFrame(0, oldA0, oldA1, oldHr, oldSpo2,
                    oldHr > 0, oldHr > 0 || oldSpo2 > 0,
                    distanceCm: -1, distanceUpdated: false,
                    hasLegacyDistanceFlag: true, legacyDistanceFlag: oldDistance);
                return true;
            }

            return false;
        }

        /// <summary>Parses PPG_RR,timestamp_ms,interval_ms,IR_amplitude.</summary>
        public static bool TryParsePpgRrLine(string line, out PpgRrSample sample)
        {
            sample = default;
            if (string.IsNullOrWhiteSpace(line))
                return false;

            string[] parts = line.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 4 || !parts[0].Equals("PPG_RR", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!uint.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint timestamp) ||
                !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double rr) ||
                !uint.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint ir) ||
                !HrvMetrics.IsValidRrInterval(rr) || ir < 10000)
                return false;

            sample = new PpgRrSample(timestamp, rr, ir);
            return true;
        }

        public static bool TryParseTimestampedEvent(string line, string eventName, out uint timestampMs)
        {
            timestampMs = 0;
            if (string.IsNullOrWhiteSpace(line) || string.IsNullOrWhiteSpace(eventName))
                return false;

            string[] parts = line.Split(',', StringSplitOptions.TrimEntries);
            return parts.Length == 2 &&
                   parts[0].Equals(eventName, StringComparison.OrdinalIgnoreCase) &&
                   uint.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out timestampMs);
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
            return markerIndex < 0 ? "" : line[(markerIndex + uidMarker.Length)..].Trim();
        }

        private static bool TryParseInt(string value, out int result) =>
            int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        private void RaiseConnectionState(string portName, bool connected)
        {
            void Raise() => ConnectionStateChanged?.Invoke(portName, connected);
            if (_ui != null && !_ui.CheckAccess() && !_ui.HasShutdownStarted)
                _ui.BeginInvoke(new Action(Raise));
            else
                Raise();
        }
    }
}
