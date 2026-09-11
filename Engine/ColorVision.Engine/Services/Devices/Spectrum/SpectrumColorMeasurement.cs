using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Spectrum
{
    public readonly record struct SpectrumValuePoint(double Wavelength, double Value);

    public sealed record SpectrumColorMeasurement(
        int ResultId,
        DateTimeOffset CapturedAt,
        double Y,
        double CieX,
        double CieY,
        IReadOnlyList<SpectrumValuePoint> Spectrum)
    {
        public double? PeakAd { get; init; }
        public double? IntegrationTime { get; init; }
        public int? NdPort { get; init; }
        public string DeviceCode { get; init; } = string.Empty;
    }

    public sealed class SpectrumColorMeasurementSummary
    {
        public int ResultId { get; set; }
        public DateTime CapturedAt { get; set; }
        public float? Y { get; set; }
        public float? CieX { get; set; }
        public float? CieY { get; set; }
        public float? PeakAd { get; set; }
        public double? IpPercent => PeakAd / 65535d * 100d;
        public int? NdPort { get; set; }
    }
}
