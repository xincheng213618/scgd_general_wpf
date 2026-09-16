using Spectrum.Models;
using System.Windows.Media;

namespace Spectrum
{
    internal readonly record struct SpectrumFillSegment(
        double StartWavelength,
        double EndWavelength,
        double StartValue,
        double EndValue,
        Color Color);

    internal static class SpectrumPlotFill
    {
        internal const double VisibleStartWavelength = 380;
        internal const double VisibleEndWavelength = 780;

        internal static IReadOnlyList<SpectrumFillSegment> CreateSegments(
            IReadOnlyList<SpectralData> samples,
            bool useAbsoluteSpectrum)
        {
            if (samples.Count < 2)
                return Array.Empty<SpectrumFillSegment>();

            List<SpectrumFillSegment> segments = new(samples.Count - 1);
            for (int i = 0; i < samples.Count - 1; i++)
            {
                SpectralData start = samples[i];
                SpectralData end = samples[i + 1];
                double startWavelength = start.Wavelength;
                double endWavelength = end.Wavelength;
                double startValue = useAbsoluteSpectrum ? start.AbsoluteSpectrum : start.RelativeSpectrum;
                double endValue = useAbsoluteSpectrum ? end.AbsoluteSpectrum : end.RelativeSpectrum;

                if (!double.IsFinite(startWavelength) || !double.IsFinite(endWavelength)
                    || !double.IsFinite(startValue) || !double.IsFinite(endValue)
                    || endWavelength <= startWavelength
                    || endWavelength <= VisibleStartWavelength
                    || startWavelength >= VisibleEndWavelength)
                {
                    continue;
                }

                startValue = Math.Max(0, startValue);
                endValue = Math.Max(0, endValue);
                ClipStart(ref startWavelength, ref startValue, endWavelength, endValue);
                ClipEnd(startWavelength, startValue, ref endWavelength, ref endValue);
                if (endWavelength <= startWavelength)
                    continue;

                double centerWavelength = (startWavelength + endWavelength) / 2;
                Color color = WavelengthToColor.Convert(centerWavelength);
                if (color.A == 0)
                    continue;

                segments.Add(new SpectrumFillSegment(
                    startWavelength,
                    endWavelength,
                    startValue,
                    endValue,
                    color));
            }
            return segments;
        }

        private static void ClipStart(ref double wavelength, ref double value, double endWavelength, double endValue)
        {
            if (wavelength >= VisibleStartWavelength)
                return;

            double ratio = (VisibleStartWavelength - wavelength) / (endWavelength - wavelength);
            value += (endValue - value) * ratio;
            wavelength = VisibleStartWavelength;
        }

        private static void ClipEnd(double startWavelength, double startValue, ref double wavelength, ref double value)
        {
            if (wavelength <= VisibleEndWavelength)
                return;

            double ratio = (VisibleEndWavelength - startWavelength) / (wavelength - startWavelength);
            value = startValue + (value - startValue) * ratio;
            wavelength = VisibleEndWavelength;
        }
    }
}
