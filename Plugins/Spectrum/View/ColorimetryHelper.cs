using cvColorVision;

namespace Spectrum.View;

/// <summary>Compatibility entry points using the shared CIE colourimetry implementation.</summary>
public static class ColorimetryHelper
{
    public static double CalculateCCT(double x, double y) => SpectrumChromaticity.Calculate(x, y).ColorTemperature ?? double.NaN;
    public static double CalculateDominantWavelength(double x, double y, double refX = 0.3127, double refY = 0.3290)
    {
        var result = SpectrumChromaticity.Calculate(x, y, refX, refY);
        // Negative wavelength denotes a complementary wavelength (purple colours).
        return result.DominantWavelength ?? -result.ComplementaryWavelength ?? double.NaN;
    }
    public static double CalculateExcitationPurity(double x, double y, double dominantWavelength, double refX = 0.3127, double refY = 0.3290)
        => SpectrumChromaticity.Calculate(x, y, refX, refY).ExcitationPurity ?? double.NaN;
}
