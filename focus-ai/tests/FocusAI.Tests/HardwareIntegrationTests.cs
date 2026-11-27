using focus_ai;
using Xunit;

namespace FocusAI.Tests;

public sealed class HardwareIntegrationTests
{
    [Fact]
    public void PpgProtocol_ParsesTimestampedPpgRrAndRejectsLegacyUnqualifiedRr()
    {
        Assert.True(BioCollector.TryParsePpgRrLine("PPG_RR,123456,812.500,24500", out PpgRrSample sample));
        Assert.Equal((uint)123456, sample.DeviceTimestampMs);
        Assert.Equal(812.5, sample.IntervalMs, precision: 3);
        Assert.Equal((uint)24500, sample.IrAmplitude);

        Assert.False(BioCollector.TryParsePpgRrLine("RR,812.500", out _));
        Assert.False(BioCollector.TryParsePpgRrLine("PPG_RR,123,812.5,2000", out _));
        Assert.False(BioCollector.TryParsePpgRrLine("PPG_RR,123,99,24000", out _));
    }

    [Fact]
    public void DataProtocol_ParsesCurrentTimestampedFrameAndMarksMissingSecondChannel()
    {
        const string line = "DATA,12345,512,-1,72,98,1,1,18,1";
        Assert.True(BioCollector.TryParseDataFrame(line, out BioDataFrame frame));
        Assert.Equal((uint)12345, frame.DeviceTimestampMs);
        Assert.Equal(512, frame.EcgDreapta);
        Assert.Equal(-1, frame.EcgStanga);
        Assert.Equal(72, frame.HeartRate);
        Assert.Equal(98, frame.SpO2);
        Assert.True(frame.FingerDetected);
        Assert.True(frame.PpgReady);
        Assert.True(frame.Distance);
        Assert.True(frame.DistanceAvailable);
        Assert.Equal(18, frame.DistanceCm);
    }

    [Fact]
    public void DataProtocol_DoesNotInventUltrasonicDistanceWhenReadingIsMissing()
    {
        const string line = "DATA,12346,510,-1,-1,-1,0,1,-1,1";
        Assert.True(BioCollector.TryParseDataFrame(line, out BioDataFrame frame));
        Assert.False(frame.DistanceAvailable);
        Assert.False(frame.Distance);
        Assert.Equal(-1, frame.DistanceCm);
    }

    [Fact]
    public void TouchAndBuzzerProtocol_ParsesDeviceTimestamps()
    {
        Assert.True(BioCollector.TryParseTimestampedEvent("TOUCH_DETECTED,99812", "TOUCH_DETECTED", out uint touch));
        Assert.Equal((uint)99812, touch);
        Assert.True(BioCollector.TryParseTimestampedEvent("BEEP_STARTED,99000", "BEEP_STARTED", out uint beep));
        Assert.Equal((uint)99000, beep);
        Assert.False(BioCollector.TryParseTimestampedEvent("TOUCH_DETECTED,bad", "TOUCH_DETECTED", out _));
    }

    [Fact]
    public void LegacyDataProtocol_RemainsReadable()
    {
        Assert.True(BioCollector.TryParseDataFrame("DATA,512,500,72,98,1", out BioDataFrame frame));
        Assert.True(frame.Distance);
        Assert.True(frame.HasLegacyDistanceFlag);
        Assert.False(frame.DistanceAvailable);
        Assert.Equal(-1, frame.DistanceCm); // a legacy flag is not a centimetre measurement
        Assert.Equal(72, frame.HeartRate);
    }

    [Fact]
    public void GazeCsv_CountsTrackingLossSeparatelyFromTargetFixation()
    {
        string path = Path.Combine(Path.GetTempPath(), $"focusai-gaze-{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(path,
                "elapsed_ms,phase,gaze_valid,gaze_x_px,gaze_y_px,target_available,target_x_px,target_y_px,target_radius_px\n" +
                "0,1,1,100,100,0,-1,-1,52\n" +
                "100,2,1,200,200,1,210,210,52\n" +
                "200,2,0,-1,-1,1,210,210,52\n" +
                "300,2,1,400,400,1,210,210,52\n");

            GazeSessionSummary summary = GazeSessionSummary.ReadCsv(path);
            Assert.Equal(4, summary.TotalSamples);
            Assert.Equal(3, summary.ValidSamples);
            Assert.Equal(3, summary.TargetSamples);
            Assert.Equal(2, summary.TrackedTargetSamples);
            Assert.Equal(1, summary.OnTargetSamples);Assert.True(summary.OnTargetFraction.HasValue);
            Assert.Equal(0.5, summary.OnTargetFraction.Value, precision: 6);
            Assert.Equal(2d / 3d, summary.TargetTrackingCoverage, precision: 6);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}
