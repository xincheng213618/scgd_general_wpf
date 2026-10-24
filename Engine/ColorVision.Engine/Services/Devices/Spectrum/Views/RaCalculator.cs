using cvColorVision;

namespace ColorVision.Engine.Services.Devices.Spectrum.Views;

/// <summary>Compatibility entry point. CCT is derived from the spectrum for standard CRI.</summary>
public static class RaCalculator
{
    public static float ComputeRa(float[] fPL, float fSpect1, float fInterval, float cct)
        => (float)(SpectrumColorRendering.Calculate(fPL, fSpect1, fInterval).Ra ?? double.NaN);
}