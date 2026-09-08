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
        IReadOnlyList<SpectrumValuePoint> Spectrum);
}
